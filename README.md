# FojiApi

Primary backend API for the Foji platform. Handles auth, company/agent CRUD, file uploads, Stripe billing, WhatsApp webhook ingestion, and admin operations.

## Tech

- .NET 10 / ASP.NET Core
- PostgreSQL (via EF Core + Npgsql)
- AWS App Runner

## Local Development

```bash
dotnet run --project src/FojiApi.Web.API
```

Runs on `http://localhost:5000`.

## Environment

Config is loaded from AWS SSM Parameter Store by prefix (`AWS_SSM_PREFIX`). For local dev, use `appsettings.Development.json` or user secrets.

## Deploy

- **Dev**: Push to `main` triggers deploy via GitHub Actions to App Runner.
- **Prod**: Manual `workflow_dispatch` with confirmation.
