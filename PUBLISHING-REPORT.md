# GitHub publication report

Status: **Pending final publication**.

Target: [`berk-ozdemir/birmarket`](https://github.com/berk-ozdemir/birmarket)

Planned default branch: `dev`

Planned visibility: public

Publication tool: locally installed GitHub CLI (`gh`) only.

## Pre-publication record

- Local source branch: `dev`.
- Reviewed application commit: `fe41a045915668d5b94e31f8591d4d5eca045eac`.
- .NET formatting and tests: passed; 12 integration tests.
- Angular formatting, production build, and unit tests: passed; 5 unit tests.
- Playwright browser suite: passed; 7 applicable desktop/mobile cases, with 3 viewport-specific skips.
- Dependency checks: `npm audit --audit-level=high` found no vulnerabilities; .NET reported no vulnerable packages.
- Infrastructure and packaging: Bicep build, PowerShell parser, Docker build, and local readiness/storefront smoke checks passed.
- Secret and generated-file review: no known credential patterns were found in staged source. `birmarket.env`, local SQLite data, dependencies, and build output are ignored.
- Repository preparation: local Git history created on `dev`; no remote configured and no GitHub account state changed. Remote inspection, branch rules, GitHub workflow runs, and public verification remain pending the final publication stage.

## Publication execution record

Complete this section immediately before the public visibility change. Do not record access tokens, secret values, or private customer data.

- Publication date: pending.
- Published source commit: pending.
- Public URL verification: pending.
- Default/protected branch verification: pending.
- CI and CodeQL workflow verification: pending.
- Pre-publication active GitHub account restored: pending.

Public source publication is not a tagged software release and does not deploy the application to Azure.
