# PR #349 review remediation — local verification

## Candidate and environment

- Existing PR: [#349](https://github.com/KonohaDerek/EasyLottery/pull/349), still open and not modified remotely.
- Worktree/branch: `.worktrees/issue-305-integrate` / `codex/issue-305-integrate-20261114`.
- Base branch HEAD remains `705ebbfb119c330c4d18fab03902ddd6629e88a2`; no commit was created. Candidate product/test patch fingerprint (base SHA + binary diff of six changed source/test files): `8a82dcfd8dff293e15ef28a8d3cb43f109477545e89ae55d4049dc6ec6c6f001`.
- Disposable source snapshot used for tests: `/private/tmp/issue305-pr349-candidate.abrdP0`; pre-execution full-source fingerprint: `f115eee85177de84b9375ae73476418a701171f9a60be909b406123c100ae54b`. The six product/test files were byte-compared with the worktree after tests and matched.
- Container: existing EasyLottery Dev Container image `vsc-devcontainer-card-shop-20261114-29415094dd8a1d1a6298fde22372f17f537c4b8445c44e68d0c61671f928834a:latest`, image ID `sha256:f371eb001f7405b29a279d78753ca1f30ac3622cc57b98889fff7b65dcd7455b`; Node `v22.23.5`, npm `10.9.9`, .NET SDK `10.0.404`, Playwright `1.62.1`, Chromium `151.0.7922.34`.
- Tests ran against a source copy in a disposable container, with no `.env`, SSH/GitHub/Codex credentials, production endpoints/data, or host data directories mounted. API used Development mode, a fresh `/tmp/easylottery-pr349-test-data`, the test-only session-token endpoint, and loopback `127.0.0.1:18931`. Container was removed at exit.

## Changes implemented by Claude

- PR finding [#4572120108](https://github.com/KonohaDerek/EasyLottery/pull/349#discussion_r4572120108): `applyPersistedWinner` stages the winner state, persists the advanced locked pool first, and retains pending ID/payload if storage rejects the write. Added E2E for storage failure, same-tab re-query and reload reconciliation.
- PR finding [#4572120111](https://github.com/KonohaDerek/EasyLottery/pull/349#discussion_r4572120111): rechecks `mutationsLocked` after `await file.text()` before applying CSV participants, winners or rates. Added a gated `Blob.prototype.text()` E2E race.
- PR finding [#4572120112](https://github.com/KonohaDerek/EasyLottery/pull/349#discussion_r4572120112): JSON and CSV use explicit scalar fields for channel scope and the three source UUIDs; import validates them and retains legacy files. Added round-trip and malformed/legacy Vitest cases.
- Ported the pending-draw unlock guard from `d2f21ad` and storage-only `phase` stripping from `d295c206`; added unit/E2E coverage.

## Verification

Commands run inside the disposable Dev Container against the exact source snapshot above:

```sh
cd /workspace/src/EasyLotteryWeb
npm ci --silent --cache /tmp/npm-vue-cache
npm test
npm run typecheck
npm run build -- --outDir ../EasyLotteryWasm/wwwroot/vue
cd /workspace/tests/e2e
npm ci --silent --cache /tmp/npm-e2e-cache
PLAYWRIGHT_BROWSERS_PATH=/tmp/pw-browsers npx playwright install --with-deps chromium
cd /workspace
ASPNETCORE_ENVIRONMENT=Development Testing__EnableAdminSessionToken=true \
  Storage__Directory=/tmp/easylottery-pr349-test-data \
  dotnet run --project src/EasyLotteryAPI/EasyLotteryAPI.csproj \
  --no-launch-profile --urls http://127.0.0.1:18931
cd /workspace/tests/e2e
BASE_URL=http://127.0.0.1:18931 npm test -- core-lottery-eligibility.spec.mjs
```

| Check | Result |
| --- | --- |
| Vue Vitest `npm test` | Passed: 35 files / 198 tests |
| Vue `npm run typecheck` | Passed |
| Vue production build | Passed; two existing SignalR Rollup annotation warnings |
| Playwright `core-lottery-eligibility.spec.mjs` | Passed: 6/6, including pending unlock/retry, locked-pool storage failure/reload, and CSV read/lock race |
| OpenSpec `npx --yes @fission-ai/openspec@1.7.0 validate --all --strict` | Passed: 50/50 |
| `git diff --check` | Passed |
| `python3 -m json.tool .agents/decisions/2026-11-15-pr349-review-remediation.json` | Passed |
| .NET API test suite | Not run; no API code changed in this remediation |
| GitHub CI | Not run; branch was not pushed or changed remotely |

## Codex review finding — unresolved

At `src/EasyLotteryWeb/src/views/LotteryHomeView.vue:289-295`, if the restored locked pool does not match the saved record, the function sets `lockError` but continues. The guard at lines 304-313 then skips persisting, but lines 315-329 still commit winner UI state and clear the pending draw. This loses the only reconciliation ID/payload while the pool was not durably advanced. Return unresolved without clearing pending on this mismatch and add a synthetic regression before accepting remediation 3.6. Severity: P2 data recovery/integrity; no automatic redrawing is enabled while `lockError` remains, but a manual invalid-lock clear can remove the guard after the pending record has already been lost.

## Independent Claude review blocker

After Derek directly authorized the review in this conversation, a new Herdr Core read-only terminal `t2s` independently reviewed the same product/test fingerprint `8a82dcfd8dff293e15ef28a8d3cb43f109477545e89ae55d4049dc6ec6c6f001`. Claude confirmed the three original PR findings, the storage-failure recovery, CSV race, serialization compatibility, phase stripping, and unlock guard were handled as described. It also confirmed the Codex mismatch finding and raised one P3 UI issue:

- **P2 mismatch finding confirmed:** when the restored locked pool does not match the saved draw, `applyPersistedWinner` sets `lockError` but continues, writes a winner into UI state, clears pending draw state, and returns success without advancing the locked pool. Because `lockError` is in-memory, a subsequent reload clears the stop state; `clearInvalidLock` is also available while pending. Claude recommended returning unresolved while keeping pending, blocking invalid-lock clear while pending, and adding a synthetic two-page/shared-localStorage regression.
- **P3 stale error notice:** if a locked-pool storage write fails and same-tab reconciliation later succeeds, the previous global `error` message is never cleared, so success and failure messages remain visible together. Claude recommended clearing that message on successful persistence or keeping the storage warning only in `pendingDrawMessage`.
- Claude did not run tests/build, did not edit files, and did not change Git or remote state. Its source-based review had the following limits: it did not read every line of `records.test.ts` or `locked-pool.ts` and did not execute the two-page scenario.

The original automatic review rejection was not retried or bypassed; the subsequent review proceeded only after the user's direct authorization.

## Delivery boundary

- The local #349 worktree contains uncommitted code, tests and evidence only; no commit, cherry-pick, push, PR edit/comment, merge, deployment, issue closure, or cleanup occurred.
- The original `.worktrees/issue-305-core-lottery-eligibility` worktree and the root worktree's pre-existing uncommitted files remain untouched.
- Do not treat this candidate as fully accepted until the P2 mismatch path is fixed and independently reviewed by both Codex and Claude on the same final fingerprint. The remote PR remains as it was.

## Follow-up remediation and final candidate

- Claude implemented the review feedback in the same isolated worktree. On a saved-record/locked-roster mismatch, `applyPersistedWinner` now returns unresolved before mutating participants, prizes or winners; it preserves the pending draw ID and shows reconciliation guidance. `clearInvalidLock` and its button are blocked while a draw is pending. Locked-pool storage failure is shown only in the pending-draw message, so same-tab recovery does not leave a stale global error banner.
- Added Playwright regressions for two pages sharing localStorage but retaining separate sessionStorage, a saved draw from P1 against B's different P2 lock, and same-tab recovery after a synthetic storage fault. The cross-tab test verifies unchanged roster/prizes/winners/lock, preserved pending ID, blocked clear/unlock/draw and safe behavior across reloads.
- Final product/test patch fingerprint: `8582abfe4f0e2f0b4a43e377b568ca84243afb2f91b91e6efa5856ce8778f296` (base HEAD `705ebbfb119c330c4d18fab03902ddd6629e88a2` plus binary diff of the six product/test files listed above). The forced-click test harness correction is included in this fingerprint.
- Verification ran in the pinned Dev Container image `vsc-devcontainer-card-shop-20261114-29415094dd8a1d1a6298fde22372f17f537c4b8445c44e68d0c61671f928834a:latest` (`sha256:f371eb001f7405b29a279d78753ca1f30ac3622cc57b98889fff7b65dcd7455b`), mounting a disposable credential-free source snapshot. Development API used synthetic `/tmp/easylottery-pr349-test-data`, the test-only admin-session-token option, and loopback `127.0.0.1:18931` only.
- Vue Vitest: passed, 35 files / 198 tests. Typecheck: passed. Production build: passed, with two existing SignalR Rollup annotation warnings. Focused Playwright: passed, 8/8, including both new regressions. `git diff --check`: passed.
- One initial E2E harness invocation stopped before Playwright because the readiness probe used `/api/health` (the API exposes `/health/ready`); the corrected run completed all eight tests. The first Playwright run then exposed a test-only issue: its forced-click simulation removed the native disabled attribute and did not restore it before reassertion. Claude corrected the harness; the final 8/8 rerun passed. Neither was an application test failure.
- Strict OpenSpec validation and decision JSON validation are being run against the final documentation/candidate now. Codex's final same-fingerprint review and the user-authorized independent Claude read-only review are pending. No commit, push, PR edit/comment, merge, deployment, issue closure, or cleanup occurred.

## Browser session-restore guard and final verification

- A subsequent independent Claude review found a P3 edge case: Chrome can restore a closed origin tab's old `sessionStorage` lock after a fresh tab has reconciled and cleared the shared pending draw. Without a durable marker that restored tab could redraw the same prize. The earlier recovery guidance alone did not prevent this.
- Claude added a localStorage ledger keyed by locked-pool version, recording every reconciled draw ID. A draw is added before UI/lock state changes; if ledger persistence fails, the draw remains pending. On restore, any locked pool missing a ledgered draw is blocked from drawing, unlocking or clearing. The E2E now simulates a restored old tab after fresh-tab recovery and verifies fail-closed behavior. Invalid ledgers also fail closed rather than forgetting prior draw IDs.
- Final product/test fingerprint: `239342f84705e304768d3156ec0fca96387d9495ecd5d5f280b4afceaa67de3a` (base HEAD `705ebbfb119c330c4d18fab03902ddd6629e88a2` plus binary diff of these eight files: `LotteryHomeView.vue`, `import-export.ts`, `import-export.test.ts`, `records.ts`, `records.test.ts`, `locked-pool.ts`, `locked-pool.test.ts`, and `core-lottery-eligibility.spec.mjs`).
- Pinned Dev Container verification on the final candidate passed: Vue Vitest 35 files / 201 tests; `npm run typecheck`; production build (two existing SignalR Rollup annotation warnings); focused Playwright `core-lottery-eligibility.spec.mjs` 8/8; OpenSpec `validate --all --strict` 50/50; `git diff --check`; decision JSON parse. The API test suite was not run because no API code changed.
- The test API ran in Development mode with a fresh synthetic `/tmp/easylottery-pr349-test-data` directory, test-only admin session token, and loopback `127.0.0.1:18931`. Only the disposable credential-free source snapshot `/private/tmp/issue305-pr349-final.wUmB0U` was mounted into the pinned image.
- Codex final review of this fingerprint is being completed. The user-authorized Claude read-only review is being repeated on these eight current paths; Claude's earlier review could not independently calculate the local SHA because its Herdr terminal lacked permission for `shasum`, but it did inspect the named working-tree diffs. No commit, push, remote PR mutation, merge, deployment, issue closure, or cleanup occurred.

## Same-origin serialization and latest verification

- A later Claude review found two medium cross-tab risks: an already-open duplicate of the same locked roster could draw after another tab resolved, and a new draw could overwrite a different shared pending record. Claude addressed both with a single origin-scoped Web Lock held from preflight through API reconciliation. Draw/reconcile/retry/unlock/clear all use the lock. Before a new draw or lock mutation, the page checks the shared pending record under the lock and adopts/reconciles that exact draw instead of replacing it. Stale-ledger checking runs again after acquiring the lock. Browsers without Web Locks fail closed and show supported-origin guidance.
- Added coverage for same-lock contention, stale same-lock copy after another tab resolves, shared-pending adoption without reload, and unsupported Web Locks. The existing different-lock mismatch, fresh-tab recovery, restored-session stale-lock, storage failure, async CSV import and regular retry cases remain covered.
- Latest final product/test fingerprint: `10494f9c89020d7af5fc92960e28c6ebdb68bd2fc47eb0127aaaed853876710d` (base HEAD `705ebbfb119c330c4d18fab03902ddd6629e88a2` plus binary diff of the same eight product/test paths listed in the previous section).
- Latest pinned Dev Container results: Vue Vitest 35 files / 204 tests passed; typecheck passed; production build passed with two existing SignalR Rollup annotation warnings; focused Playwright 11/11 passed; OpenSpec strict 50/50 passed; `git diff --check` and decision JSON validation passed. The first expanded Playwright run hit HTTP 429 on the synthetic local API's default rate limit; rerunning with `Testing__SensitivePermitLimit=1000` and `Testing__AuthenticationPermitLimit=1000` passed 11/11. These elevated limits were only for the disposable Development API.
- The disposable API used synthetic storage, test-only admin session token, and loopback `127.0.0.1:18931`. The .NET API test suite was not run because API code was unchanged. GitHub CI was not run because the branch was not pushed.
- Final Codex and user-authorized Claude read-only reviews of this latest fingerprint are in progress. No commit, push, PR edit/comment, merge, deployment, issue closure, cleanup, or changes to the root/original #305 worktrees occurred.

## Current candidate verification (2026-11-15)

- Current product/test candidate fingerprint: `88ff31c81efe64b3526d2e51253b54ce2139716f11f1e7554d0e922278b25f29` (base `705ebbfb119c330c4d18fab03902ddd6629e88a2` plus binary diff of the eight product/test paths listed above). This supersedes the earlier `10494f…` fingerprint; Claude's latest fixes had changed three of those files plus the E2E spec.
- Verification ran against `/private/tmp/issue305-pr349-current.QJ7cM4`, a credential-free tar snapshot of the current worktree, in the pinned Dev Container image ID `sha256:f371eb001f7405b29a279d78753ca1f30ac3622cc57b98889fff7b65dcd7455b`. All eight product/test files were byte-compared against the worktree after the run. API ran in Development mode with synthetic storage, test-only admin session token, loopback `127.0.0.1:18931`, and synthetic rate limits set to 1000.
- Executed frontend `npm ci`, `npm test`, `npm run typecheck`, `npm run build -- --outDir ../EasyLotteryWasm/wwwroot/vue`; E2E `npm ci`, `PLAYWRIGHT_BROWSERS_PATH=/workspace/.cache/pw-browsers npx playwright install --with-deps chromium`; `BASE_URL=http://127.0.0.1:18931 PLAYWRIGHT_BROWSERS_PATH=/workspace/.cache/pw-browsers npm test -- core-lottery-eligibility.spec.mjs`; and `npx --yes @fission-ai/openspec@1.7.0 validate --all --strict`.
- Results: Vue/Vitest 35 files / 205 tests passed; typecheck passed; production build passed with the two existing SignalR Rollup annotation warnings; focused Playwright 12/12 passed; strict OpenSpec 50/50 passed. `git diff --check` and decision JSON parse passed. .NET API tests and GitHub CI were not run (API unchanged; branch not pushed).
- Latest Codex review: in progress. Claude review: user authorized it; first attempts exposed that Herdr's default shell omits Claude OAuth and that `--bare` deliberately disables OAuth/keychain. A final normal OAuth-mode Claude CLI review with only Read/Grep/Glob tools is running in Herdr terminal `t39`; no code-write or shell tools are available. Do not report the previous `10494f…` Claude review as applying to this current fingerprint.
- Candidate remains local-only, uncommitted and unpushed. Remote PR #349 and all other worktrees remain untouched.

## Final candidate after P3 review fixes (2026-11-15)

- Claude's independent read-only review of fingerprint `88ff31c8…` found no P1/P2 and raised two P3s: an in-flight YouTube poll could append a participant during selected-draw recovery, and corrupt-ledger copy implied an admin repair path that does not exist. Codex planned a bounded response; Claude implemented the changes in the same isolated worktree. See `.agents/plans/2026-11-15-pr349-review-remediation.md`.
- Selected draws now stop chat capture before any asynchronous save/reconciliation path, including the final pending-write failure and selected-result exception paths. Pre-selection failures keep capture active. Corrupt-ledger messages preserve fail-closed behavior and now explicitly say self-service repair is unavailable, preserve the browser profile/state and contact the maintainer. Added two synthetic E2E cases for these paths. The residual limitation is that corrupt ledgers still have no automatic/in-app repair; operators must keep the state intact and contact the maintainer.
- Final product/test fingerprint: `b7fbc24e206f2761e5c700dac42583df699abe252d93707725bb8878e53b4ede` (base `705ebbfb119c330c4d18fab03902ddd6629e88a2` plus binary diff of the same eight product/test paths listed earlier).
- The current candidate was copied to `/private/tmp/issue305-pr349-reviewed.knf3os`, a credential-free source snapshot. All eight product/test paths were byte-compared after verification. Verification used pinned container image ID `sha256:f371eb001f7405b29a279d78753ca1f30ac3622cc57b98889fff7b65dcd7455b`, synthetic Development API/storage, test-only session token, loopback `127.0.0.1:18931`, and synthetic test rate limits of 1000.
- Executed: frontend `npm ci --silent --cache /workspace/.cache/npm-vue-cache`, `npm test`, `npm run typecheck`, `npm run build -- --outDir ../EasyLotteryWasm/wwwroot/vue`; E2E `npm ci --silent --cache /workspace/.cache/npm-e2e-cache`, `PLAYWRIGHT_BROWSERS_PATH=/workspace/.cache/pw-browsers npx playwright install --with-deps chromium`; `BASE_URL=http://127.0.0.1:18931 PLAYWRIGHT_BROWSERS_PATH=/workspace/.cache/pw-browsers npm test -- core-lottery-eligibility.spec.mjs`; and `npx --yes @fission-ai/openspec@1.7.0 validate --all --strict`.
- Results: Vue/Vitest 35 files / 205 tests passed; typecheck passed; production build passed with two existing SignalR Rollup annotation warnings; focused Playwright 14/14 passed (including the two P3 regressions); OpenSpec strict 50/50 passed; `git diff --check` and decision JSON parse passed. .NET API tests were not run because API code is unchanged. GitHub CI was not run because no push was authorized.
- Codex source review and test inspection completed with no additional P1/P2 findings. The user-authorized final Claude read-only review of this same fingerprint was dispatched in Herdr terminal `t3d` and is pending. Its prior `88ff31…` review does not substitute for this final review.
- No commit, push, PR edit/comment, merge, deployment, issue closure, worktree cleanup or changes to root/original #305 worktrees occurred. Existing PR #349 remains unchanged remotely. Push/Ready PR update remains blocked on missing explicit remote-delivery authorization.

## Final independent Claude review — fingerprint b7fbc2…

- Claude reviewed the current contents of all eight named paths with Read/Grep/Glob only. It reported no P1, P2 or P3 findings and confirmed both prior P3 findings were addressed. It did not run tests or shell commands and could not independently recompute the fingerprint or compare against base; Codex independently computed the fingerprint, verified all eight files byte-match the tested snapshot, and ran the checks above.
- Claude noted a possible CSV formula-injection concern in the generic `escapeCsv` behavior, likely pre-existing and not confirmed as introduced by this candidate because no base diff was available to the reviewer. The changed export adds `ChannelScope`; source UUID columns themselves are constrained. Codex confirmed generic export already handles untrusted fields through the same `escapeCsv`; this is recorded as a separate follow-up risk, not a verified regression in #349.
- The corrupt-ledger behavior remains intentionally fail-closed. There is no in-app repair mechanism; preserve the browser profile and contact the system maintainer. This is now explicit in both error paths and covered by E2E.
- After updating the OpenSpec task status with final review evidence, strict OpenSpec validation was rerun against that exact `tasks.md`: `npx --yes @fission-ai/openspec@1.7.0 validate --all --strict` — 50 passed, 0 failed.

## Publication (2026-11-17)

- Derek explicitly authorized push and a follow-up PR. Commit `284fb297cfb92173f42b2a373523cf53e5882333` was pushed on `codex/issue-305-integrate-20261114`; product/test fingerprint remained unchanged at `b7fbc24e206f2761e5c700dac42583df699abe252d93707725bb8878e53b4ede`.
- Created Ready-for-review PR #355: https://github.com/KonohaDerek/EasyLottery/pull/355, based on `main` `d1b7da035ba1095bcf8b746654c3b4e943253d85`. Original PR #349 is merged; issue #305 is closed. The follow-up uses no issue-closing keyword and does not change either state.
- GitHub reported PR #355 `MERGEABLE`, `UNSTABLE` while its `build-and-test` check was in progress; GitGuardian Security Checks had passed. Workflow run: `44106169056`. No merge/deployment occurred.
