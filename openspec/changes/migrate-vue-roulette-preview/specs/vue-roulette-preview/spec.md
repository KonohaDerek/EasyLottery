## ADDED Requirements

### Requirement: Authenticated route

- The application MUST serve `/roulette/preview/{id}` through the Vue shell and require the existing admin session before loading template data.
- Anonymous navigation MUST redirect to login without requesting template or OBS-session data.

#### Scenario: Anonymous visitor requests a preview

- WHEN a visitor without an admin session opens `/roulette/preview/{id}`
- THEN the application redirects to login and makes no template or OBS-session API request.

### Requirement: Token handoff

- For a valid template, the preview MUST use the existing Roulette template and OBS-session API contracts.
- The generated OBS session MUST be scoped to that template's Roulette public id with `read` and `control` scopes.
- The preview MUST navigate to `/obs/roulette/{publicId}?controls=1` and MUST place the session token only in the URL fragment.
- The preview MUST NOT persist or log the session token.

#### Scenario: Valid template is handed off to OBS

- WHEN an authenticated visitor opens the preview for a valid template
- THEN the application loads the template, creates a Roulette `read`/`control` OBS session, and navigates to the existing overlay with the token only in the fragment.

### Requirement: Invalid and failed loads

- An invalid id, missing template, or template without a public id MUST return to `/roulette` without issuing an OBS session.
- A transient API failure MUST be shown accessibly and MUST allow retry.

#### Scenario: Template is missing or has no public id

- WHEN the template request returns 404 or the returned template has an empty public id
- THEN the application returns to `/roulette` without requesting an OBS session.

#### Scenario: API request fails

- WHEN template or OBS-session creation fails for a reason other than a missing template
- THEN the application shows an accessible error and offers a retry action.

### Requirement: Rollback

- The Blazor preview relay MUST remain reachable at `/legacy/roulette/preview/{id}` while the Vue route is active.

#### Scenario: Operator uses the rollback path

- WHEN an operator opens `/legacy/roulette/preview/{id}`
- THEN the existing Blazor relay creates the existing OBS handoff.
