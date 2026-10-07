# Privacy and data handling

## Data Birmarket stores

Customer account email, display name, password hash, saved addresses, wishlist entries, checkout contact/address details, order lines, payment provider references, shipping references, and queued/sent order email content are stored in SQLite. Passwords are handled by ASP.NET Core Identity and are not stored in plaintext. Demo data is synthetic and isolated in a separate database file.

An identity number is requested only when a customer selects iyzico in real mode. It is sent to iyzico as part of its checkout initialization request and is not written to the Birmarket database. Card data is entered in provider-hosted forms and is not collected by Birmarket.

## External services

Demo mode makes no calls to payment, shipping, or email providers. In real mode, checkout data needed to process payment is sent to the chosen iyzico or PayTR service; paid-order recipient and parcel data is sent to KargoJet; order messages are sent through the configured SMTP provider. Provider responses, references, tracking information, and delivery states are retained with the order.

## Local and hosted operation

Local and Docker installations store data at the configured SQLite path. Operators are responsible for filesystem access, backups, retention, and deletion. The Azure preparation uses persistent App Service storage; Azure settings and logs are administered separately. Birmarket does not intentionally collect telemetry.

## Demo mode

Demo transactions and delivery updates are simulations. A visible demo banner is shown, the confirmation screen says that steps were simulated, and sample accounts/data must not be treated as real customer records.

Operators should publish their own customer privacy, retention, and service terms appropriate to the business and jurisdictions where they operate.
