# Vue PokeBox management slice

## Source and goal

- Parent issue: #268, under migration epic #252.
- The public Vue shell, admin settings, activity results, Donate activity management, and Roulette template management are already present on `main`.
- Migrate only the PokeBox template management page at `/pokebox` to Vue.

## Scope

- Add an admin-session-protected Vue management page using the existing PokeBox template REST API.
- Preserve list, create/edit/delete, duplicate, seed defaults, import/export, publication state, grid/cell editing, image/audio asset selection, and test-OBS handoff.
- Preserve `/pokebox?edit={id}` and `/pokebox/editor/{id}` edit-entry behavior.
- Keep `/legacy/pokebox` as a Blazor rollback route. Keep `/pokebox/preview/{id}`, `/obs/pokebox/{publicId}`, public draw, and SignalR behavior in Blazor.
- Reuse the Vue session guard, API client, asset picker, and dialog conventions; add no dependencies or API/Domain changes.

## Non-goals and risks

- No public PokeBox interaction, OBS rendering, preview, SignalR, result lifecycle, Roulette, API/Domain, production, or Blazorise migration.
- Main risks are preserving the persisted PokeTemplate/PokeCell JSON shape, enum values, reveal-state fields, edit query alias, and keeping the OBS token out of query/path.
- Browser tests intercept all template writes and OBS-session issuance; no real templates or live sessions are changed.
- Rollback: remove the exact `/pokebox` Vue fallback/route/handoff and retain the original Blazor page at `/legacy/pokebox`.

## Verification

- Vitest for default template shape, deep clone isolation, grid resizing/order, and OBS URL fragment handling.
- Vue tests, typecheck, production build; Playwright for auth, route handoff, editor entry aliases, CRUD, API error/retry, and legacy rollback.
- .NET solution build/tests because API host fallback and Razor route alias are touched.
- OpenSpec strict validation and `git diff --check`.

## Execution

- Main agent only; PokeBox page, data helpers, routing, and E2E coverage form one cohesive slice with overlapping files.
- TypeSafe/Jev is unavailable because `TYPESAFE_API_KEY` is not configured; manual structured decision is recorded in `.agents/decisions/`.
- Dedicated worktree: `.worktrees/vue-pokebox-management-slice`, branch `feat/vue-pokebox-management-slice`, based on merged PR #277 (`c4d00f9b5ac3e4b280fe4b4d3b4aeaaa010ef4aa`).
