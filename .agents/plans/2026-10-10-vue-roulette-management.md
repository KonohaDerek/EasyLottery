# Vue Roulette management slice

## Source and goal

- Parent issue: #268, under migration epic #252.
- The activity-results and Donate-activity management routes are already Vue routes on `main`.
- Migrate only the Roulette template management screen at `/roulette` to Vue.

## Scope

- Admin-session-protected Vue page using the existing Roulette template API.
- Preserve list, create/edit/delete, segment-count editing, publication state, default seeding, duplication, import/export, image/audio asset selection, and test-OBS launch.
- Keep `/legacy/roulette` as the Blazor rollback route; leave editor, preview, OBS, draw, and SignalR routes unchanged.
- Reuse existing Vue API, session, asset-picker and dialog patterns; no new dependencies or backend changes.

## Non-goals and risks

- No PokeBox, public roulette experience, OBS rendering/SignalR, draw/probability algorithm, API/Domain, production, or Blazorise changes.
- Main risks are preserving the persisted segment shape, admin header, built-in template protection, and keeping the OBS session token in the URL fragment.
- Browser tests intercept every template mutation and OBS-session request; no real templates or OBS sessions are changed.
- Rollback: remove the Vue `/roulette` fallback/route/handoff and keep the original Blazor page available at `/legacy/roulette`.

## Verification

- Vitest for template cloning, default segments, segment-count boundaries, and test-OBS URL handling.
- Vue typecheck/build; Playwright for auth, route handoff, CRUD, failure/retry, built-in protection, and legacy rollback.
- OpenSpec strict validation and `git diff --check`.
- No .NET tests are required unless implementation touches API/Domain code (which is out of scope).

## Execution

- Main agent only; the slice is cohesive and has no safe non-overlapping implementation task to delegate.
- TypeSafe/Jev is unavailable because `TYPESAFE_API_KEY` is not configured; manual structured decision recorded in `.agents/decisions/`.
- Dedicated worktree: `.worktrees/vue-roulette-management-slice`, branch `feat/vue-roulette-management-slice`, based on merged PR #276.
