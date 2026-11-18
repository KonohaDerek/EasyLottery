## ADDED Requirements

### Requirement: Typed shop products
The shop MUST store each product with a product type; `card_pack` MUST be supported and unknown types MUST be rejected for creation until implemented.

#### Scenario: Create a card pack product
- **WHEN** the admin creates a product of type `card_pack`
- **THEN** it is stored and listed in the admin shop

#### Scenario: Unsupported product type
- **WHEN** the admin creates a product of an unimplemented type
- **THEN** the request is rejected with a validation error

### Requirement: Serialized cards with rarity
Every card MUST have a unique, never-reused serial number within its set and exactly one configured rarity.

#### Scenario: Serial numbers are assigned
- **WHEN** three cards are added to set `S1`
- **THEN** they receive `S1-001`, `S1-002`, `S1-003`, and a deleted card's serial is not reused

### Requirement: Uploaded and AI-generated card images
The admin MUST be able to create a card image by upload or by an AI provider configured in settings; provider API keys MUST never be returned to the browser.

#### Scenario: No AI provider configured
- **WHEN** no AI image provider is configured
- **THEN** only upload is offered and the generate action explains why it is unavailable

#### Scenario: Provider refuses a prompt
- **WHEN** the configured provider rejects a prompt for content policy
- **THEN** the admin sees a clear refusal message and no card image is created

#### Scenario: Usage cap reached
- **WHEN** the configured daily or monthly generation cap is reached
- **THEN** further generation requests are rejected until the window resets
