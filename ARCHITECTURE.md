# Architecture

## Components

- `frontend/birmarket-web` contains the Turkish Angular standalone application, API client, Vitest unit tests, and Playwright browser scenarios.
- `backend/Birmarket.Api` contains the ASP.NET Core API, ASP.NET Core Identity, EF Core SQLite persistence, payment integrations, shipping, email, and demo adapters.
- The production container serves the compiled Angular application and `/api` from one ASP.NET origin. During local development, Angular proxies `/api` to the API process.
- `infrastructure/azure` contains Bicep preparation for a Linux App Service deployment. It is not a deployment pipeline.

## Data and trust boundaries

The server owns product prices, coupon validity, stock reservation, order totals, payment state, and shipment state. Checkout sends product identifiers and quantities; client-calculated totals are not accepted.

Demo and real modes use separate SQLite files, Data Protection key rings, application names, and authentication cookies. A demo administrator cookie cannot authorize a real-mode database session. Demo mode seeds synthetic products and two local demo accounts. Real mode applies migrations but creates only the administrator specified by bootstrap settings. Customer identity and shipment addresses are stored in the database; card details are handled by hosted payment forms. The iyzico identity number is forwarded for the payment request without being stored. ASP.NET Data Protection keys are persisted beside the selected database and protected by `birmarket_data_protection_key`.

Browser authentication uses an HTTP-only, same-site cookie. Mutating browser requests require an antiforgery token. Provider callbacks bypass browser antiforgery only after provider signature validation. Callback event identifiers are persisted for idempotency.

## API groups

- `/api/catalog` — category and product discovery, search, sorting, and paging.
- `/api/auth` and `/api/account` — account sessions, password recovery, current user, and saved addresses.
- `/api/orders` — coupon validation, checkout, guest tracking, and customer order history.
- `/api/payments` and `/api/webhooks` — verified payment and shipment notifications.
- `/api/wishlist` — authenticated saved products.
- `/api/admin` — catalog, inventory, discounts, order, shipment, and fulfillment operations.
- `/api/health` — liveness and database readiness.

OpenAPI is available in the development environment at `/openapi/v1.json`.

## Order flow

1. The API validates checkout fields and loads current products and prices.
2. A SQLite transaction validates stock, reserves items, applies a valid coupon, records an order, and optionally saves the customer address.
3. The selected payment adapter creates a hosted checkout session. A provider callback is verified before the order becomes paid.
4. Successful payment triggers an idempotent KargoJet shipment request and an order email. Failure or payment-session creation errors release reserved stock.
5. KargoJet's signed event updates shipment status. Administrators can retry shipment or email delivery after service failures.

## SQLite operation

The database file paths are configured separately with `birmarket_demo_sqlite_path` and `birmarket_real_sqlite_path`. Docker bind-mounts the host `.data` directory at `/app/.data`; the Azure template uses persistent `/home/data` and a single instance. Back up a stopped or checkpointed database file and keep its containing directory persistent.
