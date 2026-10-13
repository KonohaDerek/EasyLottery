# Spec Delta

## Purpose

This capability lets administrators configure the existing overtime overlay, obtain its scoped preview link, and perform explicitly requested demo or feed-clear actions while preserving live OBS-owned session state.

## ADDED Requirements

### Requirement: Load and save overtime overlay settings
The system SHALL load and save overtime settings through the existing administrator-authorized settings resource and SHALL preserve session fields owned by an active OBS session.

#### Scenario: Load overtime settings
- **WHEN** an authenticated administrator opens `/activity/overtime`
- **THEN** the system loads the existing settings and ETag
- **AND** presents the supported overlay settings, reward rules, text animation, and completion-fireworks options

#### Scenario: Save settings while OBS owns a live session
- **WHEN** an administrator saves overlay settings while an OBS session is running, paused, completed, or otherwise active
- **THEN** the system obtains the latest server settings before writing
- **AND** preserves the latest session state, stream start, pause, and completion fields
- **AND** reports success only after the server accepts the ETag-protected update

#### Scenario: Preserve a planned end changed by a concurrent payment
- **WHEN** an administrator changes another setting after the page loaded and a payment extends the planned end before save
- **AND** the administrator did not edit the planned-end field
- **THEN** the save preserves the latest server-side planned end
- **AND** an explicitly edited planned end is still saved as requested

#### Scenario: Settings save fails
- **WHEN** loading or saving settings fails, including a stale ETag conflict
- **THEN** the system shows a recoverable error
- **AND** does not present unsaved values as persisted

### Requirement: Provide a scoped overtime preview link
The system SHALL provide an overtime OBS preview URL using the existing scoped-token flow and SHALL not reveal the token in page text, logs, or unrelated routes.

#### Scenario: Copy the preview link
- **WHEN** an administrator explicitly requests the overtime preview link
- **THEN** the system copies the existing scoped `/obs/overtime` URL
- **AND** confirms the result without exposing the token separately

### Requirement: Make demo and clear actions explicit
The system SHALL create demo feed entries and clear the overtime feed only after an administrator explicitly activates the corresponding action.

#### Scenario: Send a demo contribution
- **WHEN** an administrator explicitly activates a SuperChat or ECPay demo action
- **THEN** the system sends one demo event through the existing endpoint
- **AND** does not contact YouTube, ECPay, or another real provider

#### Scenario: Clear the feed
- **WHEN** an administrator explicitly activates the clear-feed action
- **THEN** the system clears the overtime feed through the existing endpoint
- **AND** reports success only after the request succeeds

#### Scenario: Load the settings page
- **WHEN** the overtime settings page initializes or is refreshed
- **THEN** no demo event is created and no feed is cleared

### Requirement: Preserve polished animated presentation controls
The system SHALL preserve the existing text-animation and completion-fireworks choices, and any animation preview SHALL follow the product's polished animated visual style and respect reduced-motion preferences.

#### Scenario: Reduced motion is requested
- **WHEN** the browser requests reduced motion
- **THEN** any settings-page animation preview avoids nonessential motion while keeping all settings usable
