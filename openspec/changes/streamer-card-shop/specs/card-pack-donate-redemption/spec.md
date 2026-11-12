## ADDED Requirements

### Requirement: Email-gated Donate card-pack activity
A Donate card-pack activity MUST require an email address and explicit consent before the donation can trigger a draw.

#### Scenario: Donation without email
- **WHEN** a donation for a card-pack activity has no email or no consent
- **THEN** the donation is processed normally but no draw is triggered and the donor is told why

#### Scenario: Eligible donation draws once
- **WHEN** a paid callback over the threshold with a consented email arrives, including duplicate callbacks for the same payment
- **THEN** exactly one card is drawn, the OBS overlay plays the pack-opening animation, and one redemption code is emailed

### Requirement: Single-use redemption codes
Redemption codes MUST be high-entropy, stored only as hashes, expire, and be redeemable exactly once through the viewer app.

#### Scenario: Concurrent redemption
- **WHEN** the same code is submitted twice concurrently
- **THEN** exactly one request succeeds and the card instance is owned by that viewer

#### Scenario: Expired or void code
- **WHEN** an expired or voided code is submitted
- **THEN** redemption is rejected and repeated failures are rate-limited

#### Scenario: Admin re-sends a code
- **WHEN** the admin re-sends a code for an unredeemed draw
- **THEN** a new code is emailed and the previous code becomes void
