# Contributing

Thanks for helping improve Birmarket. Keep changes focused and explain the customer problem they address.

## Project conventions

- Use English for repository paths, filenames, identifiers, comments, code, and documentation.
- The current customer-facing store is Turkish. Keep user-visible copy in Turkish until a separately planned localization change is approved.
- Keep business rules and provider secrets on the server. Never add credentials, SQLite data, customer records, build output, or generated browser reports.
- Add or update focused unit, integration, or browser coverage for changed behavior.
- Preserve demo/real database separation and add migrations for entity changes.

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

Provider sandbox calls require sandbox credentials and must never use live accounts in automated tests. Demo-mode browser tests use the local simulators.

## Pull requests

- Describe the user-facing behavior, data changes, and operational impact.
- Include relevant test results and mention any check that could not run.
- Update setup, privacy, security, third-party notices, or release documentation when the change affects those areas.
- Verify that `birmarket.env`, database files, and credentials are not included.
