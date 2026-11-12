## ADDED Requirements

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
