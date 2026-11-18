# Spec Delta

## Purpose

This capability lets an administrator configure supported platform credentials and channel scope through the existing server-authorized API without exposing stored secrets or silently overwriting concurrent changes.

## ADDED Requirements

### Requirement: Read supported platform connection status
The system SHALL display YouTube, Twitch, and Discord connection status using the existing administrator-authenticated endpoint and SHALL distinguish unconfigured credentials, an unavailable adapter, an unverified connection, a verified failure, and a verified connection.

#### Scenario: Load connection status
- **WHEN** an authenticated administrator opens `/system/platform-connections`
- **THEN** the system loads the existing connection list and its ETag
- **AND** displays the supported platform status and channel scope
- **AND** does not treat stored credentials or a configuration flag as evidence of connectivity
- **AND** reports connected or failed only when the platform adapter supplies runtime health evidence
- **AND** does not expose any stored credential value or secret sentinel as visible text

#### Scenario: A configured platform has no implemented adapter
- **WHEN** an administrator views a platform with stored credentials but no implemented runtime adapter
- **THEN** the system reports that the adapter is not implemented
- **AND** does not report the platform as connected or as having a verified connection failure

#### Scenario: A platform adapter has not verified its connection
- **WHEN** credentials are stored and an implemented adapter has no current health evidence
- **THEN** the system reports the connection as unverified
- **AND** displays an explicit text label as well as a visual status cue

#### Scenario: A platform adapter reports runtime health
- **WHEN** an implemented adapter reports verified connected or failed health
- **THEN** the system displays the corresponding connected or failed state
- **AND** keeps each state distinct from configured, unavailable, and unverified states

#### Scenario: Connection read is unauthorized or fails
- **WHEN** the connection endpoint rejects the session or returns an error
- **THEN** the system shows a recoverable error and offers retry
- **AND** does not enable a save using uninitialized or stale form data
- **AND** does not keep displaying a previously loaded connection status

#### Scenario: Status after a save
- **WHEN** a save succeeds
- **THEN** the system displays the saved platform's status from the server response
- **AND** shows an unknown status rather than the pre-save status if the response omits that platform

### Requirement: Update credentials without losing unchanged secrets
The system SHALL submit connection changes through the existing endpoint with the current ETag and SHALL preserve a stored credential when its masked unchanged-secret value is returned without user edits.

#### Scenario: Save a platform connection
- **WHEN** an administrator saves a platform's credentials or channel scope
- **THEN** the system sends only the existing update fields and current `If-Match` ETag
- **AND** an unchanged masked credential is preserved by the server
- **AND** the system updates its ETag from the successful response

#### Scenario: Clear or replace a credential
- **WHEN** an administrator explicitly enters a replacement credential or clears the credential field
- **THEN** the system submits that explicit value using the existing update contract
- **AND** does not render the stored-secret sentinel as the input value

#### Scenario: Concurrent connection update
- **WHEN** the server rejects a save because the ETag is stale
- **THEN** the system reports the conflict and requires reload before another save
- **AND** does not claim that the stale update succeeded

### Requirement: Keep connection management administrator-only
The system SHALL protect the route with the existing administrator login flow and SHALL rely on the server's existing administrator-session authorization for all reads and writes.

#### Scenario: Unauthenticated direct navigation
- **WHEN** a visitor without an administrator session opens `/system/platform-connections`
- **THEN** the system redirects the visitor to the existing login flow
- **AND** does not issue a connection request before authentication
