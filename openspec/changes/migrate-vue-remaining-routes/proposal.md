# Proposal

## Why

The Strangler Migration has moved the public shell, admin settings, lottery, OBS overlays, and interaction pages to Vue, but `/market`, `/system/platform-connections`, and `/activity/overtime` still fall through to Blazor. Migrating these routes completes the remaining user-facing route slice while keeping the existing REST contracts and rollback path. This change also hardens the current overtime-feed merge behavior against a payment event arriving between a stale client read and write.

## What Changes

- Add Vue admin routes and navigation entries for marketplace, platform connections, and overtime settings.
- Preserve published-template discovery and copy-install behavior, authenticated connection settings with masked secrets and ETags, and overtime settings, preview-link, demo, and clear behavior.
- Preserve newly appended SuperChat and third-party payment events when an older full-feed snapshot is written through the existing overtime-feed endpoint.
- Keep existing REST/OAuth, settings, feed, and scoped OBS-token contracts unchanged; keep each Blazor page reachable through the existing legacy route strategy.
- Add unit and browser coverage for direct navigation, authorization, empty/error states, safe template duplication, masked-secret/ETag handling, and explicitly triggered overtime actions.

## Capabilities

### New Capabilities

- `marketplace-installation`: Browse published built-in activity templates and install editable copies without mutating the source or another installed activity.
- `platform-connections`: Read and update supported platform connection settings through the existing authenticated REST contract while preserving masked credentials and ETag concurrency.
- `overtime-settings`: Configure the existing overtime overlay, its preview URL, and explicit demo/clear actions while preserving live session state and server authorization.
- `overtime-feed`: Protect concurrently appended payment events from stale full-feed writes without changing the endpoint, DTO, or authorization contract.

### Modified Capabilities

None. The repository currently has no registered OpenSpec specs; these capabilities document the migrated UI and the strengthened behavior of an existing endpoint without changing its REST contract.

## Impact

- Vue: `src/EasyLotteryWeb/src/routes.ts`, `components/AdminLayout.vue`, new views and focused API helpers/tests under `src/EasyLotteryWeb/src`.
- API behavior regression guard: `src/EasyLotteryAPI/Obs/Endpoints/OvertimeFeedEndpoints.cs` and `tests/EasyLotteryAPITests/OvertimeFeedSuperChatApiTests.cs`.
- Browser tests: focused Playwright specs under `tests/e2e`.
- Project records: this OpenSpec change plus `.agents/decisions`, `.agents/plans`, `.agents/state`, and `.agents/verification`.
- Existing endpoint URLs, DTOs, scopes, SignalR notifications, Domain, Payment, YouTube, SMTP, database schema, and production configuration remain unchanged. The existing feed `PUT` now preserves concurrent payment events; the explicit `DELETE` clear operation remains unchanged.

## Acceptance and Risk

- All three routes render Vue directly, survive refresh, and require an admin session.
- A marketplace install always creates and publishes a new editable copy; no source or existing activity is overwritten.
- If creating a copy succeeds but publication fails, retain its ID and retry publication without creating another copy.
- Platform credential sentinels are not rendered as secrets and are preserved unchanged on save; stale ETags show a recoverable conflict.
- Overtime settings are saved through the existing admin endpoint, preserve session-owned fields and a newer server-side planned end unless the administrator edited that field, and demo/clear actions require explicit clicks.
- A stale full-feed write cannot erase a concurrent SuperChat or third-party payment event; explicit feed clearing still works through the existing `DELETE` endpoint.
- The old Blazor implementation remains available for rollback. No production cutover or native OBS verification is claimed.
