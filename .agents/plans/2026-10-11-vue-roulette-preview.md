# Vue Roulette preview relay

## Source and goal

- Parent issue: #268, under migration epic #252.
- PR #279 has merged the Vue PokeBox preview relay; `/roulette/preview/{id}` still uses Blazor.
- Move only the authenticated Roulette preview relay to Vue while retaining the existing `/obs/roulette/{publicId}` overlay.

## Scope

- Add a `requiresAuth` Vue route and API-host fallback for `/roulette/preview/{id}`.
- Load the template through the existing authenticated Roulette API, issue the same scoped OBS session, and navigate with the existing `buildRouletteTestUrl` helper.
- Keep invalid/missing templates returning to `/roulette`, expose retry for transient API failures, and preserve `/legacy/roulette/preview/{id}`.
- Cover route registration, token handoff, errors, and rollback alias.

## Non-goals and risks

- No changes to `/obs/roulette`, spin behavior, draw results, SignalR, Domain/API contracts, production, or Blazorise.
- Main risk is exposing an admin-only relay or leaking an OBS token in the path/query/storage; route guard and browser assertions cover these boundaries.
- Rollback by removing the Vue route/fallback and using the explicit Blazor alias; no persisted data is changed.

## Verification

- Vitest route/auth coverage; Vue unit tests, typecheck, and build.
- Playwright auth, invalid/missing template, error/retry, fragment-only token, and legacy route coverage.
- .NET 10 Release build/tests, OpenSpec strict validation, and `git diff --check`.

## Execution

- Main agent only; no independent file-disjoint task. A second-pass review is required before PR.
- TypeSafe/Jev credential and Superpowers skill files are unavailable; manual decision and repo local workflow apply.
- Dedicated worktree: `.worktrees/vue-roulette-preview-slice`; branch `feat/vue-roulette-preview-slice`; based on merged PR #279 (`f2b798b1c1bb9eaf80d59cb76407ba8a73b00a57`).
