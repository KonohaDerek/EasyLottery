## ADDED Requirements

### Requirement: Atomic viewer-to-viewer trades
Viewers of the same streamer site MUST be able to propose, accept, decline, or cancel trades of owned card instances; acceptance MUST transfer ownership atomically after re-checking ownership.

#### Scenario: Accepted trade swaps ownership
- **WHEN** the recipient accepts a trade whose instances are still owned as proposed
- **THEN** ownership of all offered and requested instances is exchanged in one transaction and recorded in the audit log

#### Scenario: Conflicting trades
- **WHEN** two pending trades include the same instance and both are accepted concurrently
- **THEN** only one trade completes and the other fails without changing ownership

#### Scenario: Non-tradable set
- **WHEN** a trade includes an instance from a set marked non-tradable
- **THEN** the proposal is rejected
