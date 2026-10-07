# Security policy

## Reporting a vulnerability

Please use GitHub's private vulnerability reporting feature for this repository. Do not open a public issue for an unpatched security problem. Include the affected component, impact, a minimal reproduction, and any suggested mitigation. Do not include live customer records or credentials in a report.

## Supported versions

Security fixes target the latest commit on the protected `dev` branch. There are no tagged releases yet.

## Security controls

- Keep provider keys, SMTP credentials, admin bootstrap credentials, and real databases outside Git.
- Demo mode uses local fixtures and adapters and makes no provider requests.
- Cookie-authenticated state changes require antiforgery validation; payment and shipping callbacks require provider signatures.
- ASP.NET Data Protection key-ring files are encrypted with a stable key stored in the local environment file or Azure Key Vault.
- Payment callback event IDs prevent duplicate settlement, and KargoJet shipment requests use idempotency keys.
- CI runs dependency audits and CodeQL. Dependabot monitors npm, NuGet, and GitHub Actions dependencies.
