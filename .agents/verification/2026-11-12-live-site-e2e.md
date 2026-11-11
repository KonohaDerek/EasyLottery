# Live-site exploratory QA and Playwright regression suite — 2026-11-12

- Target: https://easylotter.k-derek.synology.me/ (static assets last-modified 2026-10-22; likely release v0.0.7)
- Branch/worktree: `claude/live-site-e2e-20261112` / `.worktrees/live-site-e2e-20261112`, base `origin/main` 43f69b8
- Tools: ego-lite (ego-browser 2.2.0) exploratory session; curl header/status probes; Playwright 1.62.1
- Scope: anonymous, read-only. No login, no settings change, no draw. One Passkey options request with a non-admin email.
- Not covered: authenticated admin pages (Passkey login required), OBS rendering in OBS itself.

## Command

```bash
cd tests/e2e
npx playwright test -c live/live.config.mjs
```

Result: 28 passed (15 normal + 13 `test.fail` known defects). Each known-defect test was
also run once with `test.fail` disabled to confirm it fails for the documented reason.

## Issues filed

Defects: #318 routing fallback/404, #319 admin email enumeration, #320 mobile nav/title,
#321 login a11y, #322 OBS error states, #323 favicon/robots.txt. Existing #253 covers HTTP→HTTPS.

Competitor-based proposals: #324–#333.
