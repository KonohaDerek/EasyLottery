# Design

## Route and authorization

- Add `/roulette/preview/:id` to the authenticated Vue routes.
- Add an exact API-host fallback for `/roulette/preview/{id}`.
- Add `/legacy/roulette/preview/{Id:int}` to the existing Blazor relay.

## Preview handoff

- Parse the route id as a positive safe integer and GET `/api/roulette-templates/{id}` through the existing session-token API client.
- If the template is missing or has an empty public id, replace the route with `/roulette` without issuing an OBS session.
- POST `/api/obs-sessions` with `resourceKind: "roulette"`, the template public id, and `read`/`control` scopes.
- Reuse `buildRouletteTestUrl` to navigate to the existing OBS route; do not put the token in the path, query, or persistent storage.
- Show API failures accessibly with a retry action.

## Verification and rollback

- Playwright intercepts API calls and asserts authorization, scopes, destination, fragment-only token handoff, and error paths.
- Verify anonymous navigation makes no API calls and the Blazor alias still hands off to the overlay.
- Roll back by removing the Vue route/fallback while keeping the explicit legacy alias.
