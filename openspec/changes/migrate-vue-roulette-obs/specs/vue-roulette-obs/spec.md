## ADDED Requirements

### Requirement: Vue serves the existing Roulette OBS route

The API host MUST serve the existing `/obs/roulette/{publicId}` browser-source URL through Vue. The overlay MUST keep the existing OBS session token scope and MUST NOT require an administrator login session.

#### Scenario: OBS URL loads a Roulette template

- **WHEN** a valid read-scoped OBS session opens `/obs/roulette/{publicId}`
- **THEN** Vue loads that public template through the existing API and renders the existing Roulette OBS experience with a transparent background.

#### Scenario: Control URL enables controls

- **WHEN** the same route is opened with `?controls=1` and a read/control-scoped token
- **THEN** the existing test controls and accessible live-connection status are available.

### Requirement: OBS session token handoff stays private

The Vue shell MUST consume `sessionToken` from the existing query or fragment handoff, store it in session storage when available (or in memory for the current page when storage is blocked), and remove it from the address bar before API/hub work. The `controls` query parameter MUST remain intact.

#### Scenario: Fragment token is consumed

- **WHEN** an OBS URL contains `#sessionToken=...`
- **THEN** the token is available to the API client and hub Join, and the visible URL no longer contains the token.

#### Scenario: Browser storage is unavailable

- **WHEN** session storage rejects access while an OBS URL supplies a token
- **THEN** the URL is still cleaned, Vue starts without an uncaught error, and API/hub requests can use the in-memory token for that page load.

### Requirement: Live Draw protocol remains compatible

The Vue overlay MUST join the existing `/hubs/live-draw` hub through `JoinSession`, handle `LiveDrawStateChanged`, and use the existing state and spin REST APIs. It MUST NOT change server contracts or compute draws in the browser.

#### Scenario: Control spin is broadcast to the OBS source

- **WHEN** a control page starts a spin
- **THEN** the server-owned live-draw state drives countdown, rotation and result rendering on the read-only OBS source.

### Requirement: Live Draw recovery remains authoritative

After SignalR reconnects, the overlay MUST rejoin the Roulette session and fetch the latest server state. A new connection MUST accept its snapshot even if the server revision counter reset. Within the same connection generation, older revisions MUST NOT replace newer state. If automatic retries end, the overlay MUST offer manual reconnect that starts, rejoins and refreshes the session.

#### Scenario: Connection recovers

- **WHEN** SignalR disconnects and reconnects
- **THEN** control mode announces the connection state, re-joins the same Roulette group, retrieves the latest snapshot and resumes receiving future states.

#### Scenario: Automatic retries are exhausted

- **WHEN** SignalR closes after exhausting its automatic reconnect attempts
- **THEN** the overlay keeps the latest rendered content, announces the disconnected state, and exposes a manual reconnect action that starts, re-joins, and refreshes the session.

### Requirement: Audio cues do not block visual state

The overlay MUST render live state without waiting for audio playback permission.

#### Scenario: Result audio waits for a user gesture

- **WHEN** a state snapshot contains a countdown, spin, or result while browser audio is suspended
- **THEN** the visual state renders immediately without waiting for audio permission.

#### Scenario: OBS session is stale

- **WHEN** the session is rejected or expired
- **THEN** control mode exposes an accessible recoverable error directing the operator to reopen the OBS preview; the standard overlay remains free of diagnostic copy.

### Requirement: Blazor remains a rollback path

The existing Blazor Roulette OBS page MUST remain directly available at `/legacy/obs/roulette/{publicId}` until full Phase 4 parity is verified.

#### Scenario: Operator opens legacy route

- **WHEN** an operator opens the legacy URL
- **THEN** the existing Blazor overlay loads with its current session and draw behavior.
