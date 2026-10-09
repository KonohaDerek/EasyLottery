# vue-activity-results Specification

## ADDED Requirements

### Requirement: Authenticated activity-result browsing

The Vue application MUST render the activity-results route only for an unexpired admin session and MUST fetch records through the existing `GET /api/activity-results` endpoint using the shared session-token header. The route MUST NOT change the API contract or expose the token in a URL.

#### Scenario: Unauthenticated visitor opens results

- **WHEN** a visitor without a valid admin token opens `/activity-results`
- **THEN** the router redirects to the login route with a safe return path
- **AND** no activity-results API request is sent

#### Scenario: Admin opens results from either entry path

- **WHEN** an admin opens `/activity-results` directly or follows its Blazor navigation link
- **THEN** the Vue shell renders the page
- **AND** the request carries the token in `X-EasyLottery-Session-Token`

### Requirement: Searchable, filterable, accessible results

The page MUST preserve the existing activity type, search fields, date-range filtering, descending sort order, summary metrics and expandable result-item details. Filter controls and disclosure MUST be keyboard accessible.

#### Scenario: Filters are combined

- **WHEN** an admin enters a search term and selects an activity type and local date range
- **THEN** only records matching all selected criteria are shown
- **AND** displayed counts and the latest-activity label reflect the filtered records

#### Scenario: Results are empty or filtered out

- **WHEN** the API returns no records or filters match no records
- **THEN** the page shows the corresponding distinct empty-state guidance

### Requirement: Reliable loading and CSV export

The page MUST distinguish loading, API failure and empty data. It MUST provide retry after failure and export only the currently filtered records using the existing CSV schema, BOM, CRLF and quoting behavior.

#### Scenario: API read fails then succeeds on retry

- **WHEN** the activity-results request fails and a later retry succeeds
- **THEN** the page displays an accessible error and retry action before success
- **AND** the failure is not presented as an empty archive

#### Scenario: Admin exports filtered results

- **WHEN** an admin exports a non-empty filtered result set
- **THEN** the browser downloads a UTF-8 CSV with the existing column order and escaped quoted cells
- **AND** an empty filtered set cannot be exported
