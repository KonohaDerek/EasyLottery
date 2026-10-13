# Issue #289 — migrate remaining routes to Vue

- Status: Verification
- Source: GitHub issue #289; GitHub Project #1 transitions verified: Backlog → Ready → In progress.
- OpenSpec: `openspec/changes/migrate-vue-remaining-routes`
- Branch/worktree: `feat/vue-remaining-routes` / `.worktrees/vue-remaining-routes`
- Base: merged `origin/main` at `429532346eff0556d7ec7c3b60c92a573b55cf59` (includes PR #292).
- Dev Container: shared project checkout mounted at `/workspaces/EasyLottery/.worktrees/vue-remaining-routes`; package restore completed.

## Scope

Migrate `/market`, `/system/platform-connections`, and `/activity/overtime` into the existing Vue app. Preserve existing backend protocols and REST/SignalR contracts, harden the existing overtime-feed PUT against stale-snapshot loss of payment events, add the routes to the authenticated admin navigation, and keep the Blazor pages available for rollback.

## Non-goals

- No endpoint, DTO, authorization-scope, SignalR, Domain, payment-processing, provider, or YouTube protocol changes; no production configuration or real external-provider calls.
- No Blazor host/dependency removal and no production cutover.
- No claiming native OBS verification; that remains user-owned under #269.

## Implementation and validation

1. Inventory the three legacy pages, existing Vue APIs/layouts, endpoint authorization, and SPA/legacy fallback behavior.
2. Define the three route requirements, compatibility boundaries, tests, rollback, and observability in the OpenSpec change.
3. Implement the Vue routes and admin navigation using existing patterns, and add explicit Blazor `/legacy/...` rollback aliases:
   - Marketplace lists only published built-ins; installing duplicates into an editable copy and marks only that new copy `Published`, matching the legacy flow without overwriting the source or another installed copy. If publication fails after duplication, retain the new copy ID and retry publication without duplicating again.
   - Platform connections use the existing admin-session endpoint and `If-Match` ETag; masked-secret sentinels must be preserved when unchanged and never displayed as credential values.
   - Overtime settings use the existing admin settings endpoint, preserve live session-owned state and a newer server planned end when the administrator leaves that field untouched, generate/copy the existing scoped OBS preview URL, and keep demo/clear actions explicit.
   - The existing overtime-feed PUT preserves concurrent SuperChat and third-party payment events omitted from a stale snapshot; explicit DELETE still clears the feed.
4. Add unit/API tests plus focused Playwright coverage for direct navigation/refresh, login/authorization, safe empty/error states, duplicate publication recovery, contextual action names, secret/ETag conflicts, planned-end concurrency, and explicit demo/clear actions. Test feed preservation against JSON and SQLite, including a full mixed-payment feed. Tests must never contact real providers.
5. Run Vue tests/typecheck/build, focused Playwright, relevant .NET endpoint tests, strict OpenSpec validation, `git diff --check`, and a second-pass review. Record exact results in `.agents/verification/`.
6. Push a dedicated branch and create a Ready-for-review PR linked to #289/#252. Move #289 to In review and verify the card. Do not merge or deploy.

## Risks and safeguards

- Platform credentials are write-only from the browser's perspective; do not reveal the backend's unchanged-secret sentinel. Preserve it exactly when saving an unmodified credential and use the latest ETag.
- Marketplace install is copy-based and intentionally creates a new editable version on each install/reinstall.
- Overtime settings can be edited while OBS owns a live session or a payment extends the planned end; merge/preserve current server state unless the admin explicitly edited that field.
- ECPay demo writes are whole-feed updates; preserve concurrently appended payment callbacks within the existing atomic feed mutation, while keeping the existing endpoint contract and DELETE-clear behavior.
- Demo and clear actions mutate the overtime feed, so require explicit user action and keep them out of route-load effects.
- Keep existing Blazor routes available; rollback is route/fallback restoration only, with no data migration.

## Delegation

Beauvoir performed a read-only investigation of platform connection endpoints/DTOs/secret masking/ETag behavior and API-test gaps. Mencius completed an independent read-only review; it reported no code findings and requested full-feed mixed-payment boundary tests, which were added for both JSON and SQLite repositories. Main agent owns all implementation, shared route/menu integration, testing, push, and PR operations.
