# Migrate PokeBox preview relay to Vue

## Why

The PokeBox template-management route is now Vue, but the directly addressable `/pokebox/preview/{id}` test-OBS relay still depends on the Blazor WebAssembly host. Migrating the relay completes this narrow admin preview path without changing the existing OBS source.

## What changes

- Serve the exact `/pokebox/preview/{id}` route from the Vue shell and protect it with the existing admin-session guard.
- Use the existing PokeTemplate and OBS-session REST APIs to issue a PokeBox control session and hand off to the existing `/obs/pokebox/{publicId}` overlay.
- Retain the Blazor relay at `/legacy/pokebox/preview/{id}` for rollback.

## What does not change

- No OBS overlay rendering, draw/reveal lifecycle, result notifications, SignalR, REST/Domain contract, production deployment, or Blazorise changes.

## Acceptance criteria

- Anonymous visits go to login without requesting template or OBS-session data.
- A valid template causes one authenticated OBS-session request and navigates to the existing overlay URL; the token appears only in the fragment.
- A missing/invalid template returns to `/pokebox`; transient API failures are visible and retryable.
- The legacy Blazor relay remains available under `/legacy/pokebox/preview/{id}`.
- Vitest, typecheck/build, Playwright, .NET build/tests, OpenSpec strict validation, and diff checks pass.

## Risks and rollback

- The route can issue an OBS control token, so it remains admin-authenticated and only requests a token after a valid template is loaded.
- Rollback by removing the Vue fallback/route and restoring the Blazor path; the explicit legacy alias remains available throughout.
- No production cutover or persistent data changes are part of this change.
