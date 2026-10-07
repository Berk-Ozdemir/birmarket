# Release notes

## First tagged release — not published

Public source publication is separate from a tagged application release. Do not describe the repository publication as a supported software release. Before creating the first version tag, update this document with the release number and date and complete `PUBLISHING-CHECKLIST.md`.

### Release scope

- Turkish-language responsive storefront and administrator screens.
- ASP.NET Core API backed by local SQLite migrations.
- Customer accounts, saved addresses, shopping cart, order tracking, coupons, and inventory management.
- iyzico and PayTR hosted checkout, KargoJet shipment tracking, and configurable SMTP order notifications.
- Isolated end-to-end demo mode with no real external transactions.
- Local web server, Docker/WSL 2 support, and Azure infrastructure preparation.

### Limitations to disclose

- Azure uses a single App Service instance with SQLite. Horizontal scale-out is unsupported.
- Live payments, shipping, and email require provider accounts, valid credentials, verified callback/sender configuration, and a public HTTPS base URL.
- No service-level agreement or legal, financial, or security certification is provided.
- This repository does not deploy Azure resources or include real customer data, provider credentials, or database files.
