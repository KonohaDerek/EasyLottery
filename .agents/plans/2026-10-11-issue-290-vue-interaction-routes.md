# Issue #290：Vue 互動管理與 OBS 路由

- Status: In Progress
- Source: #290 under #269 and #252; blocks #270
- OpenSpec: openspec/changes/migrate-vue-interaction-routes
- Branch/worktree: feat/vue-interaction-routes / .worktrees/vue-interaction-routes
- Base: origin/main f52225908f0fcb0a762d8581c87bd572b7702b55
- Board: Project #1 #290 moved Backlog → In progress.

## Bounded tasks

1. Inventory existing route behavior and REST/SignalR contracts. Completed by read-only survey and direct source inspection.
2. Implemented both authenticated management routes and REST helpers/tests in the issue worktree; retained the existing admin header and one-time code protocol.
3. Implemented all four OBS modes and the SignalR lifecycle, including reconnect rejoin, refreshed snapshots, stale-token, missing-token and missing-round states.
4. Integrated six routes, AdminLayout/Blazor navigation, exact API host fallbacks, all six /legacy aliases, router/Vitest coverage and focused Playwright cases.
5. Vue tests/typecheck/build, interaction API/Hub/identity-link endpoint tests, strict OpenSpec and all three focused Playwright UI cases passed. Playwright covered direct Vue routes, admin REST, identity-link safe errors, legacy rollback alias, four OBS modes, lowercase vote counts, reconnect plus a post-rejoin event, and hidden notice state while paused. A real ego-browser check also exercised round creation/start, REST vote ingestion, live SignalR rendering, all four OBS modes, safe identity-code errors, invalid credentials and missing rounds. Native OBS validation remains user-owned. Delivery record, push and PR are in progress; do not merge or deploy.

An isolated management-slice commit was reviewed but not cherry-picked because it overlapped the integrated implementation; the issue branch contains one canonical set of interaction views and clients.

## Validation plan

- Vue: npm test, npm run typecheck, npm run build.
- .NET: interaction-filtered EasyLottery API tests and any changed host tests.
- Browser: focused Playwright cases pass (3/3), covering login gating, Vue routes/refresh, REST actions/errors, safe identity-link errors and success, live/reconnect/rejoin/empty/error OBS states, reduced motion and legacy aliases. Native OBS remains user-owned.
- Repo: openspec validate --all --strict; git diff --check; review token handling and reconnect behavior.
- Native OBS: user-owned verification for supported resolutions, transparency, reload/reconnect and read-token failure; do not mark verified by Chromium.

## Scope and rollback

No Domain/protocol, payment, production, or data changes. The identity-link endpoint now binds the existing JSON `{ code }` body already sent by the Blazor client, rather than changing the request contract or placing the code in a URL. Rollback by restoring original Blazor route directives and removing Vue route/fallback entries; preserve all /legacy aliases until parity and the user's OBS validation gate are satisfied.
