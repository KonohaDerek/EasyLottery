## ADDED Requirements

### Requirement: Multi-streamer grouped collection
The mobile app MUST use only the Card Hub API with one viewer account and MUST show the collection grouped by streamer, then by set and rarity, with viewer-defined groups, search, and filters.

#### Scenario: Cards from two streamers
- **WHEN** a viewer has redeemed cards from two streamers
- **THEN** the collection shows two streamer groups under the same account

### Requirement: Redeem with pack-opening animation
The app MUST redeem a code against the Card Hub and play the pack-opening animation that reveals the card.

#### Scenario: Successful redemption
- **WHEN** a valid code is entered
- **THEN** the animation plays and the card appears in the collection

### Requirement: Capture protection and wallpapers
The phase-one store release MUST target Android and MUST block screenshots and recording of card screens on Android. Shared React Native code SHOULD remain portable to iOS, and iOS Safari web flows remain supported; native iOS capture protection, wallpaper export, and store submission are deferred until payment-model review is complete. Android phase one MUST offer wallpaper export within platform limits.

#### Scenario: Android capture
- **WHEN** a screenshot or recording is taken on a card screen on Android
- **THEN** the captured image is blank

#### Scenario: Native iOS release is deferred
- **WHEN** phase-one acceptance is evaluated
- **THEN** native iOS store submission and native iOS capture protection are out of scope

#### Scenario: Android sets wallpaper
- **WHEN** the viewer chooses "set as wallpaper"
- **THEN** Android saves the watermarked image and lets the viewer set it as the home or lock screen
