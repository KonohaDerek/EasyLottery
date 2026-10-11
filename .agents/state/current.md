# Current work state

- Task ID: `vue-core-lottery-2026-10-11`
- Status: In Progress; implementing issue #288 after PR #287 merged.
- Source: #288, under epic #252; Phase 5 #270 is blocked on route parity and remains in Backlog. User asked to continue the migration, open direct Ready-for-review PRs, and personally perform native OBS verification. User also requires polished imagery consistent with the animation style whenever image work is needed; this core lottery page currently has no image or animation assets to migrate.
- Scope: Replace the Blazor `/` core lottery workspace with authenticated Vue while preserving participant/prize workflows, draw behavior, JSON/CSV formats, YouTube member import and level weighting, chat filters/cooldown, and accepted SuperChat-to-overtime-feed behavior. Keep the Blazor fallback/legacy route available. Add only the minimum server API surface needed to keep YouTube credentials server-side and use an ETag-protected DrawingRules resource.
- Non-goals: Do not remove EasyLotteryWasm/Blazorise, change payment/domain draw contracts, modify production configuration, run production deployment, perform native OBS verification, or merge.
- OpenSpec change: `migrate-vue-core-lottery`
- Branch: `feat/vue-core-lottery-workspace`
- Worktree: `.worktrees/vue-blazor-retirement`, based on `origin/main` at `fc1ddfcd4567e4bcf808c4a6210e511aa1a25f31`.
- Board: Project #1 has #288 In progress, #252/#269 In progress (WIP 3/3); #270 Backlog. New route follow-ups #289/#290 remain Backlog.
- Decision: Jev 1.13.0 classified this as strong (confidence 0.84); parallel implementation safe 0.25; elevated risk 0.94; browser E2E required 0.77; additional authorization for source changes/Ready PR not needed 0.19. See `.agents/decisions/2026-10-11-vue-core-lottery.json`.
- Plan: Implement domain-independent draw/import/filter logic with Vitest first; add protected YouTube adapter API and DrawingRules settings resource with API tests; build Vue view, route/fallback, and focused Playwright coverage; run Dev Container suite and strict OpenSpec; review diff; push and open Ready-for-review PR linked to #288/#252/#270; synchronize board to In review.
- Verification targets: Vue tests/typecheck/build; API and Domain tests; focused route/E2E tests with YouTube mocked; health smoke and `openspec validate --all --strict`; diff/security review. Native OBS is not relevant to this core page and remains a user-owned separate check for #269.
- Rollback: Remove the Vue `/` route/fallback and restore the original Blazor route; additive API endpoints can remain without changing existing clients. No data migration.
- Observability: Do not log API keys, session tokens, live-chat content, or raw YouTube URLs. Expose recoverable UI errors for import, settings, and provider failures.
- verification_complete: true
- branch_pushed: false
- pull_request_created: false
- Updated: 2026-10-11
