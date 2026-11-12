## ADDED Requirements

### Requirement: Viewer email-code sign-in
The system MUST let a viewer sign in with an email address and a one-time code delivered by email, and MUST issue a viewer token that is distinct from admin credentials.

#### Scenario: Valid code signs the viewer in
- **WHEN** a viewer submits the code sent to their email within 10 minutes
- **THEN** the system issues a viewer access token and refresh token for that email's viewer account

#### Scenario: Repeated wrong codes are throttled
- **WHEN** five wrong codes are submitted for one email
- **THEN** the code is invalidated and further attempts are rate-limited

#### Scenario: Viewer token cannot call admin APIs
- **WHEN** a request to any admin endpoint carries a viewer token
- **THEN** the system responds 401 or 403 and performs no admin action

### Requirement: Card ownership and grouping
The system MUST record the current owner of every card instance and MUST let a viewer organize owned instances into named groups.

#### Scenario: Viewer creates a group
- **WHEN** a viewer creates a group and adds owned card instances to it
- **THEN** the collection lists those instances under that group

#### Scenario: Viewer deletes their account
- **WHEN** a viewer deletes their account
- **THEN** their email, groups and sessions are removed and their card instances become retired
