# Creator admin experience

## ADDED Requirements

### Requirement: Creator-first grouped administration

The authenticated Vue admin shell SHALL group existing routes by creator workflow, expose a visible accessible logout action, and preserve each route's authorization and behavior.

#### Scenario: Creator navigation groups

- **WHEN** an administrator opens any protected Vue route
- **THEN** the navigation presents creator/live-stream tasks before secondary system settings, groups can be expanded and collapsed by keyboard, and the current route remains clearly identified

#### Scenario: Visible logout

- **WHEN** an administrator views the desktop or mobile shared shell or tabs to the logout action
- **THEN** the action has WCAG AA text contrast, a visible keyboard focus indicator, and an accessible name; activating it clears the existing session tokens and redirects to `/login`

### Requirement: One guided system YouTube setup path

The Vue interface SHALL present one authoritative editor for the system YouTube Live Chat key and Super Chat setting, clearly distinguish the separate interaction-platform credential store, and keep all secrets server-side and masked.

#### Scenario: Configure Live Chat from the creator home

- **WHEN** the existing server status reports that the system YouTube key is not configured
- **THEN** the creator home shows a direct setup action to `/system/youtube-login` and explains that the server uses the key for YouTube Live Chat

#### Scenario: Configured, unavailable, or invalid key

- **WHEN** the status is configured, cannot be read, or a later Live Chat API request fails
- **THEN** the user sees a distinct status or existing recoverable API error and can navigate to the same setup page without the key appearing in URLs, logs, or ordinary text

#### Scenario: Similar interaction credential is distinct

- **WHEN** the administrator views YouTube under platform connections
- **THEN** the UI explains that this is a separate interaction-platform setting, not the `SystemSettings.YouTube.ApiKey` consumed by the lottery Live Chat flow; existing server values are neither copied nor cleared

### Requirement: Consistent accessible creator visual tokens

The public creator pages, admin shell, and core creator workflows SHALL use the shared Vue theme tokens for color, type hierarchy, surfaces, and focus while remaining responsive and readable.

#### Scenario: Shared theme and reduced motion

- **WHEN** a creator navigates between public pages, the lottery workspace, admin navigation, and settings
- **THEN** shared colors and surfaces remain consistent, text and controls meet WCAG AA contrast, focus is visible, and `prefers-reduced-motion` and transparent OBS styles continue to work

### Requirement: Original marketplace previews

The marketplace SHALL show useful original visual previews for Roulette and PokeBox with accessible alternatives and a functional fallback while preserving the existing copy-install behavior.

#### Scenario: Artwork available or unavailable

- **WHEN** a marketplace card loads or its artwork request fails
- **THEN** the preview uses non-distorting dimensions, lazy loading, specific alternative text, and a readable fallback; install controls remain keyboard accessible and unchanged

### Requirement: Derived Donate schedule state

The Donate activity list SHALL show a read-only lifecycle state derived from existing UTC dates separately from the persisted enabled/disabled flag.

#### Scenario: Scheduled, active, ended, or incomplete period

- **WHEN** the list renders with future start, within range, exact end/past end, missing end, or invalid required date values
- **THEN** it shows the corresponding scheduled, active, ended, or incomplete state using UTC boundary comparisons and the existing local-time date presentation

#### Scenario: Manually disabled or ended-but-enabled

- **WHEN** an activity is disabled or its schedule has ended while its saved flag remains enabled
- **THEN** both independent states are shown and no activity data is modified automatically
