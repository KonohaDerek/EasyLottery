# PR #336 reconciliation — 2026-11-12

## Scope and provenance

This is a frozen pre-review reconciliation record, not current approval or deployment evidence.
PR #336 starts at `82617d2672e61ce12c375efd615b655210eb10a6`; main is
`388f1e9884306a35850ce8a4ebdb2a828534aaf2` after merged PR #334.
The original live-suite commit `01aa1d1cfafde98e1c14ca28ba38efd4f636eb2c`
was already cherry-picked onto main as `ab5af48`. The suite, its config,
the ordinary suite's live exclusion, and its original report are already present.
No application code, test implementation, configuration, dependency or lockfile is changed here.

The merge preserves the historical QA decision and ego-browser report verbatim.
The sole conflict was `.agents/state/current.md`: main's entire frozen #334
snapshot is retained byte-for-byte. The complete prior PR state is preserved
verbatim from `82617d2` at `.agents/state/history/2026-11-12-live-site-qa.md`;
its dates, results and delivery flags describe that historical task, not this reconciliation.
The only non-document change is ignoring `test-results-live/` and
`playwright-report-live/` under `tests/e2e`. Existing original worktree reports,
screenshots and traces were not accessed for modification or removed.

## Route and plan

The authoritative GitHub release lookup verified published stable `v0.0.7`
(non-draft, non-prerelease; published 2026-10-22T03:44:20Z) on 2026-11-12.
Actual Claude Opus 5.5 planned in session `00164458-a48e-476b-b0b5-5cf587cf30bb`;
Codex performs this bounded documentation/conflict reconciliation.
Manual structured route: low application risk, one isolated worktree,
static verification and listing-only browser-suite discovery. `TYPESAFE_API_KEY`
was absent, so Jev was not called and the active host fallback applies.
No new product behavior or OpenSpec capability is introduced.

Two planning assumptions were corrected by reading the actual tests:
- The historical suite is not GET-only: it includes one non-admin-email POST to
  `/api/auth/passkey/options` and invalid-email client-side button checks. It is
  not a real Passkey ceremony, draw, settings write or payment. This task does
  not execute any of those tests.
- There are 12 textual `test.fail` declarations, one expanded across two viewport
  cases: the historical result is 13 expected failures, not the grep count.

## Evidence boundaries

The historical live-suite result called "28 passed" means **15 ordinary passes
plus 13 expected known-defect failures**. It is not 28 successful unmasked
assertions. Keep its original report and dates unchanged.
The separate ego-browser report contains its own nine manual cases and an
inconclusive Playwright subset attempt (4 passed, 15 environment/transport failures).
Those results were not rerun or reclassified here.

PR #334 separately records **148/148 ordinary, unmasked local browser regression
passes** in `.agents/verification/2026-11-12-live-fixes.md`. That historical local
run is distinct from the deployed-site suite and from this task's discovery counts.
No production contact or test execution took place in this reconciliation.
Neither merged code nor local tests establish that production defects were fixed
or deployed. The unchanged live config defaults to production; do not run it
without appropriate target-specific authorization. Listing below explicitly
uses loopback and never starts a server/browser or executes test bodies.

## Checks on the reconciled tree

Node: `/Users/derek/.hermes/tools/node-26.12.1-darwin-arm64/bin`.

- `npm ci --prefix tests/e2e --ignore-scripts --cache /Users/derek/Documents/Project/EasyLottery-review-evidence/pr336-20261112/npm-cache`: exit 0; four packages, zero vulnerabilities.
- From `tests/e2e`, `BASE_URL=http://127.0.0.1:18951 LIVE_BASE_URL=http://127.0.0.1:18951 ./node_modules/.bin/playwright test --list --reporter=list`: exit 0; **148 tests in 22 files**, no live files.
- Same environment, `./node_modules/.bin/playwright test -c live/live.config.mjs --list --reporter=list`: exit 0; **28 tests in four files**, all live files.
- Byte-identity checks preserve main current state, original QA state archive,
  original QA reports/decision, and main application/test/config tree.
- `git diff --check`: exit 0; historical decision JSON parses successfully.

No fresh runtime/browser/backend test pass is claimed here. Full final-head CI
and two independent provider reviews remain pending at this frozen snapshot.
Their authoritative SHA-bound outputs, commands, findings and round ledger live
outside this tree at `EasyLottery-review-evidence/pr336-20261112`.
Maximum five candidate rounds; each changed candidate needs both providers again.
PR #336 must remain unmerged: main is unprotected and the pinned baseline gates
remain unmet. PR #334's one-time exception does not apply. No settings, release
or deployment changes are part of this work. Rollback, if needed, is a separately
authorized revert of this reconciliation merge, not a force push or deletion.
