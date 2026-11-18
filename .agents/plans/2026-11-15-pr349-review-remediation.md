# PR #349 review remediation plan

- Task: Continue existing EasyLottery PR #349 / Issue #305 locally; do not create another PR or modify GitHub.
- Target worktree/branch: `.worktrees/issue-305-integrate` / `codex/issue-305-integrate-20261114`, starting at `705ebbfb119c330c4d18fab03902ddd6629e88a2`.
- Isolated code candidate sources: original follow-up commits `d2f21ad97525fed2533698020a4a23cb0e377fe7` (pending draw blocks unlock; adds E2E) and `d295c206807ee852d5715641a0be20d0cf23222e` (strips storage-only `phase` before retry). Port these scoped changes into the PR worktree; do not change the original #305 worktree.
- Requirements: resolve PR #349's three review findings: (1) retain pending draw state if `sessionStorage` cannot persist the advanced locked pool; (2) recheck the lock after `await file.text()` for YouTube member CSV import; (3) preserve interaction provenance (`source`) and `identity.channelScope` across participant JSON and CSV import/export. Add synthetic regressions for these and the two original follow-ups above.
- Non-goals: No remote writes (push, PR edits/comments, merge), deployment, production calls/data, issue closure, cleanup, unrelated worktree edits, credential/keychain/login inspection, or company data access.
- Plan: Claude implements through a new Herdr Core terminal rooted at this worktree. It may inspect the two source commits read-only. Codex integrates/reviews and runs the focused Vitest suite, frontend typecheck/build, and core-lottery Playwright spec in an isolated Dev Container using synthetic data and loopback-only API.
- Routing evidence: GitHub Releases API for `KonohaDerek/EasyLottery` lists `v0.0.7` as published 2026-10-22, `draft=false`, `prerelease=false`. The stable-release baseline routes Claude planning/Codex implementation. Derek explicitly instructed this task to use Herdr Core to assign implementation to Claude; this task-specific role override is recorded and honored. No model override is made; use Claude Code's configured default.
- Jev: no request made because no explicitly approved, known no-charge health/auth check is available in this task. Manual structured routing fallback; serial execution because these changes share the locked-pool, participant serialization, and E2E contracts.
- Risks: browser storage failure and reload can otherwise re-enable a previously awarded ticket; async file read can bypass the lock transition; dropping source/scope breaks stable deduplication and permits reimported winners. Preserve legacy import compatibility and existing JSON/CSV field spellings.
- Acceptance: regressions fail before/fix after; focused frontend tests, typecheck, production build, and Playwright pass on one frozen candidate tree; Codex and an independent Claude reviewer inspect the same full candidate SHA/fingerprint; `git diff --check` passes. Record exact commands and any limits. Local only; the existing PR remains open and unmodified remotely.
- Current outcome: reviews found a P2 lock-mismatch recovery gap and successive P3 UI/session-restore and same-origin duplicate-draw risks. Claude implemented staged persistence, a per-lock reconciled-draw ledger, stale-tab blocking, and same-origin Web Locks for draw/reconcile/retry/unlock/clear, with pending-record adoption and fail-closed behavior when unsupported. Final pinned-Dev-Container verification passed (Vue 35/204, typecheck/build, Playwright 11/11, OpenSpec 50/50); final same-fingerprint Codex/Claude reviews are in progress. See `.agents/verification/2026-11-15-pr349-review-remediation.md`.
- Rollback: revert only the remediation commits in this worktree; no storage/schema migration or production rollback is required.
- Observability: keep pending draw locked with actionable UI error when pool storage fails; no server telemetry or participant data logging is added.

## Latest Claude P3 follow-up — Codex plan

- Review was read-only on the same eight current paths; Claude could not independently reproduce the fingerprint because its tool allowlist omitted shell. It found no P1/P2 and reported two P3s.
- **Fix in scope:** stop active chat capture before either selected-draw recovery path awaits server reconciliation (drawLottery throws after selection, or persisting the result pending record fails). This prevents a chat poll already in flight from appending new participants while the saved winner is applied from the pre-draw roster. Add a synthetic Playwright regression proving the in-flight poll cannot append a participant that is later discarded. Claude implements; Codex inspects and verifies.
- **Known fail-closed limitation:** a malformed local reconciled-draw ledger intentionally blocks draw/unlock/clear because prior draw IDs cannot be inferred from corrupt storage. Do not provide an unverified DevTools repair recipe. Correct error copy to say self-service repair is unavailable and direct the operator to preserve the browser state and contact the maintainer; record this as a remaining P3 risk unless a complete, server-history-backed repair flow is separately designed.
- No API change, server/history scan, commit, push, PR edit, merge, deploy or cleanup is in scope. Re-run Vue/Vitest, typecheck/build, focused Playwright and strict OpenSpec on any changed candidate, then re-review the exact new fingerprint.

## Delivery plan update (2026-11-17)

- GitHub now reports original PR #349 as merged at `d1b7da035ba1095bcf8b746654c3b4e943253d85`; issue #305 is closed. Remote `main` tree is byte-identical to the candidate base tree at `705ebbfb119c330c4d18fab03902ddd6629e88a2`. The old work branch is not present on origin.
- Derek explicitly authorized publishing with “發 PR啊” on 2026-11-17. Commit the verified changes to the 13 task-related source, test and evidence files and push the branch, then create a separate Ready-for-review follow-up PR targeting `main`. Reference #349 and #305 without a closing keyword because the original issue is already completed. Do not reopen #349 or #305, merge or deploy.
- Proposed title: `fix: harden core lottery draw recovery after #349`.

## Delivery completed (2026-11-17)

- Commit `284fb297cfb92173f42b2a373523cf53e5882333` was created on `codex/issue-305-integrate-20261114` and pushed to origin after Derek authorized “發 PR啊”. The product/test fingerprint remains `b7fbc24e206f2761e5c700dac42583df699abe252d93707725bb8878e53b4ede`.
- Created Ready-for-review follow-up PR #355: https://github.com/KonohaDerek/EasyLottery/pull/355, targeting `main` at `d1b7da035ba1095bcf8b746654c3b4e943253d85`. Original PR #349 is merged and issue #305 is already closed; neither was reopened or closed again.
- No merge or deployment. PR CI run `44106169056` was in progress at last observation.
