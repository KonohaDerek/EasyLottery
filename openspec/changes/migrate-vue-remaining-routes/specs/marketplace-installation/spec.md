# Spec Delta

## Purpose

This capability lets an administrator discover published built-in activity templates and install safe, independently editable copies without changing the catalog source or another activity.

## ADDED Requirements

### Requirement: Browse published built-in templates
The system SHALL show only published built-in PokeBox and Roulette templates in the marketplace and SHALL indicate whether a copy is already installed.

#### Scenario: Marketplace has published templates
- **WHEN** an authenticated administrator opens `/market`
- **THEN** the system shows published built-in templates and their installed status
- **AND** excludes unpublished, non-built-in, and unrelated user templates

#### Scenario: Marketplace has no published templates
- **WHEN** the template APIs return no published built-in templates
- **THEN** the system shows a clear empty state
- **AND** does not present a failed or endless loading state

### Requirement: Install a template as an editable copy
The system SHALL install a selected marketplace template by creating a new editable copy, without overwriting the catalog source or any previously installed activity.

#### Scenario: First installation
- **WHEN** an administrator installs a published built-in template
- **THEN** a new editable copy is created through the existing API
- **AND** the new copy is set to `Published` through the existing publication API
- **AND** the source template remains unchanged
- **AND** the administrator can open the newly created copy

#### Scenario: Reinstall an already installed template
- **WHEN** an administrator chooses to install the latest version again
- **THEN** another editable copy is created
- **AND** only that new copy is set to `Published`
- **AND** the earlier installed copy remains unchanged

#### Scenario: Template installation fails
- **WHEN** the duplicate request fails
- **THEN** the system shows a recoverable error
- **AND** does not navigate to an activity that was not created

#### Scenario: Publishing a newly created copy fails
- **WHEN** duplication succeeds but publishing the returned copy fails
- **THEN** the system retains the copy ID and offers a publication retry for that same copy
- **AND** the retry does not call the duplicate endpoint again
- **AND** the administrator can open the retained copy if publication remains unavailable

### Requirement: Keep marketplace navigation usable
The system SHALL provide clear loading, empty, and error feedback for marketplace data and SHALL keep the route protected by the administrator session.

#### Scenario: Unauthenticated direct navigation
- **WHEN** a visitor without an administrator session opens `/market`
- **THEN** the system redirects the visitor to the existing login flow
- **AND** does not issue an authenticated template mutation
