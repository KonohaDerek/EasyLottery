# Vue public frontend

## ADDED Requirements

### Requirement: Public Vue routes

The system SHALL serve Vue for `/about`, `/privacy-policy`, and `/login` while leaving the root and non-migrated routes on the existing Blazor host.

#### Scenario: Anonymous visitor opens About

- **WHEN** an anonymous visitor opens `/about`
- **THEN** the Vue public layout renders one `h1` for About, public navigation, and no authenticated settings request

#### Scenario: Anonymous visitor opens Privacy Policy

- **WHEN** an anonymous visitor opens `/privacy-policy`
- **THEN** the Vue public layout renders the deployment-controlled privacy content without admin navigation or settings request

### Requirement: Client-side login validation

The login view SHALL start with an empty email and reject blank or malformed email before requesting Passkey options.

#### Scenario: Empty email

- **WHEN** the visitor submits Login without an email
- **THEN** an accessible alert is shown and `/api/auth/passkey/options` is not requested

#### Scenario: Malformed email

- **WHEN** the visitor submits Login with a malformed email
- **THEN** an accessible alert is shown and `/api/auth/passkey/options` is not requested

### Requirement: Existing Passkey protocol

The Vue login client SHALL use the existing options/verify endpoints and preserve the returned session token format.

#### Scenario: Passkey login succeeds

- **WHEN** WebAuthn returns a credential and the verify endpoint returns a session token
- **THEN** the token is stored in session storage and the browser returns to the current root admin entry point
