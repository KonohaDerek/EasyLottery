# Settings resources

## ADDED Requirements

### Requirement: Section resources
The system SHALL expose independent GET and PUT resources for OBS layout, sound cues, visual style, payment configuration, and overtime overlay settings.

#### Scenario: Update one section
- **WHEN** an authenticated administrator sends a valid PUT to one section resource
- **THEN** only that section is changed and the response includes an ETag.

### Requirement: Optimistic concurrency
The system SHALL reject a section update when a supplied `If-Match` value is stale.

#### Scenario: Stale update
- **WHEN** an administrator sends a stale `If-Match`
- **THEN** the API returns HTTP 409 and does not overwrite the current section.

### Requirement: Secret protection
The payment resource SHALL mask stored secrets and preserve them when the client sends the unchanged mask.

#### Scenario: Round-trip masked credentials
- **WHEN** a client reads and writes payment settings without changing masked credentials
- **THEN** the persisted credentials remain unchanged and are never returned in clear text.

### Requirement: Compatibility endpoint
The legacy `/settings` YAML endpoint SHALL be marked deprecated and documented as a migration/import compatibility endpoint.

#### Scenario: Legacy client migration
- **WHEN** a client calls the legacy `/settings` endpoint
- **THEN** the response identifies the endpoint as deprecated and points to the section resources.
