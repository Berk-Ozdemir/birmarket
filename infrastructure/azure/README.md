# Azure infrastructure preparation

`main.bicep` describes a Linux App Service custom container, HTTPS-only access, one Basic B1 worker, persistent `/home/data` storage, a system-assigned managed identity, and a Key Vault with RBAC. The template is a preparation artifact; it is not run by CI and does not deploy resources.

## Required inputs

- `appName`: unique lowercase App Service name.
- `containerImage`: a container image reference published through the future release process.
- `keyVaultName`: globally unique vault name.
- `publicBaseUrl`: HTTPS storefront URL used for payment callbacks.
- `demoMode`: defaults to `true`; use `false` only after every required secret exists in Key Vault.

Before setting `demoMode=false`, create these Key Vault secrets:

- `birmarket-admin-password`
- `birmarket-data-protection-key` — use at least 32 random characters and keep it stable across restarts.
- `iyzico-api-key`, `iyzico-secret-key`
- `paytr-merchant-id`, `paytr-merchant-key`, `paytr-merchant-salt`
- `kargojet-api-token`, `kargojet-webhook-secret`
- `smtp-username`, `smtp-password`, `email-from`

The App Service managed identity receives the Key Vault Secrets User role. Store no secrets in the Bicep source or parameter files.

## Validation

```powershell
az bicep build --file infrastructure/azure/main.bicep
```

The generated `main.json` is a local build artifact and is ignored by Git. Azure SQL, multi-instance scaling, cloud resource creation, and application deployment are outside this preparation step.
