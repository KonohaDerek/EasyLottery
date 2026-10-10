# Design

## Route and authorization

- Add a Vue route for `/pokebox/preview/:id` with `requiresAuth: true`.
- Add a constrained API-host fallback for `/pokebox/preview/{id:int}` so the Vue router receives direct navigation.
- Add `/legacy/pokebox/preview/{Id:int}` to the existing Blazor relay for rollback.

## Preview handoff

- Parse the route id as a positive safe integer, then GET `/api/poke-templates/{id}` with the existing session-token client.
- If the template is absent or its `publicId` is empty, replace the route with `/pokebox`, matching current behavior.
- POST `/api/obs-sessions` with `resourceKind: "pokebox"`, the template public id, and `read`/`control` scopes.
- Reuse `buildPokeTestUrl` to navigate to `/obs/pokebox/{publicId}?controls=1#sessionToken=...`; never put the token in the path, query, or persisted storage.
- On recoverable API errors, show an alert and retry action; do not mint repeated tokens unless the user retries.

## Verification and rollback

- Playwright intercepts the template and OBS-session calls and asserts the session header, exact redirect, and fragment-only token.
- Verify anonymous navigation makes no API calls and the legacy alias remains served by Blazor.
- Roll back by removing the one fallback and Vue route; retain the legacy alias and existing overlay unchanged.
