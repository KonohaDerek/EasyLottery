## ADDED Requirements

### Requirement: Multi-streamer grouped collection
The mobile app MUST let a viewer add multiple streamer sites and MUST show the collection grouped by streamer, then by set and rarity, with viewer-defined groups, search, and filters.

#### Scenario: Two streamer sites
- **WHEN** a viewer has redeemed cards on two streamer sites
- **THEN** the collection shows two streamer groups with each site's cards

### Requirement: Redeem with pack-opening animation
The app MUST redeem a code against its streamer site and play the pack-opening animation that reveals the card.

#### Scenario: Successful redemption
- **WHEN** a valid code is entered
- **THEN** the animation plays and the card appears in the collection

### Requirement: Capture protection and wallpapers
The app MUST block screenshots and recording of card screens on Android, MUST obscure card content while iOS screen capture is active and record iOS screenshot events, and MUST offer wallpaper export within platform limits.

#### Scenario: Android capture
- **WHEN** a screenshot or recording is taken on a card screen on Android
- **THEN** the captured image is blank

#### Scenario: iOS recording
- **WHEN** iOS screen recording or mirroring starts on a card screen
- **THEN** card content is obscured until capture stops

#### Scenario: Set wallpaper
- **WHEN** the viewer chooses "set as wallpaper"
- **THEN** Android sets home or lock screen directly and iOS saves the image and guides the viewer through system settings
