# GitHub publication checklist

Repository publication is the final stage after application work and verification are complete. Use the installed GitHub CLI (`gh`) only. Keep the target private while preparing its initial history, settings, required checks, and publication report; change visibility to public as the final repository-setting action.

## Source readiness

- [ ] Confirm the reviewed source is on `dev` and the worktree contains no unintended files.
- [ ] Run .NET tests and formatting checks, Angular build/unit tests, Playwright E2E, `npm audit`, and .NET vulnerability checks.
- [ ] Review migrations, demo/real separation, payment callback validation, shipment idempotency, and real-mode configuration validation.
- [ ] Inspect all Git objects and the final tree for provider credentials, `birmarket.env`, SQLite files, customer data, build artifacts, private logs, and generated test reports.
- [ ] Verify MIT license ownership and third-party notices. Review external-service and privacy descriptions.
- [ ] Complete README, setup, security, contribution, release, and publication documents in English. Keep current user-facing interface copy in Turkish.

## GitHub preparation with `gh`

- [ ] Record the current active `github.com` account with `gh auth status`; do not log out or remove any existing account.
- [ ] At publication time, add the `berk-ozdemir` account using `gh auth login --hostname github.com --with-token` with a replacement credential supplied through masked terminal input. Omit `--git-protocol`: this machine already uses HTTPS, and that option changes the host-wide Git protocol setting for every logged-in account.
- [ ] Verify the new identity with `gh api user --jq .login`. Restore the previously active account immediately, then use a process-scoped `GH_TOKEN` for publication commands so other shells retain their prior active account.
- [ ] Inspect the destination with `gh repo view berk-ozdemir/birmarket`. Preserve any existing remote refs and history; never force-push or delete repository data.
- [ ] Ensure the repository has the intended HTTPS `origin`, `dev` default branch, and PR-only protection with successful required CI checks. Configure a repo-only helper under `.git` to retrieve the `berk-ozdemir` token from the GitHub CLI credential store; do not track the helper, change global Git settings, or run `gh auth setup-git` globally.
- [ ] Enable issues, secret scanning, push protection, Dependabot, and CodeQL where supported. Verify CI and security workflows.
- [ ] Complete `PUBLISHING-REPORT.md` with the actual remote branch, source commit, public URL, workflow runs, and settings checks.
- [ ] As the final GitHub repository mutation, set the destination to public. Then perform read-only verification and restore the account that was active before publication.

## Publication boundary

Publish reviewed source only. Do not publish binaries, SQLite databases, provider credentials, real customer records, generated Playwright reports, or ignored runtime files. Do not create a version tag unless the separate release checklist is complete.
