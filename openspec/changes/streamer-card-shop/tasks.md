# Tasks

## 0. Planning (Claude)
- [x] Record requirements and owner decisions from #337-#344.
- [x] Write proposal, design, and capability specs for the card-shop first phase.
- [x] Owner decisions: central Card Hub (D1), wallpaper at device resolution with watermark (D8).
- [x] Product + development leads record code expiry (90 days) and encrypted email retention (through redemption/expiry plus a maximum 180-day support window); provider and legal-entity choices remain in the operations issue.

## 1. Foundation - #338 (Codex, blocks all later waves; three PRs, see D11)
### 1a. Hub base (PR 1)
- [x] New `src/EasyLotteryCardHub` ASP.NET Core project, PostgreSQL migrations for the wave-1 Hub tables (D11), health checks, Dockerfile, CI job.
- [x] Hub platform admin with a separate fail-closed credential and audit log, invite-only streamer onboarding (email + Passkey assertion login), tenant lifecycle states, site registration, client-credentials tokens with two-secret rotation (24 h overlap) and revocation.
- [x] Tests: invite single use/expiry, credential rotation/revocation, disabled-tenant rejection, streamer vs site token isolation.
### 1b. Viewer accounts (PR 2, after 1a)
- [x] Viewer email-code sign-in (zh-TW email, tenant-aware copy, 60 s resend), viewer JWT audience, refresh tokens, rate limits; 13–17 accounts may register (under 13 rejected). The Hub has no paid-draw entry point in this wave; the 18+ gate ships with the donate flow (3b).
- [x] Terms version/acceptance, age bracket, reserved handle, group create/rename/delete/list, account deletion through the card-retirement seam (`IViewerCardRetirement`, no-op until the instance table exists), synchronous audited `no-store` JSON export of the data that exists in this wave (profile and consent record, groups, session metadata).
- [x] Tests: viewer token rejected by streamer/site/admin APIs, code expiry/attempt limits, deletion, export scope.
- [ ] Production email delivery adapter for viewer sign-in codes. Until the provider is chosen in the operations issue, the registered sender fails closed with 503 outside Development/Testing, so viewer sign-in is not usable in production.
### 1c. Site side (PR 3, parallel with 1a)
- [x] `Shop:Enabled` (default off) gating nav, pages and APIs; site SQLite migration for the D3 site tables.
- [x] Vue "Card Hub 連結" settings page: four states, test connection, write-only secret stored encrypted (entered once, cleared after save, never returned or re-displayed; the Hub issues the secret and returns it once, 1a).
- [x] Tests: flag off hides and blocks, secret never returned by APIs, unreachable vs invalid state.

## 2a. Catalog and images - #339 (Codex)
- [ ] Site: rarity tiers, sets, serialized cards, product type registry with `card_pack`; publish to Hub with versioned snapshots.
- [ ] Image upload with crop/thumbnail; `ICardImageProvider` adapters (OpenAI, Gemini/Imagen, Stability, FLUX, OpenAI-compatible) and `ICardTextAssistant` (Claude); encrypted keys; usage caps.
- [ ] Admin Vue pages; tests with fake providers (no real API calls in CI).

## 2b. Packs and draw engine - #340 (Codex)
- [ ] Site: pack configuration, cover upload, odds validation, publish to Hub, public shop page with odds.
- [ ] Hub: idempotent draw API, atomic crypto-RNG draw with per-rarity and optional total supply, automatic immutable odds-version publication when a rarity is depleted, statistical distribution test, audit records.
- [ ] Hub: with the card-instance table, replace the no-op `IViewerCardRetirement` so account deletion retires owned instances, and add group membership endpoints (add/remove owned instances) with ownership checks and tests; add the viewer's collection and draw history to the JSON export.

## 3a. Pack-opening animation - #341 (Codex)
- [ ] Shared stage timeline spec; Vue component; OBS overlay auto variant; reduced motion; skip; visual regression screenshots.

## 3b. Donate card-pack activity - #342 (Codex)
- [ ] Site: activity type, 18+ declaration + legally reviewed consent on the donation form, Hub draw call with payment-reference idempotency and retry queue, OBS event and pending state.
- [ ] Hub: redemption codes (hash-only storage, 90-day expiry, atomic redeem, rate limit), encrypted recoverable recipient address in delivery outbox with 180-day deletion, email retry, and resend of the same still-valid code to its original address.
- [ ] Stop draws on the prior finite-supply odds version when a rarity is depleted; publish and display the next immutable odds version before resuming. Block production payment handling until the human owner approves recovery for a paid callback that races with the last instance.
- [ ] Hub: add redeemed-code history and the explanation of irreversibly anonymized omissions to the viewer JSON export.
- [ ] Privacy policy update; concurrency and idempotency tests.

## 4a. Mobile app - #343 (Codex)
- [ ] `apps/mobile` Expo project against the Hub API only: viewer sign-in, redeem with animation, collection grouped by streamer/set/rarity plus custom groups.
- [ ] Android phase-one capture protection (FLAG_SECURE) and wallpaper export at device resolution with viewer watermark; native iOS capture, wallpaper export and store behavior are deferred (D8).
- [ ] CI job for lint/typecheck/unit tests; device smoke checklist for the owner.

## 4b. Trading - #344 (Codex)
- [ ] Hub cross-streamer trade API with atomic acceptance, limits, non-tradable sets, audit; app trading UI after the API lands.

## 5. Verification and delivery
- [ ] Each wave: unit/integration/E2E evidence in `.agents/verification/`, Ready PR linked to its issue, no auto-merge or deploy.
