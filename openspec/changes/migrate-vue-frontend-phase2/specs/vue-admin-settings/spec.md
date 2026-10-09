# Vue admin settings

## ADDED Requirements

### Requirement: Authenticated admin route guard

The system SHALL redirect visitors with a missing, malformed, expired, or non-admin session token from Phase 2 admin routes to `/login` before issuing authenticated settings requests. The API remains responsible for validating token signatures and permissions.

#### Scenario: Missing session token

- **WHEN** an anonymous visitor opens `/system/payment`
- **THEN** the Vue router redirects to `/login` and no `/api/settings` request is sent

#### Scenario: Existing session token

- **WHEN** an authenticated visitor opens `/system/payment`
- **THEN** the Vue admin layout loads and sends the session token only in the request header

#### Scenario: OBS token cannot enter an admin route

- **WHEN** a visitor opens `/system/payment` with an unexpired OBS-scoped token
- **THEN** the Vue router redirects to `/login` and no `/api/settings` request is sent

#### Scenario: Passkey login without a safe redirect

- **WHEN** an administrator completes Passkey login without a `redirect` query, with a cross-origin redirect, or with a same-origin path beginning with `//`
- **THEN** Vue opens the Passkey management route instead of the unimplemented root route

### Requirement: Legacy navigation hands off to Vue-owned routes

Blazor navigation links for routes migrated to Vue MUST perform a full-page navigation so the Blazor router does not render its legacy page for those URLs.

#### Scenario: Navigate from Blazor to a migrated admin route

- **WHEN** a visitor clicks a Blazor navigation link for a Phase 2 settings page
- **THEN** the browser loads the Vue route and renders its page

### Requirement: Settings resource persistence

The system SHALL load and save Phase 2 settings through the existing resource APIs and preserve the returned ETag.

#### Scenario: Visual style save succeeds

- **WHEN** an authenticated visitor changes the visual style and saves
- **THEN** the client sends the last ETag with the existing resource payload and renders the server response

#### Scenario: Settings conflict

- **WHEN** the API rejects a save with a concurrency conflict
- **THEN** the client shows a reload/error message and does not claim the save succeeded

#### Scenario: Payment provider environments remain independent

- **WHEN** an administrator edits and saves provider credentials and enable flags in testing or production
- **THEN** each environment retains its own values after reload, and masked secrets are preserved unless replaced

#### Scenario: Failed settings load cannot be saved

- **WHEN** the initial payment, YouTube, audit, or preset settings request fails
- **THEN** the editable form remains unavailable and no update is sent until a successful retry supplies the current ETag

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
