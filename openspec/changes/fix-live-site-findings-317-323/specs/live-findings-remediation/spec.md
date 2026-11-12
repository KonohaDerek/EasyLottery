## ADDED Requirements

### Requirement: Meaningful missing-route responses
The application SHALL return non-HTML 404 for unmatched API/hub requests and a Vue HTTP404 document for unknown navigation while preserving explicit legacy routes and settings API clients.

#### Scenario: Unknown API request
- **WHEN** a client requests an unknown API or hub endpoint
- **THEN** status is404 and the body is not an HTML shell

#### Scenario: Settings browser navigation
- **WHEN** a browser navigates directly to /settings
- **THEN** it redirects into Vue navigation instead of a bare401 document

### Requirement: Login responses resist administrator enumeration
The application SHALL provide equivalent option shapes and uniform failed login verification responses for administrator and other valid emails, except registration-specific messages at allowed sources.

#### Scenario: Decoy options do not disrupt a real login
- **WHEN** an untrusted caller requests options for another valid email
- **THEN** status and option shape do not identify the administrator and the real pending login remains unchanged

### Requirement: Public pages remain accessible and truthful
The application SHALL provide readable mobile navigation, one semantic login heading, associated input errors, bounded capability wording, favicon and robots assets.

#### Scenario: Narrow viewport with invalid email
- **WHEN** login is opened at375px and invalid email is submitted
- **THEN** labels and title remain readable without horizontal overflow and input references its error

### Requirement: Fatal OBS errors are actionable
The application SHALL display recovery information when no valid overlay content exists, while preserving valid idle transparency.

#### Scenario: Missing Donate activity
- **WHEN** an authenticated scoped request cannot find an activity
- **THEN** the overlay explains the missing activity and how to obtain a valid URL
