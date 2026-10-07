# Birmarket

Birmarket is a Turkish-language storefront for thoughtfully selected everyday products. It includes a responsive Angular web client, an ASP.NET Core API, a portable SQLite database, customer accounts and password recovery, order management, and payment, shipping, and email provider integrations.

The application has two data-isolated runtime modes. Demo mode provides realistic product, account, checkout, shipment, and email flows without contacting external services. Real mode uses configured merchant and service credentials. A visible demo banner distinguishes simulated activity from real transactions.

## Quick start

Requirements: .NET 10 SDK, Node.js 24, npm, and PowerShell or a POSIX shell.

```powershell
dotnet tool restore
dotnet run --project backend/Birmarket.Api/Birmarket.Api.csproj --launch-profile http
```

In a second terminal:

```powershell
cd frontend/birmarket-web
npm ci
npm start
```

Open <http://localhost:4200>. The Angular development server proxies `/api` requests to the API at `http://localhost:5045`. On first API start, the root `birmarket.env` file is copied from the safe demo template if it is missing; the generated local file is ignored by Git.

Demo accounts:

| Role | Email | Password |
| --- | --- | --- |
| Customer | `customer@birmarket.local` | `DemoCustomer!234` |
| Administrator | `admin@birmarket.local` | `DemoAdmin!234` |

These credentials are for the isolated demo database only. Do not reuse them in a real environment.

## Runtime modes

| Mode | Start method | Data and integrations |
| --- | --- | --- |
| Local development | `dotnet run` and `npm start` | Local SQLite; Angular development proxy; demo mode by default |
| Docker / WSL 2 | `docker compose up --build` | App container plus a mounted persistent SQLite directory |
| Azure preparation | Bicep templates under `infrastructure/azure` | Linux App Service configuration prepared for persistent `/home/data` storage and one instance; no resources are deployed by this repository |

The Azure profile retains SQLite and therefore targets a single application instance. Horizontal scaling needs a separate database decision.

## Configuration

`birmarket.env.example` documents the available settings. Copy it to the ignored `birmarket.env` for a fresh workspace. The default is:

```dotenv
birmarket_demo_mode=true
```

Demo mode uses its own database at `.data/birmarket-demo.sqlite3`, a separate authentication key ring, generated sample catalog data, and simulated iyzico, PayTR, KargoJet, and email operations. No external provider request is made.

To connect real or provider-sandbox accounts, set `birmarket_demo_mode=false` and complete all required settings in `birmarket.env`. Startup checks payment, KargoJet, SMTP, admin bootstrap, data-protection key, and public HTTPS callback settings before enabling real adapters. `birmarket_provider_environment` selects `sandbox` or `live` service endpoints. The real-mode database defaults to `.data/birmarket.sqlite3` and is never seeded with demo products or accounts.

Never commit `birmarket.env`, SQLite files, real customer data, or provider credentials. Azure values belong in Azure's secret configuration, not in a deployed environment file or the frontend bundle.

## Payments and fulfillment

- iyzico Checkout Form and PayTR iFrame are initialized by the backend; provider callbacks are verified before an order is marked paid.
- Birmarket does not collect or store card numbers. A national identity number is requested only for the real iyzico checkout request and is not persisted in SQLite.
- KargoJet creates shipments after payment and updates shipping status from signed, idempotent webhooks. An administrator can retry failed shipment creation.
- Transaction emails use Azure Communication Services SMTP in real mode. Demo emails are stored as simulated outbox entries.

Merchant agreements, sandbox/live credentials, a verified sender domain, an HTTPS callback URL, and a KargoJet sender address are required before live transactions or shipping can be used.

## Development checks

From the repository root:

```powershell
dotnet tool restore
dotnet test Birmarket.sln
```

From `frontend/birmarket-web`:

```powershell
npm ci
npm run format:check
npm run build
npm test
npm run e2e:install
npm run e2e
npm audit
```

Playwright runs the storefront and API in demo mode with a per-run temporary SQLite file. It does not use live credentials, modify the local demo/real databases, or create real shipments.

## Project guides

- [Architecture](ARCHITECTURE.md)
- [Setup and runtime modes](SETUP.md)
- [Privacy and data handling](PRIVACY.md)
- [Security policy](SECURITY.md)
- [Contributing](CONTRIBUTING.md)
- [Support](SUPPORT.md)
- [Legal and distribution review](LEGAL-REVIEW.md)
- [Change log](CHANGELOG.md)
- [Release notes](RELEASE-NOTES.md)
- [Publishing checklist](PUBLISHING-CHECKLIST.md)

## License

Original Birmarket source is licensed under the MIT License. Third-party services and dependencies remain under their own terms; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
