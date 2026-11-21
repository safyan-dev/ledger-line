# Email delivery

The API sends five emails today: email confirmation, resend confirmation, password reset, email
change confirmation, and team invitations.

## What Mailpit does and does not do

Mailpit is an **SMTP catcher**, not a mail server. It accepts every message and stops there:

```text
API -> Mailpit:1025 -> [ stops here ] -> someone opens Mailpit UI :8025
```

- It has **no outbound delivery path**: no MX lookup, no relay.
- A user who registers `someone@gmail.com` **never receives** that mail.
- The operator reads the message — including the confirmation link — in the Mailpit UI.

That is why it is free, credential-free and works offline. Never describe it as delivering email.

### Why you cannot relay onward for free

1. **Port 25 is blocked** by AWS, GCP, Azure, DigitalOcean, Railway and most consumer ISPs, so a
   container cannot deliver directly.
2. **Reputation**: mail from a fresh VPS IP without SPF/DKIM/DMARC, from a domain you do not control,
   is rejected or junked.

"Free" and "lands in the real inbox" cannot both be true from a self-hosted container.

## Choosing a mode

| Mode | Cost | User receives mail? | Best for |
|---|---|---|---|
| `outbox` (default in Development) | free | no — write to `backend/src/Api/email-outbox/` | offline local work |
| `smtp` + Mailpit | free | no — captured in the Mailpit UI | demos, local development, tests |
| `smtp` + a real relay (Brevo, SES, SMTP2GO) | free tier + a domain | **yes** | anything real |

Real delivery requires a verified sender. Brevo documents that free sender domains cannot be
authenticated, so reliable delivery needs a domain you own plus SPF, DKIM and DMARC records.

### Hosted demo consideration

If you host a public demo with Mailpit, a stranger cannot confirm their signup, and you should not
expose the Mailpit UI publicly — it contains every confirmation token and reset link. Options:

- **B1.** You drive the demo yourself; document the steps.
- **B2.** Add a demo-only auto-confirm flag (a behaviour change, off by default).
- **B3.** Use a real relay and own the full email flow.

## Configuration

```text
Email:Provider        outbox | log | smtp        default outbox (Development)
Email:FromAddress     no-reply@ledgerline.local
Email:FromName        Ledgerline
Email:Smtp:Host       localhost
Email:Smtp:Port       1025
Email:Smtp:UseStartTls false                     true for 587 relays
Email:Smtp:Username   (only for a real relay)
Email:Smtp:Password   (only for a real relay)
```

`Email:WriteToOutbox` still works as a legacy alias when `Email:Provider` is not set.

### Local

```bash
docker compose up -d mailpit
dotnet user-secrets set "Email:Provider" "smtp" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:Host" "localhost" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:Port" "1025" --project backend/src/Api
```

Register a tenant, then open <http://localhost:8025> and click the confirmation link.

### Real delivery

```bash
dotnet user-secrets set "Email:Provider" "smtp" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:Host" "smtp-relay.brevo.com" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:Port" "587" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:UseStartTls" "true" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:Username" "<relay-user>" --project backend/src/Api
dotnet user-secrets set "Email:Smtp:Password" "<relay-password>" --project backend/src/Api
dotnet user-secrets set "Email:FromAddress" "no-reply@your-domain" --project backend/src/Api
```

Publish SPF, DKIM and DMARC for the sending domain, and prefer a sending subdomain such as
`mail.your-domain` so its reputation is separate from the app domain.

## Credentials

This repository is public. No SMTP password or provider key is ever committed.

| Environment | Storage |
|---|---|
| Local | .NET user-secrets |
| CI | none — tests replace `IEmailSender` |
| Hosted demo | none (Mailpit needs no credentials) |
| Production | GitHub Environment secret, injected as `Email__Smtp__Password` |

Mailpit's UI can be protected with `MP_UI_AUTH=user:password` when hosted. Never expose SMTP port
1025 publicly.

## Failure behaviour

`SmtpEmailSender` logs a warning and returns if delivery fails. A failed email never fails
registration, invitation, or password reset — those operations are already committed. Recipients and
message bodies are never logged.
