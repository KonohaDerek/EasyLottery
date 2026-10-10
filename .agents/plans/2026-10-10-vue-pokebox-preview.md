# Vue PokeBox preview relay

## Source and goal

- Parent issue: #268, under migration epic #252.
- PR #278 (PokeBox template management) is merged. The template admin already offers a Vue test-OBS button; the direct `/pokebox/preview/{id}` relay still runs in Blazor.
- Migrate only that relay to Vue, retaining the existing OBS overlay route and its server-owned draw/SignalR behavior.

## Scope

- Add a `requiresAuth` Vue route and exact API-host fallback for `/pokebox/preview/{id}`.
- Load the template through the existing authenticated PokeTemplate API, issue the same scoped OBS session through `/api/obs-sessions`, and navigate to the existing overlay with the token only in the fragment.
- Keep missing/invalid templates returning to `/pokebox`, expose retry for transient API failures, and preserve a Blazor rollback URL at `/legacy/pokebox/preview/{id}`.
- Add route unit coverage and Playwright coverage with template/session calls intercepted.

## Non-goals and risks

- No changes to `/obs/pokebox/{publicId}`, public draw API, animation, result recording, SignalR, domain data, production, or Blazorise.
- Main risk is accidentally exposing the admin-only relay or placing an OBS token in a path/query/log; route guard and browser assertions cover those boundaries.
- Rollback: remove the Vue route/fallback and use the explicit legacy alias; no data migration is involved.

## Verification

- Vitest for authenticated route registration/guard expectations.
- Vue tests, typecheck, production build; Playwright for auth, not-found, error/retry, and token-fragment handoff.
- .NET 10 Release build/tests because API fallback and Razor alias are touched.
- OpenSpec strict validation and `git diff --check`.

## Execution

- Main agent only; implementation is a narrow, coupled route and authorization handoff.
- TypeSafe/Jev and Superpowers are unavailable in this environment; manual decision and repository local workflow apply.
- Dedicated worktree: `.worktrees/vue-pokebox-preview-slice`, branch `feat/vue-pokebox-preview-slice`, based on merged PR #278 (`f23e824c328495378b11d080fa7cab2090bae8ce`).
