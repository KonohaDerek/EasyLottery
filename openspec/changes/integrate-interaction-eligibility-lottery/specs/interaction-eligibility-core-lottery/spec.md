## ADDED Requirements

### Requirement: Explicitly preview and import an interaction eligibility snapshot
The authenticated administrator MUST preview the selected interaction round's eligibility snapshot and explicitly confirm importing it into the core lottery. Import MUST be idempotent by round, snapshot and stable AudienceProfile identity. Display names MUST NOT be used as identity keys.

#### Scenario: Preview and import an eligible round
- **WHEN** an administrator selects a valid round and confirms the preview
- **THEN** the system shows its name, state, snapshot, eligible count and import differences before changing the participant list
- **AND** the participant entries retain source round, snapshot and profile identity

#### Scenario: Retry the same snapshot
- **WHEN** the same snapshot is imported more than once or the request is retried
- **THEN** it does not create another entry or extra lottery eligibility

#### Scenario: Same name without identity match
- **WHEN** a manual entry has the same display name as an imported profile but no confirmed stable identity link
- **THEN** the entries remain distinct and the preview reports a possible name collision

#### Scenario: Exact scoped identity links a manual entry
- **WHEN** a manual entry's external identity has the same platform, channel scope and account ID as an imported profile identity
- **THEN** the manual entry gains the imported provenance without adding another lottery entry

#### Scenario: Same account ID with a different or missing channel scope
- **WHEN** a manual entry shares an imported identity's platform and account ID but its channel scope differs or either side lacks a channel scope
- **THEN** the entries are not linked, the imported profile is added as a separate entry
- **AND** the preview reports a possible identity conflict

#### Scenario: Empty, unavailable or failed source
- **WHEN** the round is missing, ineligible, empty, or the API request fails
- **THEN** the current manual list and pool lock remain unchanged
- **AND** the administrator receives a recoverable message

### Requirement: Lock a draw pool independently from live interaction changes
The system MUST let an administrator lock a versioned snapshot of the core lottery pool before drawing. While locked, participant, prize and chat-import mutations MUST NOT change the pool. Explicit unlock is required before any such mutation.

#### Scenario: Reload a locked page
- **WHEN** an administrator reloads the same tab after locking the pool
- **THEN** the application restores and validates the exact pool snapshot before allowing a draw

#### Scenario: Invalid persisted lock
- **WHEN** locked-pool storage has an unsupported schema or fails its integrity check
- **THEN** the application blocks drawing and provides an explicit recovery action

#### Scenario: Explicitly unlock
- **WHEN** the administrator explicitly unlocks the pool
- **THEN** participant/prize mutations resume
- **AND** the prior pending draw and saved history remain unchanged

### Requirement: Preserve immutable interaction provenance in draw history
The system MUST snapshot the interaction source round, eligibility snapshot and AudienceProfile identity into each applicable draw participant record. Later identity merge, unlink, rename or new interaction events MUST NOT rewrite prior draw history. Source identity fields MUST NOT be added to public OBS responses.

#### Scenario: Identity changes after a draw
- **WHEN** a profile is merged, unlinked or renamed after its eligibility was imported and a draw was saved
- **THEN** the historical record retains the original source IDs and draw-time display data

#### Scenario: Existing history without provenance
- **WHEN** the system reads a history record created before source metadata existed
- **THEN** it remains readable with null interaction provenance

### Requirement: Identify the locked participant-list version in draw history
A draw made from a locked pool MUST record the pool's lock hash and lock time as its participant-list version. The version MUST be preserved across reload, reconciliation and retry of the same draw ID, and MUST be part of draw-ID idempotency. Draws made without a lock and history saved before this field existed MUST have no version.

#### Scenario: Draw from a locked pool
- **WHEN** an administrator draws while the pool is locked
- **THEN** the draw request and the saved admin history include the lock hash and lock time

#### Scenario: Retry with a different pool version
- **WHEN** a draw ID is retried with a different, added or removed pool version
- **THEN** the API rejects it as a conflicting result and does not change the saved record

#### Scenario: Unlocked or legacy draw
- **WHEN** a draw is made without a lock or a history record predates the pool version
- **THEN** the record remains readable with a null pool version

#### Scenario: Review traceability in admin history
- **WHEN** an administrator lists draw history or opens a draw's history detail
- **THEN** the paginated list and the detail include the pool version, and the detail shows the lock hash and lock time when present
- **AND** each roster row shows its interaction source round ID, snapshot ID and AudienceProfile ID, or an em dash for manual rows
- **AND** unlocked and legacy draws show an em dash instead of a pool version
