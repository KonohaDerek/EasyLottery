# Live-site E2E

Read-only Playwright checks for a deployed EasyLottery site. They do not log in,
change settings, or trigger draws; the only auth call is one Passkey options
request with a non-admin email (rate-limited endpoint).

```bash
cd tests/e2e
npx playwright test -c live/live.config.mjs
LIVE_BASE_URL=https://other.example npx playwright test -c live/live.config.mjs
```

Tests marked `test.fail()` document known defects and link their GitHub issue.
When an issue is fixed and deployed, the test starts "unexpectedly passing";
remove the `test.fail()` line in the fix PR.
