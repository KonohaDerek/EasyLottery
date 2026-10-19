## ADDED Requirements

### Requirement: Source-scoped stable participant identity
The lottery system MUST use the pair of platform source and stable account ID to identify a chat participant when both are available. Display names MUST NOT be used to automatically merge participants.

#### Scenario: Same account changes display name
- **WHEN** accepted messages from the same platform and stable account ID use different display names or casing
- **THEN** only one participant is added for that identity

#### Scenario: Distinct accounts share a display name
- **WHEN** accepted messages have the same display name but different stable account IDs on one platform
- **THEN** each account remains a distinct participant

#### Scenario: Same identifier exists on multiple platforms
- **WHEN** two messages have the same account-ID string but different platform sources
- **THEN** they remain distinct participants unless an existing explicit verified identity link is applied outside this behavior

#### Scenario: Stable identity is unavailable
- **WHEN** a message lacks a platform or stable account ID
- **THEN** it is not merged with another participant based on display name and receives no inferred account identity

### Requirement: Identity-aware cooldown with compatible name rules
The lottery system MUST apply cooldown to the same stable platform/account identity across display-name changes. When identity is unavailable, it MUST NOT attribute account cooldown to a guessed display name. Existing keyword and explicit display-name allow/deny rules MUST remain unchanged.

#### Scenario: Account changes name during cooldown
- **WHEN** the same stable platform/account identity submits another eligible message before its cooldown expires under a different display name
- **THEN** the second message is rejected by cooldown

#### Scenario: Same-name accounts have independent cooldowns
- **WHEN** two stable identities share a display name
- **THEN** one identity's accepted message does not start the other's cooldown

#### Scenario: Account identity is missing
- **WHEN** an accepted message has no stable account identity
- **THEN** it is not recorded as an account cooldown for another message, and the participant is not inferred from its display name

### Requirement: Backward-compatible participant identity import/export
The lottery system MUST continue to accept existing participant JSON and CSV without identity fields, and MUST round-trip optional platform/account ID fields in new JSON and CSV exports.

#### Scenario: Import legacy participant data
- **WHEN** participant JSON or CSV has no platform/account ID columns
- **THEN** entries import normally without inferred identity

#### Scenario: Round-trip explicit identity
- **WHEN** participant JSON or CSV contains a valid platform and account ID
- **THEN** import preserves the identity and export includes it
