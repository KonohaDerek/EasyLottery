# Issue #290：Vue 互動管理與 OBS 路由

- Status: In Progress
- Source: #290 under #269 and #252; blocks #270
- OpenSpec: openspec/changes/migrate-vue-interaction-routes
- Branch/worktree: feat/vue-interaction-routes / .worktrees/vue-interaction-routes
- Base: origin/main f52225908f0fcb0a762d8581c87bd572b7702b55
- Board: Project #1 #290 moved Backlog → In progress.

## Bounded tasks

1. Inventory existing route behavior and REST/SignalR contracts. Completed by read-only survey and direct source inspection.
2. Implement two authenticated management routes and isolated REST helpers/tests. Parallel candidate; owns only src/EasyLotteryWeb/src/interactions/admin/**.
3. Implement four OBS routes, interaction SignalR lifecycle and tests. Parallel candidate; owns only src/EasyLotteryWeb/src/interactions/obs/**.
4. Integrate route table, AdminLayout navigation, API host exact fallbacks, Blazor legacy aliases, router and Playwright tests. Main agent owns shared integration files after interfaces are fixed.
5. Run Dev Container verification, inspect diff/security/compatibility, record results, push branch and open a Ready-for-review PR. Do not merge, deploy or close Phase 4 #269.

Tasks 2 and 3 use isolated worktrees and disjoint file paths. Shared route registration, host fallback, and Playwright integration remain serialized on the main issue branch.

## Validation plan

- Vue: npm test, npm run typecheck, npm run build.
- .NET: interaction-filtered EasyLottery API tests and any changed host tests.
- Browser: focused Playwright for login gating, routes/refresh, REST actions/errors, identity-code errors, live/empty/error OBS states, and legacy aliases.
- Repo: openspec validate --all --strict; git diff --check; review token handling and reconnect behavior.
- Native OBS: user-owned verification for supported resolutions, transparency, reload/reconnect and read-token failure; do not mark verified by Chromium.

## Scope and rollback

No backend/domain/protocol, payment, production, or data changes. Rollback by restoring original Blazor route directives and removing Vue route/fallback entries; preserve all /legacy aliases until parity and the user's OBS validation gate are satisfied.
