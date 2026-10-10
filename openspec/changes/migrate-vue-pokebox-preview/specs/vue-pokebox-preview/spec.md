# Vue PokeBox preview relay

## ADDED Requirements

### Requirement: Authenticated route

- The application MUST serve `/pokebox/preview/{id}` through the Vue shell and require the existing admin session before loading template data.
- Anonymous navigation MUST redirect to login without requesting `/api/poke-templates/{id}` or `/api/obs-sessions`.

#### Scenario: Anonymous visitor requests a preview

- WHEN a visitor without an admin session opens `/pokebox/preview/{id}`
- THEN the application redirects to login and makes no template or OBS-session API request.

### Requirement: Token handoff

- For a valid template, the preview MUST use the existing PokeTemplate API and OBS-session API contracts.
- The generated OBS session MUST be scoped to the template's PokeBox public id with `read` and `control` scopes.
- The preview MUST navigate to the existing `/obs/pokebox/{publicId}?controls=1` overlay and MUST place the session token only in the URL fragment.
- The preview MUST NOT persist or log the session token.

#### Scenario: Valid template is handed off to OBS

- WHEN an authenticated visitor opens the preview for a valid template
- THEN the application loads the template, creates a PokeBox `read`/`control` OBS session, and navigates to the existing overlay with the token only in the fragment.

### Requirement: Invalid and failed loads

- A missing template, invalid id, or template without a public id MUST return to `/pokebox` without issuing an OBS session.
- A transient API failure MUST be shown accessibly and MUST allow retry.

#### Scenario: Template is missing or has no public id

- WHEN the template request returns 404 or the returned template has an empty public id
- THEN the application returns to `/pokebox` without requesting an OBS session.

#### Scenario: API request fails

- WHEN template or OBS-session creation fails for a reason other than a missing template
- THEN the application shows an accessible error and offers a retry action.

### Requirement: Rollback

- The Blazor preview relay MUST remain reachable at `/legacy/pokebox/preview/{id}` while the Vue route is active.

#### Scenario: Operator uses the rollback path

- WHEN an operator opens `/legacy/pokebox/preview/{id}`
- THEN the existing Blazor relay creates the existing OBS handoff.
