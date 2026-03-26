# FojiApi — Plan

## Role in the Foji AI Ecosystem

FojiApi is the **CMS and management API** for Foji AI. It owns the PostgreSQL schema (via EF Core migrations), handles all business logic for companies, users, agents, files, subscriptions, and billing. It is the single source of truth for structured data and exposes a REST API consumed by `foji-ui`.

The Python services (`foji-ai-api`, `foji-worker`) share the same PostgreSQL database but **never run migrations** — FojiApi owns the schema exclusively.

---

## Tech Stack

- **.NET 10** — ASP.NET Core Web API
- **Entity Framework Core** — ORM, migrations, PL/pgSQL triggers
- **PostgreSQL** — Primary database (AWS RDS)
- **Stripe.net** — Subscription billing
- **AWS S3** — File storage (via AWSSDK.S3)
- **JWT (HS256)** — Authentication tokens
- **SendGrid / SES** — Transactional email (verification, invitations)
- **AWS Secrets Manager** — Credentials at runtime

## Architecture

```
FojiApi.Core/           → Domain entities, interfaces, business rules
FojiApi.Infrastructure/ → EF Core DbContext, repositories, services, Stripe, S3, email
FojiApi.Web.API/        → Controllers, DTOs, middleware, auth, Stripe webhooks
```

---

## Data Model (entities owned by this service)

| Entity | Purpose |
|--------|---------|
| `Company` | Tenant — name, slug, logo, Stripe customer |
| `User` | Global user account (email + password) |
| `UserCompany` | Junction: user ↔ company with role (owner/admin/user) |
| `Agent` | AI agent: industry type, prompts, token, WhatsApp config |
| `AgentFile` | File uploaded for agent context (S3 reference + extracted text) |
| `Plan` | Starter / Professional / Scale — limits & Stripe price ID |
| `Subscription` | Active plan per company, Stripe subscription state |
| `Invitation` | Email invite → role assignment per company |
| `AIModel` | Available AI models (admin-managed: provider, model ID, pricing) |
| `AuditLog` | Immutable action trail per company |

### Key Relationships
```
User ──< UserCompany >── Company
Company ──< Agent ──< AgentFile
Company ──< Subscription >── Plan
Company ──< Invitation
Agent (IndustryType → system prompt generated at create time)
```

---

## Key Controllers

| Controller | Prefix | Notes |
|-----------|--------|-------|
| `AuthController` | `/api/auth` | signup, login, verify email, forgot/reset password |
| `CompaniesController` | `/api/companies` | CRUD, member list, remove member |
| `AgentsController` | `/api/agents` | CRUD, regenerate token, get embed code |
| `FilesController` | `/api/files` | upload to S3, delete, processing status |
| `SubscriptionsController` | `/api/subscriptions` | Stripe checkout, portal, webhook handler |
| `PlansController` | `/api/plans` | public listing |
| `UsersController` | `/api/users` | profile update, password change, companies list |
| `InvitationsController` | `/api/invitations` | send invite, accept via token |
| `AIModelsController` | `/api/admin/ai-models` | super-admin CRUD |
| `AuditLogsController` | `/api/audit-logs` | read-only, scoped to company |

---

## Auth & JWT

**Signup flow** (no admin required — self-service):
1. `POST /api/auth/signup` → create user, send email verification
2. `GET /api/auth/verify-email?token=` → mark `EmailVerifiedAt`
3. `POST /api/auth/create-company` → create company, set user as `owner`
4. `POST /api/auth/login` → return signed JWT

**JWT Claims**:
```json
{
  "userId": 1,
  "email": "user@example.com",
  "isSuperAdmin": false,
  "companies": [
    { "companyId": 1, "role": "owner" },
    { "companyId": 2, "role": "admin" }
  ]
}
```

**Request-scoped `CurrentUserService`**: extracts claims from `HttpContext`, provides `GetActiveCompanyId()`, `GetRole()`, `IsSuperAdmin()`.

---

## Plan Enforcement

`PlanEnforcementService` checks before mutating:
- **Agent create**: count active agents in company ≤ `Plan.MaxAgents`
- **File upload**: file size ≤ 30MB
- **WhatsApp enable**: `Plan.HasWhatsApp` must be `true`
- **Subscription status**: block writes if `past_due` or `canceled`

---

## Stripe Integration

- Checkout: `POST /api/subscriptions/checkout` → Stripe Checkout Session URL
- Portal: `POST /api/subscriptions/portal` → Customer Portal URL
- Webhook (`POST /api/subscriptions/webhook`): handles:
  - `checkout.session.completed` → activate subscription
  - `customer.subscription.updated` → sync status/period
  - `customer.subscription.deleted` → cancel
  - `invoice.payment_failed` → set `past_due`

---

## Industry System Prompts

`IndustryPromptService` returns base system prompt by `IndustryType`:

- `accounting_finance` → Financial/accounting assistant, informational only disclaimer
- `law` → Legal research assistant, "not legal advice" disclaimer
- `internal_systems` → Internal knowledge assistant, uses `{company_name}` placeholder

Agent's `UserPrompt` is appended after the system prompt. Agent's `AgentLanguage` injects "Always respond in [language]" at the end.

---

## Migrations

EF Core, same pattern as TutoriaApi. Files in `FojiApi.Infrastructure/Migrations/`:
- `*_InitialSchema` — all tables + indexes + FK constraints
- `*_AddUpdatedAtTriggers` — PL/pgSQL `BEFORE UPDATE` triggers for `UpdatedAt`
- Seeded data: 3 Plans, 3 AIModels (Gemini, OpenAI, Bedrock/Nova)

---

## Environment Variables

```
DATABASE_URL                   # PostgreSQL connection string
JWT_SECRET                     # HS256 signing key
STRIPE_SECRET_KEY
STRIPE_WEBHOOK_SECRET
AWS_REGION
AWS_S3_BUCKET                  # foji-files-dev / foji-files-prod
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
SENDGRID_API_KEY               # or AWS_SES_* if using SES
APP_BASE_URL                   # https://app.foji.ai (for email links)
INTERNAL_API_KEY               # service-to-service secret (used by foji-ai-api)
```

---

## Deploy Target

**AWS App Runner** (dev + prod)
- Dev: 0.25 vCPU / 0.5 GB, request-based auto-pause
- Prod: 1 vCPU / 2 GB, auto-scale

**CI/CD** (GitHub Actions):
- `.github/workflows/deploy-dev.yml` — triggers on push to `main`
- `.github/workflows/deploy-prod.yml` — `workflow_dispatch` (manual)
- Both run EF Core migrations before deploying the new image

---

## Connections to Other Services

| Service | How |
|---------|-----|
| `foji-ui` | Consumes this REST API directly |
| `foji-ai-api` | Shares same PostgreSQL DB (read-only on schema) |
| `foji-worker` | Notified via SQS when files are uploaded; shares DB |
| Stripe | Webhook → `/api/subscriptions/webhook` |
| AWS S3 | Files uploaded here, S3 keys stored in `AgentFile` |
