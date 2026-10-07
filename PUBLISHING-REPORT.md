# GitHub publication readiness record

This document records the pre-publication review. The target repo's visibility history is the source of truth for the final publication timestamp and current visibility.

## Repository

- Target: [`berk-ozdemir/birmarket`](https://github.com/berk-ozdemir/birmarket)
- Default branch: `dev`
- Published source: the reviewed `dev` branch tip
- Publication sequence: keep the repo private through source and workflow verification; then make it public and immediately apply the branch/security controls available to a public repo on GitHub Free before final read-only verification.

## Source verification

- Reviewed baseline: `0dc54eb6b8808cee957228a4bb86ebf3519380f7`.
- .NET format and tests: passed; 12 integration tests.
- Angular format, production build, and unit tests: passed; 5 unit tests.
- Playwright E2E: passed locally and in GitHub Actions for desktop and mobile scenarios.
- Dependency checks: npm audit found no vulnerabilities; .NET reported no vulnerable packages.
- Infrastructure and packaging: Bicep build, webhook-script syntax, Docker image build, and local container smoke checks passed.
- GitHub CI for the reviewed baseline: [run 37669400744](https://github.com/Berk-Ozdemir/birmarket/actions/runs/37669400744) passed on Linux and Windows backend tests, frontend checks, Playwright E2E, and container build.
- File review: English repository paths and documentation; Turkish customer-facing UI. Local environment secrets, SQLite data, dependencies, and build output are ignored and excluded from source history.

## GitHub preparation

- The destination had no readable repository before preparation. It was created privately, with HTTPS `origin` and the reviewed `dev` history.
- `dev` is the default branch. Issues are enabled; Projects and Wiki are disabled. Squash merge is the only enabled merge method, and merged branches are deleted.
- Dependabot runs weekly. Automatic major-version updates are ignored; patch/minor updates remain enabled. Automatically opened major upgrade pull requests were closed with an explanation and were not merged.
- The `berk-ozdemir` identity was verified through GitHub CLI, then the account active before login was restored. No other GitHub login was removed or logged out.
- GitHub Free does not allow branch protection for this private personal repository. The protection endpoint returned HTTP 403 and stated the repository must be public or upgraded. Apply protected `dev` and public-repository secret protection immediately after the visibility change. CodeQL is configured for public repositories; private-repository runs are intentionally skipped.

## Final publication

The source branch is prepared for public release at the target URL above. In the final publication sequence, change visibility, apply GitHub Free's public-repository branch and secret protection settings, and then perform read-only checks of public accessibility, default/protected `dev`, and CI/CodeQL. Do not add source changes or create a release tag as part of the visibility transition.
