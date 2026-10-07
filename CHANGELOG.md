# Changelog

All notable project changes are recorded here. There are no tagged software releases yet.

## Unreleased

### Added

- Responsive Turkish Angular storefront with product search, categories, sorting, favorites, cart, coupons, checkout, order tracking, and administrator screens.
- ASP.NET Core API with SQLite migrations, customer accounts, address book, product/order administration, payment providers, shipment tracking, and transactional email.
- Isolated demo and real database modes controlled by `birmarket_demo_mode` in the local environment file.
- iyzico, PayTR, KargoJet, and Azure Communication Services SMTP adapters with demo simulators.
- Angular unit tests, .NET integration tests, Playwright browser scenarios, Docker/WSL packaging, and Azure Bicep preparation.
- GitHub community, security, privacy, contribution, release, and publication documents.

### Known limitations

- The Azure template targets a single App Service instance while the application uses SQLite.
- Live provider requests require merchant accounts, credentials, a public HTTPS callback URL, and verified shipping/email configuration.
- Demo mode is a simulation and cannot charge cards, create real shipments, or send real customer messages.
