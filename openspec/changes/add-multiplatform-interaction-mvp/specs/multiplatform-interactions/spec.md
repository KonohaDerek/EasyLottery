## ADDED Requirements

### Requirement: Unified audience identity

The system SHALL associate platform identities with an audience profile and apply per-round participation limits by audience profile.

#### Scenario: Linked identities vote once

- **WHEN** two platform identities belong to one audience profile and each submits a valid vote in the same round
- **THEN** the first vote is accepted and the second is rejected as a duplicate profile vote

### Requirement: Idempotent interaction event processing

The system SHALL persist and deduplicate platform events by platform, channel scope, and external event ID.

#### Scenario: Replayed event does not score twice

- **WHEN** a connector submits the same external event twice
- **THEN** the second submission does not change points, votes, or eligibility

### Requirement: Scoped OBS interaction state

The system SHALL provide the active interaction snapshot only to an OBS token scoped for that interaction resource.

#### Scenario: OBS reconnect receives current state

- **WHEN** an authorized OBS source reconnects during a live round
- **THEN** it receives the current snapshot before subsequent state changes
