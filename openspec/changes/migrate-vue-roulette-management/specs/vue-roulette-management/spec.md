# vue-roulette-management Specification

## ADDED Requirements

### Requirement: Authenticated Vue Roulette management

The Vue application MUST render `/roulette` only for a valid admin session and MUST use the existing Roulette template REST API with the shared session-token header. The API contract and template data shape MUST remain unchanged.

#### Scenario: Unauthenticated visitor opens Roulette management

- **WHEN** a visitor without a valid admin token opens `/roulette`
- **THEN** the router redirects to login with a safe return path
- **AND** no Roulette template API request is sent

#### Scenario: Admin navigates from Blazor or opens the route directly

- **WHEN** an admin opens `/roulette` directly or follows its Blazor navigation entry
- **THEN** the Vue shell renders the management page
- **AND** requests include `X-EasyLottery-Session-Token`
- **AND** the original Blazor management page remains reachable at `/legacy/roulette`

### Requirement: Preserve Roulette template management behavior

The Vue management page MUST preserve list, create, edit, delete, duplicate, publication status, default seeding, import/export, segment configuration and existing asset selection behavior. Editing MUST NOT mutate the displayed template until saved, and built-in templates MUST NOT offer deletion.

#### Scenario: Admin edits segment count and saves

- **WHEN** an admin changes a template's segment count to 6, 8, 12, or 24 and saves
- **THEN** segments are ordered by index, truncated or filled with the existing default values, and sent using the existing template API shape
- **AND** unsaved edits canceled from the dialog leave the displayed template unchanged

#### Scenario: Template API fails to load

- **WHEN** the list request fails
- **THEN** the page displays an accessible error and retry action
- **AND** it does not present the failure as an empty template list

### Requirement: Safe test OBS handoff

The management page MUST continue to launch the existing test OBS route without migrating OBS rendering or SignalR. The short-lived OBS token MUST NOT appear in URL path or query parameters.

#### Scenario: Admin opens test OBS

- **WHEN** an admin opens test OBS for a published template
- **THEN** the existing OBS session API issues the token and the existing overlay route opens with `controls=1`
- **AND** the token is passed only in the URL fragment
