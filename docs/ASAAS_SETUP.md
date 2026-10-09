# Setting up Asaas for Foji

How billing works, what to configure in Asaas, and what to put in AWS so the
API can charge customers. Asaas menu names are in Portuguese, exactly as the
panel shows them. Checked against docs.asaas.com in October 2026.

## How Foji charges

| Plan | How it's paid | Renewal |
| --- | --- | --- |
| Monthly | Card only, on Asaas's hosted page | Charged automatically every month |
| Yearly | Card or Pix | Card: automatic. Pix: we email the Pix for the next year when Asaas creates it |

- **Upgrade:** the customer pays the prorated difference for the days left (saved card, or a payment link), and the new plan unlocks when it's paid.
- **Downgrade:** takes effect at the end of the paid period.
- **Card or cycle change:** a new subscription starts when the current period ends, so nobody is charged twice.
- **Payment fails:** the assistant keeps working for 7 days (`Billing:GraceDays`), then pauses. After 30 days unpaid (`Billing:CancelAfterDays`), the subscription is canceled at Asaas.
- **WhatsApp overage:** plans with a per-message price are billed once per monthly window, on the saved card or by payment link. Amounts under R$ 5 are waived.
- **Emails:** Foji sends every billing email. Asaas's own emails, SMS and voice calls are turned off for each customer we create.
- **Trial:** 7 days, no card, unchanged.

## 1. Create the Asaas account

1. Sign up at asaas.com as **Pessoa Jurídica** with the Code Phoenix Dev LTDA CNPJ.
2. **Minha conta → Informações comerciais:** fill everything in, and in the site field put **`fojiai.com`**.
   - Asaas only sends customers back to URLs on the domain registered here.
   - If Asaas insists on the exact host, register the dashboard host the API uses, the one in `/foji/{env}/App/BaseUrl` (see the note in step 7).
   - If the dashboard lives on another domain, set `Asaas:ReturnBaseUrl` to a URL on the registered domain.
3. Send the documents Asaas asks for (CNPJ card, ID and selfie of the partner, bank account). Approval usually takes up to 2 business days.
4. **Pix → Minhas chaves:** register a Pix key (the CNPJ is fine). Without one, Pix QR codes only last until midnight.

> **One account, several products.** Foji tags everything it creates with `foji:...` (`Asaas:ReferencePrefix`) and ignores events that aren't its own. The return-URL domain rule above is the main reason to give each product its own Asaas account (or subaccount) instead.

## 2. Start with the sandbox

Do everything below in **sandbox.asaas.com** first, then repeat it in production.

- **Sandbox API:** `https://api-sandbox.asaas.com/v3`. Its keys start with `$aact_hmlg_`.
- **Production API:** `https://api.asaas.com/v3`. Its keys start with `$aact_prod_`.

## 3. API key

1. Go to **Integrações → Chaves de API → Gerar chave de API**. Only account admins can do this, and only on the website.
2. Copy the key right away; Asaas shows it only once. The `$` at the start is part of the key.
3. Leave it without an expiration date, or put a reminder in your calendar.

Foji makes one small call a day so Asaas doesn't disable the key for inactivity (Asaas disables keys after 3 months unused and deletes them after 6).

## 4. Webhook

Go to **Integrações → Webhooks → Adicionar**:

| Field | Value |
| --- | --- |
| Nome | Foji |
| URL | `https://api-dev.fojiai.com/api/billing/webhook/asaas` (and `https://api.fojiai.com/...` for production) |
| E-mail | the address that should hear about delivery failures |
| Versão da API | v3 |
| Token de autenticação | a random string, 32 to 255 characters, no spaces (generate with `openssl rand -hex 32`) |
| Tipo de envio | **Sequencial** |
| Situação | Ativo |

**Eventos:** tick everything under **Cobranças**, **Assinaturas**, **Checkout** and **Notas fiscais**.

Foji stores each event and answers 200 at once. If the queue ever shows **interrompida** (paused) in the panel, fix the cause and use **Reativar fila**.

## 5. Ask your Asaas account manager for two things

1. **Card tokenization** (*tokenização de cartão de crédito*). This is **required**. Without it:
   - Asaas won't change the price of a card subscription, so upgrades and downgrades on monthly card plans don't reach the next charge. Foji writes a note on the subscription (visible to super-admins) when that happens.
   - Upgrade differences and WhatsApp overage can't be charged to the saved card; the customer gets a payment link instead.

   Once Asaas confirms it's enabled, set `/foji/{env}/Asaas/TokenizationEnabled` to `true`.
2. Confirm that **Checkout Asaas with recurring card charges** is enabled on the account.

## 6. Nota fiscal (NFS-e)

Do this with your accountant.

1. **Notas fiscais → Configurações:** inscrição municipal, tax regime, and your city hall access (login, or an A1 digital certificate, depending on the city).
2. Pick the municipal service code for the software subscription. Common choices are LC 116 item 1.05 (licensing of software) or 1.03 (processing and hosting of data). Your accountant decides.
3. Set the rates (ISS, and PIS/COFINS/CSLL/IR/INSS if retained) in AWS (step 7), then set `Asaas:Nfse:Enabled` to `true`.

