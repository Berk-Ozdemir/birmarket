## Summary

Describe the customer or maintenance change.

## Verification

- [ ] `dotnet test Birmarket.sln`
- [ ] `npm run build` and `npm test`
- [ ] `npm run e2e` when browser behavior changes
- [ ] `npm audit` and migration review when relevant

## Data, security, and documentation

- [ ] No credentials, SQLite files, customer records, or generated reports are included.
- [ ] Demo and real mode behavior remains isolated.
- [ ] English repository documentation and Turkish user-facing copy are updated where needed.
