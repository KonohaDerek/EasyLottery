# Current work state

- Task: fix-live-site-findings-317-323
- Status: Testing
- Source: user requested direct repairs to reported #317–#323 findings.
- Worktree: EasyLottery-live-fixes-20261112 / codex/live-fixes-317-323, base 43f69b802bb12e3c1bda2b4636f892bbc8d775fd plus imported E2E commit ab5af48.
- Route: official stable v0.0.7 verified. Actual Claude Opus 5.5 plan session c1580a06-9b21-4003-acfe-5243161da38d; Codex implements. Jev key absent.
- Parallel scopes: public pages, OBS errors, routing, Passkey privacy. Isolated worktrees; coordinator integrates and reruns final checks.
- Baseline: API147/147 and Vue build passed; local route200 HTML and email409/403 differences reproduced.
- Focused Passkey privacy/security API checks:36/36 passed. Worker validation recorded separately.
- Original claude/live-site-e2e-20261112 worktree, report, traces and screenshots remain untouched. Production not contacted or changed.
- Reviews: round0; authoritative ledger outside reviewed tree at EasyLottery-review-evidence/live-fixes-20261112.
- Flags: verification_complete: false; branch_pushed: false; pull_request_created: false.