Foji asks Asaas to issue the note when each payment is confirmed: automatically for subscriptions, and one note per upgrade or overage charge. The PDF link shows up in the customer's invoice list. Each note costs R$ 0,49.

## 7. Settings in AWS (Parameter Store)

The API reads parameters under `/foji/dev/` and `/foji/prod/` and picks up changes within 5 minutes, without a redeploy. Use **SecureString** for keys and tokens.

| Parameter | Value |
| --- | --- |
| `Asaas/BaseUrl` | `https://api-sandbox.asaas.com/v3`, later `https://api.asaas.com/v3` |
| `Asaas/ApiKey` | the key from step 3 (SecureString) |
| `Asaas/WebhookToken` | the token from step 4 (SecureString) |
| `Asaas/TokenizationEnabled` | `true` once step 5 is done |
| `Asaas/ReturnBaseUrl` | only if the dashboard isn't on the domain registered in step 1 |
| `Asaas/Nfse/Enabled` | `true` once step 6 is done |
| `Asaas/Nfse/MunicipalServiceCode` | from your accountant |
| `Asaas/Nfse/MunicipalServiceName` | from your accountant |
| `Asaas/Nfse/Iss`, `Pis`, `Cofins`, `Csll`, `Inss`, `Ir` | percentages, e.g. `2` for 2% |
| `Asaas/Nfse/RetainIss` | `true` or `false` |
| `App/BaseUrl` | the real dashboard address, e.g. `https://app-dev.fojiai.com` |
| `Billing/GraceDays` | optional, default `7` |
| `Billing/CancelAfterDays` | optional, default `30` |
| `Billing/MinChargeValue` | optional, default `5` |

Check `App/BaseUrl` in particular: the code falls back to `app.foji.ai` when it's missing, but the site is `fojiai.com`. Email links and the "back from payment" page use it.

The old `Stripe/SecretKey` and `Stripe/WebhookSecret` parameters can be deleted.

## 8. Plans

In Foji, as super-admin, open **Admin → Planos**:

1. **Review every price.** All plans were switched to reais. If a plan was entered in dollars (for example "29"), it now reads R$ 29.
2. Fill in **Preço anual** for plans that should have a yearly option, for example 10 times the monthly price ("2 meses grátis"). Leave it at 0 for monthly-only plans.

People already subscribed keep the price they signed up with. A new price applies to new subscriptions and plan changes.

## 9. Test in the sandbox before going live

| Payment | How to test |
| --- | --- |
| Card approved | 4444 4444 4444 4444, CVV 123, any future date |
| Card refused | 5184 0197 4037 3151 |
| Pix or boleto | open the charge in the sandbox panel and click **Confirmar recebimento**, or call `POST /v3/sandbox/payment/{id}/confirm` |
| Overdue | `POST /v3/sandbox/payment/{id}/overdue` |
| Super-admin sweep | `POST /api/admin/billing/sweep` runs the hourly job right away (grace periods, overage) |

Walk through these in Foji:

1. **New monthly card subscription.** Asaas's page opens. After paying you come back to "Confirmando seu pagamento..." and the plan shows as active.
2. **Yearly with Pix.** The Pix page opens; confirm it in the sandbox panel.
3. **Upgrade.** With tokenization on, it's charged instantly; without it, you get a payment link.
4. **Downgrade.** Shows "Seu plano muda para X em ...".
5. **Change card** while the plan is paid. Then check in the Asaas panel that the new subscription's first charge is dated at the end of the current period, not today. This hasn't been confirmed against the real sandbox yet.
6. **Cancel, then undo.**
7. **Overdue charge.** The red banner shows the pause date; after `GraceDays` the assistant pauses.

Things only the real sandbox can confirm. The code is written for them, but please verify:

- Hosted checkout accepts our existing customer id and the product image.
- Hosted checkout with a future first due date charges on that date, not immediately (used for card changes and cycle switches).
- Changing a card subscription's price works once tokenization is on.
- The return page works on your registered domain.

## 10. Go live

1. Repeat steps 3 and 4 in the production account.
2. Put the production key, webhook token and `https://api.asaas.com/v3` in `/foji/prod/` (and in `/foji/dev/`, if dev is what customers use).
3. Make one real test with your own card on a temporary, private R$ 5 plan (Asaas has minimum charge values). Then cancel it in Foji and refund it in the Asaas panel.

## Fees (asaas.com/precos-e-taxas, October 2026; your contract may differ)

| Item | Fee |
| --- | --- |
| Card, à vista and subscriptions | 2,99% + R$ 0,49 per payment |
| Pix | R$ 1,99 per payment received |
| NFS-e | R$ 0,49 per note |
| Asaas notifications | R$ 0,99 per paid charge; Foji turns them off and sends its own emails |

New accounts get about 3 months of lower promotional fees.

## Later: Pix Automático

Asaas supports Pix Automático (recurring Pix that charges by itself) for company accounts whose **CNPJ has been active for at least 6 months**. When Code Phoenix qualifies, monthly Pix plans can be added without the "pay every month by hand" problem.
