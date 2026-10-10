# Migrate Roulette preview relay to Vue

## Why

The Roulette manager is now served by Vue, but the direct `/roulette/preview/{id}` OBS test relay still depends on Blazor WebAssembly. Migrating this one relay completes the Roulette preview path without changing the existing OBS source.

## What changes

- Serve `/roulette/preview/{id}` from the Vue shell and protect it with the existing admin-session guard.
- Use the existing Roulette template and OBS-session APIs, then navigate to `/obs/roulette/{publicId}` with the token only in the fragment.
- Retain the Blazor relay at `/legacy/roulette/preview/{id}` for rollback.

## What does not change

- No OBS overlay rendering, spin/result lifecycle, SignalR, REST/Domain contracts, production deployment, or Blazorise changes.

## Acceptance criteria

- Anonymous visits redirect to login without template or OBS-session requests.
- A valid template creates one scoped OBS session and navigates with the token only in the fragment.
- Invalid/missing templates return to `/roulette`; transient API failures are accessible and retryable.
- The legacy Blazor relay remains reachable under `/legacy/roulette/preview/{id}`.
- Vue, Playwright, .NET, OpenSpec strict, and diff checks pass.

## Risks and rollback

- The relay issues a short-lived OBS control token, so it remains authenticated and only issues a token after loading a valid template.
- Roll back by removing the Vue route/fallback; the explicit legacy alias remains available.
- No production cutover or persistent data changes are part of this change.
