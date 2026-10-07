# Birmarket web client

This Angular application is the Turkish customer and administrator interface for Birmarket. Repository code, filenames, identifiers, and documentation use English; customer-facing text remains Turkish.

Run the API from the repository root, then use:

```powershell
npm ci
npm start
```

The development server listens at <http://localhost:4200> and proxies `/api` requests to the backend on port 5045.

Build and test commands:

```powershell
npm run build
npm test
npm run e2e:install
npm run e2e
```

See the repository [setup guide](../../SETUP.md) for runtime configuration and provider details.
