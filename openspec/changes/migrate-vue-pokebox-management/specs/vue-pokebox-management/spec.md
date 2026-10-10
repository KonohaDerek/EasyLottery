# vue-pokebox-management Specification

## ADDED Requirements

### Requirement: Authenticated Vue PokeBox management

The Vue application MUST render `/pokebox` only for a valid admin session and MUST use the existing PokeBox template REST API with the shared session-token header. API contracts and persisted template/cell shapes MUST remain unchanged.

#### Scenario: Unauthenticated visitor opens PokeBox management

- **WHEN** a visitor without a valid admin token opens `/pokebox`
- **THEN** the router redirects to login with a safe return path
- **AND** no PokeBox template API request is sent

#### Scenario: Admin opens the management route or legacy edit entry

- **WHEN** an admin opens `/pokebox`, `/pokebox?edit={id}`, or the legacy `/pokebox/editor/{id}` entry
- **THEN** the Vue shell renders the PokeBox management page and requested editor state
- **AND** management requests include `X-EasyLottery-Session-Token`
- **AND** the Blazor management page remains reachable at `/legacy/pokebox`

### Requirement: Preserve PokeBox template management behavior

The Vue management page MUST preserve list, create, edit, delete, duplicate, publication state, default seeding, import/export, grid configuration, cell settings, and existing asset selection behavior. Editing MUST NOT mutate the displayed template until saved, and built-in templates MUST NOT offer deletion.

#### Scenario: Admin changes the grid size and saves

- **WHEN** an admin applies a row/column change within the existing 1-to-10 limits
- **THEN** cells are ordered by index, truncated or filled with existing default cell values, and sent using the existing template API shape
- **AND** canceling unsaved edits leaves the displayed template unchanged

#### Scenario: Template API fails to load

- **WHEN** the list request fails
- **THEN** the page displays an accessible error and retry action
- **AND** it does not present the failure as an empty template list

### Requirement: Safe test OBS handoff

The management page MUST continue to open the existing PokeBox OBS route without migrating preview, OBS rendering, public draw, or SignalR. The short-lived OBS token MUST NOT appear in URL path or query parameters.

#### Scenario: Admin opens test OBS

- **WHEN** an admin opens test OBS for a published template
- **THEN** the existing OBS session API issues a token and the existing overlay route opens with `controls=1`
- **AND** the token is passed only in the URL fragment
