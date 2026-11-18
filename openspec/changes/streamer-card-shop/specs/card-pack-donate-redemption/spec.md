## ADDED Requirements

### Requirement: Email-gated Donate card-pack activity
A Donate card-pack activity MUST require an email address, explicit consent, and an 18+ declaration before the donation can trigger a paid draw. The flow MUST NOT claim guardian consent in Wave 1.

#### Scenario: Donation without email
- **WHEN** a donation for a card-pack activity has no email or no consent
- **THEN** the donation is processed normally but no draw is triggered and the donor is told why

#### Scenario: Eligible donation draws once
- **WHEN** a paid callback over the threshold with a consented email arrives, including duplicate callbacks for the same payment
- **THEN** the site calls the Hub once per payment reference, exactly one card is drawn on the Hub, the OBS overlay plays the pack-opening animation, and the Hub emails one redemption code

#### Scenario: Minor attempts a paid draw
- **WHEN** the donor declares an age bracket of 13–17
- **THEN** the paid draw is not triggered and the flow explains that paid card packs are for adults in Wave 1

### Requirement: Single-use redemption codes
Redemption codes MUST be high-entropy, stored as hashes in the redemption record, expire, and be redeemable exactly once through the viewer app. An encrypted delivery outbox MAY retain a recoverable code copy only while required for delivery retry/resend, and MUST delete it after expiry or when the delivery-retention window ends.

#### Scenario: Concurrent redemption
- **WHEN** the same code is submitted twice concurrently
- **THEN** exactly one request succeeds and the card instance is owned by that viewer

#### Scenario: Expired or void code
- **WHEN** an expired or voided code is submitted
- **THEN** redemption is rejected and repeated failures are rate-limited

#### Scenario: Card Hub is unreachable
- **WHEN** the site cannot reach the Hub after an eligible donation
- **THEN** the draw request is queued and retried with the same idempotency key, and OBS and admin show a pending state

#### Scenario: Admin re-sends a code
- **WHEN** an admin re-sends the code for an unredeemed draw before its original expiry
- **THEN** the same code is sent only to the original consented email and its original expiry is preserved

#### Scenario: Recipient email retention
- **WHEN** the Hub queues or retries a redemption email
- **THEN** the recipient address and any recoverable code copy are retained only in encrypted delivery data for delivery and support, then deleted from the database, outbox, logs and backups no later than 180 days after the later of redemption or code expiry

#### Scenario: Expired code resend
- **WHEN** the redemption code has expired
- **THEN** the system does not reissue a code or send a reminder; the draw follows the approved payment recovery procedure
