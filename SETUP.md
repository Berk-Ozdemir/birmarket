# Setup guide

## Local development on Windows

1. Install the .NET 10 SDK, Node.js 24, and npm.
2. Start with the root `birmarket.env` in demo mode. The API creates a local copy from `birmarket.env.example` on first start when needed; it is ignored by Git.
3. Restore the local EF tool and run the API from the repository root:

   ```powershell
   dotnet tool restore
   dotnet run --project backend/Birmarket.Api/Birmarket.Api.csproj --launch-profile http
   ```

4. In a second terminal, run the web application:

   ```powershell
   cd frontend/birmarket-web
   npm ci
   npm start
   ```

5. Open <http://localhost:4200>. The API readiness endpoint is <http://localhost:5045/api/health/ready>.

## Docker Desktop with WSL 2

Docker Desktop must have its WSL 2 engine enabled. From the repository root:

```powershell
docker compose up --build
```

The container listens on port 8080. SQLite persists as portable files in the host `.data` directory shared with local development. Stop the service with `Ctrl+C`; use `docker compose down` to remove the container while keeping the database files.

## Demo and real settings

`birmarket.env` uses dotenv-style `key=value` entries and is loaded by the API on startup. Process environment values override file values. Docker Compose reads the same file.

Demo mode is the default and uses mock adapters. To use provider sandbox or live services, set `birmarket_demo_mode=false`, choose `birmarket_provider_environment=sandbox` or `live`, and fill all required settings. The API fails at startup with the missing setting names if configuration is incomplete. It never writes setting values into logs.

Configure iyzico and PayTR merchant credentials, KargoJet token/webhook secret and default sender address, SMTP account and verified sender, admin bootstrap email/password, a stable data-protection key, and the public HTTPS callback base URL. Demo mode creates and persists a random data-protection key in the ignored `.data` directory if none is supplied. Provider sandbox testing still calls external sandbox endpoints and requires sandbox credentials. Keep live credentials out of GitHub commits and the Angular build.

Configure the PayTR notification URL as `<public-base-url>/api/payments/paytr/callback` and the iyzico callback URL as `<public-base-url>/api/payments/iyzico/callback`. After the public HTTPS endpoint is reachable, run `.\scripts\register-kargojet-webhook.ps1` once. It registers signed KargoJet events and stores the returned secret only in the ignored local environment file. Azure deployments use the corresponding Key Vault secret instead.

## Database migrations

The repository includes a local `dotnet-ef` tool manifest and committed EF migrations. After changing an entity, create a migration from the root:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add DescribeTheChange --project backend/Birmarket.Api/Birmarket.Api.csproj --startup-project backend/Birmarket.Api/Birmarket.Api.csproj --output-dir Data/Migrations
```

Review the generated migration and test it against an isolated SQLite file before committing.

## Azure preparation

The Bicep template describes a Linux App Service plan, custom container settings, HTTPS, and persistent `/home/data` storage. It fixes scale at one instance to keep SQLite file access safe. Review parameters and fill secrets through Azure configuration before deployment. This project does not create Azure resources or deploy the application.
