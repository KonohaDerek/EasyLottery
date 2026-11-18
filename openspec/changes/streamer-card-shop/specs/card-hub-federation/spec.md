## ADDED Requirements

### Requirement: Platform administrator boundary
Hub management endpoints MUST require a separately configured platform-admin credential, use HTTPS outside loopback development, apply rate limits, and create audit events for invite, tenant-status and site-credential operations. A missing admin credential MUST fail closed.

#### Scenario: Admin credential is absent or invalid
- **WHEN** a request reaches a management endpoint without a valid platform-admin credential
- **THEN** the request is rejected and no state changes

#### Scenario: Tenant deletion notice
- **WHEN** a tenant enters the 30-day deletion notice period
- **THEN** the Hub shows a notice in the Hub and redemption pages, notifies account holders in Hub and by email, emails holders of unredeemed codes only where consent covers this service notice, and records the notice event; the streamer receives prepared notice text for their own post

#### Scenario: Deletion grace period is open
- **WHEN** the 30-day notice period has not ended
- **THEN** images remain available to existing holders; after the period, images are purged and card instances render a removed placeholder

### Requirement: Streamer site linking
The Card Hub MUST let a streamer register a tenant and link their EasyLottery site with revocable client credentials scoped to that tenant.

#### Scenario: Link a site
- **WHEN** the streamer enters the Hub client ID and secret in site settings
- **THEN** the site verifies them with the Hub, stores the secret encrypted, and never returns it to the browser

#### Scenario: Revoked credentials
- **WHEN** the streamer revokes the site credentials on the Hub
- **THEN** further site calls are rejected and the site admin shows the link as broken

### Requirement: Catalog publishing
A linked site MUST publish card sets, cards, images and packs to its Hub tenant; published serial numbers and rarities MUST be immutable on the Hub.

#### Scenario: Publish a pack
- **WHEN** the streamer publishes a valid pack
- **THEN** the Hub stores a versioned snapshot of the pack, its cards and images under the streamer's tenant

#### Scenario: Change a published card's rarity
- **WHEN** the site attempts to change the rarity or serial of an already published card
- **THEN** the Hub rejects the change and existing owned instances are unaffected

### Requirement: Idempotent Hub draw API
The Hub MUST expose a draw API for linked sites that is idempotent per tenant and site payment reference.

#### Scenario: Retried draw request
- **WHEN** a site sends the same draw request twice with the same payment reference
- **THEN** the Hub returns the same drawn card and issues only one redemption code
