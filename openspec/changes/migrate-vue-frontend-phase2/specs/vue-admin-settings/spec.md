# Vue admin settings

## ADDED Requirements

### Requirement: Authenticated admin route guard

The system SHALL redirect unauthenticated visitors from Phase 2 admin routes to `/login` before issuing authenticated settings requests.

#### Scenario: Missing session token

- **WHEN** an anonymous visitor opens `/system/payment`
- **THEN** the Vue router redirects to `/login` and no `/api/settings` request is sent

#### Scenario: Existing session token

- **WHEN** an authenticated visitor opens `/system/payment`
- **THEN** the Vue admin layout loads and sends the session token only in the request header

### Requirement: Settings resource persistence

The system SHALL load and save Phase 2 settings through the existing resource APIs and preserve the returned ETag.

#### Scenario: Visual style save succeeds

- **WHEN** an authenticated visitor changes the visual style and saves
- **THEN** the client sends the last ETag with the existing resource payload and renders the server response

#### Scenario: Settings conflict

- **WHEN** the API rejects a save with a concurrency conflict
- **THEN** the client shows a reload/error message and does not claim the save succeeded

### Requirement: Administrative operations

The system SHALL preserve the existing authorization and behavior for Passkey management, backups, and OBS assets.

#### Scenario: Passkey management page

- **WHEN** an authenticated administrator opens `/system/access`
- **THEN** registered devices load and add/remove operations use the existing admin Passkey endpoints

#### Scenario: Backup restore

- **WHEN** an authenticated administrator restores a listed backup
- **THEN** the existing backup restore endpoint is called and the list is reloaded

#### Scenario: Asset upload

- **WHEN** an authenticated administrator uploads an OBS asset
- **THEN** the existing multipart asset endpoint is used and the asset list refreshes
