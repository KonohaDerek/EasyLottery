## ADDED Requirements

### Requirement: Anonymous information pages use a public shell

The system SHALL render the About and Privacy Policy routes without the authenticated administration navigation or authenticated settings requests when no administrator session exists.

#### Scenario: Anonymous visitor opens the privacy policy

- **WHEN** an unauthenticated visitor navigates to `/privacy-policy`
- **THEN** the page displays the privacy policy in a public content shell
- **AND** the administration sidebar is not rendered
- **AND** the browser does not request `/settings`

#### Scenario: Anonymous visitor opens the About page on mobile

- **WHEN** an unauthenticated visitor opens `/about` at a 390px viewport
- **THEN** the page has one page heading
- **AND** the administration sidebar is not rendered
- **AND** the document does not horizontally overflow

### Requirement: Login and home forms avoid premature error states

The system SHALL validate administrator email locally before starting a Passkey ceremony and SHALL not show required chat-capture errors until the user attempts to enable chat capture.

#### Scenario: Visitor attempts login with an empty email

- **WHEN** a visitor selects the Passkey login action with an empty or whitespace-only email
- **THEN** the page presents an accessible validation message
- **AND** no request is sent to `/api/auth/passkey/options`

#### Scenario: Administrator opens an unconfigured control panel

- **WHEN** an authenticated administrator opens the home control panel before entering a live URL or keyword
- **THEN** required-field errors are not visible
- **WHEN** the administrator attempts to enable chat capture
- **THEN** the required-field errors are visible and capture remains disabled

### Requirement: Public metadata and privacy copy describe the deployed product

The system SHALL identify the product in traditional Chinese and SHALL describe self-hosted data handling without asserting that no data is stored.

#### Scenario: Visitor inspects page metadata

- **WHEN** a visitor opens the application
- **THEN** the root document language is `zh-Hant`
- **AND** the document title identifies EasyLottery

#### Scenario: Visitor reads the privacy policy

- **WHEN** a visitor opens `/privacy-policy`
- **THEN** the policy states that the deployer controls storage and retention
- **AND** the policy identifies Passkey public credentials, configuration, activity/result, and payment-callback data as applicable data categories
- **AND** the policy does not state that the application never stores information

### Requirement: Audited management pages expose semantic primary headings

The system SHALL provide exactly one semantic primary heading for the payment, YouTube, and audit settings pages.

#### Scenario: Administrator opens an audited settings page

- **WHEN** an authenticated administrator opens payment, YouTube, or audit settings
- **THEN** the page exposes one `h1` corresponding to its page title
