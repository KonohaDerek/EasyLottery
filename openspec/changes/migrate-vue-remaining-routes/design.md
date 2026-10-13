# Design

## Context

See `proposal.md` for motivation and `specs/` for user-visible requirements. The Vue app already has an authenticated admin shell, shared `apiRequest` session/ETag handling, PokeBox/Roulette template clients, and an overtime OBS page. The three admin routes remain owned by Blazor. Platform connections return a masked-secret sentinel and use an ETag-protected update endpoint; overtime settings have a separate admin resource endpoint from the scoped OBS overlay endpoint.

## Goals / Non-Goals

**Goals:**

- Make all three routes first-class Vue admin routes with direct navigation and refresh support.
- Reuse existing UI, API, and authentication patterns while keeping backend contracts unchanged.
- Preserve secret, concurrency, copy-install, and live-session semantics.
- Prevent stale full-feed writes from dropping payment events appended by a concurrent callback.
- Keep a straightforward route-level rollback to the existing Blazor pages.

**Non-Goals:**

- Adding OAuth UX that the current Blazor page does not provide.
- Reworking payment, provider, Domain, database, or deployment behavior.
- Changing existing REST URLs, request/response DTOs, OBS token scopes, or SignalR event names.
- Replacing the existing `/obs/overtime` implementation or changing its token scopes.

## Decisions

### Route and layout ownership

Add the three paths to the existing Vue admin route table with `requiresAuth`, add navigation entries to `AdminLayout`, and update the API host's existing Vue-shell route allowlist. Add explicit `/legacy/...` aliases to the existing Blazor pages before transferring the original paths to Vue, preserving route-level rollback. Keep route registration and fallback changes in the main integration branch; the pages share this surface, so their route contract is not parallelized.

### Marketplace uses existing copy endpoints

Load PokeBox and Roulette templates with their existing authenticated APIs, filter to built-in and published sources, and identify installations by source public ID. Preserve the legacy flow: every install/reinstall calls the existing duplicate endpoint, sets `Published` on only the newly returned copy through the existing publication endpoint, then navigates to that copy. Never mutate the catalog source or an earlier installed copy. Build the catalog with the existing Vuetify styles rather than adding a marketplace dependency or new asset pipeline.

If duplication succeeds but publication fails, retain the returned copy ID in the UI and retry only the publication request. This avoids orphaning the recovery path or creating another duplicate on retry.

### Platform connection secrets remain write-only in Vue

Use `apiRequest` against the existing connections endpoints and keep the response ETag. Show configured/degraded status without rendering the server's unchanged-secret sentinel. Track each credential field as untouched, replaced, or explicitly cleared: send `null` for untouched fields (the server preserves them), a supplied string for replacements, and an empty string only after an explicit clear action. On `409`, stop saving and require a fresh load. Do not add a Twitch OAuth flow that is absent from the current page.

### Overtime admin settings use the admin resource API

Use the existing `/api/settings/overtime` resource and `If-Match` ETag for the admin settings page; keep the OBS overlay's scoped `/settings` behavior unchanged. Before saving, fetch the latest settings and merge the administrator-editable fields onto that snapshot, preserving the latest `SessionState`, `StreamStartedAtUtc`, `PausedAtUtc`, and `CompletedAtUtc`. If the administrator did not edit the planned end field, also preserve the newest server value rather than writing the stale form value back. Use the existing scoped-token creation path for the preview URL and the existing feed operations for explicit demos and clearing. Never run those operations during initialization.

The existing `PUT /api/overtime-feed` remains the whole-feed route with its current DTO and control scope. While holding the feed repository's mutation lock, it preserves current `SuperChat` and `ThirdPartyPayment` events omitted from an older client snapshot, following the existing SuperChat protection. Explicit `DELETE /api/overtime-feed` still clears the full feed. This closes the payment-callback race without introducing a new endpoint or changing event payloads.

### Keep visual behavior within the existing system

Use the existing Vuetify theme, components, and overtime animation presets. If the settings page includes a visual preview, keep it compact, polished, and consistent with the animated overlay; honor `prefers-reduced-motion`. No new raster assets or dependencies are needed.

## Risks / Trade-offs

- A credential field could accidentally overwrite an existing secret → keep untouched state separate from the blank password input, send `null` for unchanged values, and test both unchanged and explicit-clear behavior.
- A stale form could overwrite OBS-owned session state → re-read the settings and ETag immediately before saving, then merge only editable fields.
- A stale form could revert a newer planned end → track whether that field changed and preserve the latest server value when it did not.
- A demo/full-feed update could erase a payment callback between read and write → protect concurrently stored payment events in the existing atomic PUT mutation and cover both JSON and SQLite repositories.
- A failed publish after duplication could cause repeat installs → keep the new copy ID available and make retry publish that copy only.
- Reinstall wording could imply replacement while the API creates a copy → duplicate the source, publish only the returned new copy, and test that earlier copies remain unchanged.
- Direct-route refresh might still fall through to Blazor → update the existing Vue fallback allowlist and verify all three paths with Playwright, while keeping each legacy route.
- A demo or clear control could mutate data unintentionally → keep mutations behind explicit buttons and test that page initialization sends no feed writes.

## Migration Plan

1. Add the capabilities and tests in the Vue worktree; do not change persisted data or server APIs.
2. Verify direct routes, auth, normal/empty/error states, mutation safety, and the existing legacy fallback in the Dev Container.
3. If rollback is needed, remove the three Vue route/fallback entries and restore Blazor ownership; no data rollback is required.
4. Keep production cutover, merge, and native OBS validation outside this change.
