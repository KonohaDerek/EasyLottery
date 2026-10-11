## ADDED Requirements

### Requirement: Authenticated Vue lottery workspace with rollback route
The system MUST serve the core lottery workspace from the authenticated Vue route at `/` and keep the existing Blazor page reachable at `/legacy` during migration.

#### Scenario: Direct navigation to the lottery workspace
- **WHEN** an administrator opens or refreshes `/`
- **THEN** the Vue lottery workspace loads after authentication
- **AND** an unauthenticated visitor is redirected to the login route

#### Scenario: Roll back to the existing Blazor lottery page
- **WHEN** an administrator opens `/legacy`
- **THEN** the existing Blazor lottery page remains available

### Requirement: Preserve weighted participant identity in imports and exports
The system MUST preserve a participant's lottery group identity across JSON and CSV export/import while assigning fresh internal participant IDs.

#### Scenario: Round-trip multiple tickets for one weighted member
- **WHEN** multiple participant rows share one lottery group ID and are exported then imported
- **THEN** every imported ticket keeps the shared lottery group ID
- **AND** each imported participant receives a new internal ID
- **AND** one draw marks all tickets in that lottery group as winners together

#### Scenario: Import a legacy participant file without a lottery group ID
- **WHEN** a valid legacy JSON or CSV participant file omits the lottery group ID
- **THEN** each imported participant receives a usable group ID without rejecting the file

### Requirement: Protect DrawingRules updates against missing or concurrent versions
The system MUST require a specific `If-Match` ETag when updating DrawingRules and MUST compare and persist SQLite settings changes atomically.

#### Scenario: Update without a specific version
- **WHEN** an authenticated administrator updates DrawingRules without `If-Match` or with `If-Match: *`
- **THEN** the API returns HTTP 428
- **AND** the stored rules remain unchanged

#### Scenario: Concurrent updates from the same version
- **WHEN** two requests update SQLite-backed settings using the same ETag
- **THEN** only one request succeeds
- **AND** the other request receives HTTP 409 without overwriting the successful update

#### Scenario: A stale legacy settings save follows a DrawingRules update
- **WHEN** a legacy client saves a full configuration snapshot loaded before a DrawingRules update, without changing DrawingRules itself
- **THEN** the newer DrawingRules values remain stored

#### Scenario: A stale SQLite compatibility settings request races with a resource update
- **WHEN** a `/settings` write uses an ETag from before a DrawingRules update
- **THEN** the write receives HTTP 409
- **AND** the resource update remains stored because ETag validation and persistence are atomic

### Requirement: Append SuperChat events without replacing the overtime feed
The system MUST allow a control-scoped overtime session to append one SuperChat event atomically, preserve existing overtime events, and deduplicate by YouTube message ID.

#### Scenario: Add a new SuperChat
- **WHEN** an authorized control-scoped request posts a valid SuperChat event
- **THEN** the event is added to the overtime feed without replacing existing events
- **AND** connected overtime clients are notified of the change

#### Scenario: Retry the same SuperChat
- **WHEN** the same YouTube message ID is posted again
- **THEN** the feed contains only one event for that message
- **AND** existing manual and payment events remain unchanged

#### Scenario: A stale full-feed write races with a SuperChat append
- **WHEN** a client writes a feed snapshot read before a SuperChat was appended
- **THEN** the server preserves the current SuperChat while applying the snapshot when the 30-event cap allows
- **AND** if the cap is exceeded, a new event in the incoming snapshot is retained and the oldest existing event is evicted first
- **AND** the mutation is atomic for both JSON and SQLite storage providers

#### Scenario: Append while the feed is full
- **WHEN** a new SuperChat is appended to a feed containing 30 events
- **THEN** the new SuperChat remains in the feed
- **AND** the oldest existing event is evicted to retain the 30-event limit
