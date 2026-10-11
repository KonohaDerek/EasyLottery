# Overtime OBS Vue migration plan

## Context and global constraints

- Request source: issue #284, under Phase 4 #269 and migration epic #252; #282 and #283 have merged.
- Scope: replace only `/obs/overtime` presentation with Vue, preserving the REST/YAML projection, `/api/overtime-feed`, `/hubs/overtime`, token scopes, display behavior, and a Blazor rollback alias.
- Non-goals: backend contract/auth changes, overtime domain/rule changes, payment/feed generation changes, Donate OBS, production cutover, and removal of Blazor.
- Never use `PUT /api/settings/overtime` for an OBS control token. Continue using the existing scoped `PUT /settings` compatibility projection; record its 2026-12-31 sunset as a follow-up risk.
- Never treat `?controls=1` as authorization. Let the existing API reject read-only writes and show the error.
- Main agent owns the tightly coupled implementation. The read-only survey by Confucius is complete. Any independent final reviewer must use a separate isolated worktree per `.agents/workflow.yaml`.
- Preserve local `.devcontainer/local/data` symlink and all user data; stage only explicit task files.
- Validation runs in the existing project Dev Container. No deployment or merge.
- Rollback: retain `/legacy/obs/overtime`; the Vue-specific fallback can be removed independently.

## Task 1: Add tested overtime API and session helpers

Files: `src/EasyLotteryWeb/src/overtime/overlay.ts`, `overlay.test.ts`, `api.ts`, `api.test.ts`.

Interfaces: Task 1 exports typed overtime settings/feed operations and deterministic time/session helpers. Task 2 consumes them from the Vue view. No server-side change is permitted.

1. Write `overlay.test.ts` first for idle/running/paused/ended transitions, remaining time and progress boundaries, pause extension, event visibility, and generation/revision protection against stale snapshots. Run from `src/EasyLotteryWeb`: `npm test -- src/overtime/overlay.test.ts`.
   Expected: tests fail because the module does not yet exist.
2. Implement only the functions needed to satisfy those tests, matching `OvertimeSessionTimeCalculator` semantics and the legacy page behavior.
3. Write `api.test.ts` first for parsing/updating `OvertimeOverlay` in the existing YAML projection without dropping sibling fields; GET/PUT `/settings`; GET/PUT `/api/overtime-feed`; preserving token headers; and rejecting API errors without logging credentials. Run: `npm test -- src/overtime/api.test.ts`.
   Expected: tests fail because the API module does not yet exist.
4. Implement the typed API adapter with the installed `yaml` package, existing `apiText`/`apiRequest` helpers, maximum 30 feed items, and existing source enum values.
5. Verify: `npm test -- src/overtime` and `npm run typecheck`.
6. Commit: `feat: add overtime OBS Vue client`.

## Task 2: Build the Vue overlay and preserve both routes

Files: `src/EasyLotteryWeb/src/views/OvertimeObsView.vue`, `src/EasyLotteryWeb/src/overtime/overtime.css`, `src/EasyLotteryWeb/src/overtime/realtime.ts`, `src/EasyLotteryWeb/src/overtime/realtime.test.ts`, `src/EasyLotteryWeb/src/routes.ts`, `src/EasyLotteryWeb/src/router.test.ts`, `src/EasyLotteryAPI/Program.cs`, `src/EasyLotteryWasm/Pages/Overtime/OvertimeOverlay.razor`, `tests/e2e/overtime-obs.spec.mjs`.

Interfaces: consumes Task 1 exports. `/obs/overtime` is an exact Vue route/fallback; `/legacy/obs/overtime` remains the old Blazor page. The shared startup token bootstrap scrubs credentials from URL while preserving `controls=1`.

1. Add/adjust route tests and the Playwright scenario before UI implementation. The E2E must save and restore the original admin settings and the exact pre-test feed in `finally`; it must not clear existing user data permanently. Cover initial token scrubbing, controls hidden without query, read-only control rejection, start/pause/resume/end, test feed notification, transparent background, and the legacy alias. Run the focused tests and observe the expected failure before implementing the route/view.
2. Implement the Vue view using existing API/session-token/SignalR helpers. Refresh authoritative YAML/feed after hub events and reconnect; re-join with the session token. Poll at low frequency even while connected, coalesce overlapping snapshots, and patch the latest settings projection instead of saving the loaded object. Use effective settings/feed end time for expiry and retry failed completion writes. Cover reconnect/coalescing with focused Vitest tests; use Playwright for live hub propagation rather than relying on a browser-offline event to deterministically trigger SignalR reconnect. Keep transparent rendering, `motion=reduce`, countdown/progress, manual control feed markers, support notification, amount/reward presentation, completion animation, connection/error state, and existing one-second display updates.
3. Add precise `MapFallbackToFile("/obs/overtime", "vue/index.html")`, the public OBS route metadata, and a second Blazor `@page` alias `/legacy/obs/overtime`; do not broaden SPA fallback.
4. Verify: focused Playwright overtime test with test data restored; Vue test suite, typecheck and production build; relevant .NET tests. Do not claim a live network-break reconnect E2E unless one is made deterministic and passes.
5. Inspect the diff, then commit: `feat: migrate overtime OBS overlay to Vue`.

## Task 3: Final acceptance, review, and PR handoff

1. Validate OpenSpec with `openspec validate migrate-vue-overtime-obs --strict` and reconcile its tasks against the implementation.
2. Run the complete Vue suite, typecheck, production build, relevant .NET Release tests, and focused overtime Playwright test in the project Dev Container. Record exact commands/results in `.agents/verification/2026-10-11-vue-overtime-obs.md`.
3. Update `.agents/state/current.md` with evidence, branch/PR state, and any manual OBS Browser Source smoke-test limitation.
4. Perform a fresh whole-branch review in a separate isolated worktree, following `.agents/workflow.yaml`; fix any material findings and rerun affected plus full required verification.
5. Check staged paths and diff; do not include local environment symlinks/data. Push this feature branch and create a Ready-for-review PR referencing `Fixes #284` and related #269/#252 without closing either parent. Attach the PR artifact to this task and update Project #1 status for #284 to `Needs Review`.

## Acceptance criteria

- The Vue route loads without the Blazor WebAssembly runtime; legacy `/legacy/obs/overtime` still resolves to the original component.
- Read/control session tokens are carried only in the existing session header/hub Join call; URL credentials are scrubbed, and no control-token call uses admin-only `/api/settings/overtime`.
- Existing OBS overlay state, controls, event display, feed semantics, transparent background, hub refresh/reconnect, and error states are preserved.
- Unit, E2E, typecheck/build, .NET, and OpenSpec verification results are recorded; browser E2E restores settings and feed even on failure.
- The PR is Ready for review; no merge or production change is performed.
