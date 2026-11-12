## ADDED Requirements

### Requirement: Card pack configuration
A card pack MUST reference one card set, a streamer-provided cover image, and rarity weights that sum to 100 where every weighted rarity contains at least one card.

#### Scenario: Invalid odds are rejected
- **WHEN** the weights do not sum to 100 or a weighted rarity has no cards
- **THEN** the pack cannot be published

#### Scenario: Odds are public
- **WHEN** a pack is published
- **THEN** its rarity odds are visible on the public shop page

### Requirement: One-card auditable draw
Opening a pack MUST draw exactly one card using a cryptographic RNG by the published weights, reserve a new card instance, and record the draw, all atomically.

#### Scenario: Draw distribution
- **WHEN** a pack with weights N=70, R=25, SR=5 is drawn 100,000 times in a test
- **THEN** each rarity's observed frequency is within statistical tolerance of its weight

#### Scenario: Supply exhausted
- **WHEN** a pack's opened count reaches its supply limit
- **THEN** further draws are rejected and no card instance is created
