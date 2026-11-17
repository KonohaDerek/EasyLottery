## ADDED Requirements

### Requirement: Viewer email-code sign-in
The Card Hub MUST let a viewer sign in with an email address and a one-time code delivered by email, and MUST issue a viewer token that is distinct from admin credentials.

#### Scenario: Valid code signs the viewer in
- **WHEN** a viewer submits the code sent to their email within 10 minutes
- **THEN** the system issues a viewer access token and refresh token for that email's viewer account

#### Scenario: Repeated wrong codes are throttled
- **WHEN** five wrong codes are submitted for one email
- **THEN** the code is invalidated and further attempts are rate-limited

#### Scenario: Viewer token cannot call streamer APIs
- **WHEN** a request to any streamer-site or Hub streamer-admin endpoint carries a viewer token
- **THEN** the system responds 401 or 403 and performs no admin action

### Requirement: Viewer data export
The Hub MUST provide an authenticated JSON export of data associated with the requesting viewer, set `Cache-Control: no-store`, and audit each export request.

#### Scenario: Viewer exports their data
- **WHEN** an authenticated viewer requests an export
- **THEN** it includes their account and consent records, groups, card ownership, draws and redeemed-code history; it excludes other viewers' data and fraud signals, and explains that irreversibly anonymized records are omitted

#### Scenario: Export is isolated to the requester
- **WHEN** a viewer requests an export
- **THEN** no other viewer's email, account identifier, collection or trade counterpart details are disclosed

### Requirement: Paid entry age gate
Viewers aged 13–17 MAY register, collect and redeem gifted cards, but MUST NOT initiate or trigger a paid card-pack draw in Wave 1. Wave 1 MUST NOT claim guardian consent without a verified guardian-consent flow.

#### Scenario: Minor attempts a paid draw
- **WHEN** a viewer declares an age bracket of 13–17 at a paid card-pack entry point
- **THEN** the paid draw is unavailable and no payment-triggered draw request is sent

### Requirement: Card ownership and grouping
The system MUST record the current owner of every card instance and MUST let a viewer organize owned instances into named groups.

#### Scenario: Viewer creates a group
- **WHEN** a viewer creates a group and adds owned card instances to it
- **THEN** the collection lists those instances under that group

#### Scenario: Viewer deletes their account
- **WHEN** a viewer deletes their account
- **THEN** their email, groups and sessions are removed and their card instances become retired
