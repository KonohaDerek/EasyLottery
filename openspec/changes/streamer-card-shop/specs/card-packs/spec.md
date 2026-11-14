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
Opening a pack MUST draw exactly one card using a cryptographic RNG by the immutable odds version shown to the viewer, reserve a new card instance, and record the draw and odds version atomically.

#### Scenario: Draw distribution
- **WHEN** a pack with weights N=70, R=25, SR=5 is drawn 100,000 times in a test
- **THEN** each rarity's observed frequency is within statistical tolerance of its weight

#### Scenario: Supply exhausted
- **WHEN** any rarity in a published finite-supply pack version has no remaining instances
- **THEN** sales for that pack version stop; its published odds never change, and no further draws are accepted

#### Scenario: New odds after depletion
- **WHEN** a streamer wants to resume sales after a rarity is depleted
- **THEN** they publish a new pack version with new odds, and the shop displays those odds before accepting another paid draw

#### Scenario: Paid callback races with the last instance
- **WHEN** a paid callback arrives after the last instance has already been reserved
- **THEN** the request is recorded as unresolved and no additional card or redemption code is issued until the approved payment recovery procedure resolves it
