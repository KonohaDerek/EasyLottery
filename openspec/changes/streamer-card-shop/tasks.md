# Tasks

## 0. Planning (Claude)
- [x] Record requirements and owner decisions from #337-#344.
- [x] Write proposal, design, and capability specs for the card-shop first phase.
- [ ] Owner confirms D1 tenancy, D8 wallpaper default, and code expiry/retention defaults.

## 1. Foundation - #338 (Codex, blocks all later waves)
- [ ] Add `Shop:Enabled` setting (default off) and gate all new endpoints and pages.
- [ ] Add one SQLite migration creating every table in design D3; YAML provider reports the feature as unsupported.
- [ ] Viewer email-code sign-in, viewer JWT audience, refresh tokens, rate limits, account deletion/export.
- [ ] Tests: auth isolation (viewer token rejected by admin APIs), code expiry/attempt limits, deletion.

## 2a. Catalog and images - #339 (Codex)
- [ ] Rarity tiers, sets, serialized cards, product type registry with `card_pack`.
- [ ] Image upload with crop/thumbnail; `ICardImageProvider` adapters (OpenAI, Gemini/Imagen, Stability, FLUX, OpenAI-compatible) and `ICardTextAssistant` (Claude); encrypted keys; usage caps.
- [ ] Admin Vue pages; tests with fake providers (no real API calls in CI).

## 2b. Packs and draw engine - #340 (Codex)
- [ ] Pack configuration, cover upload, odds validation, publish, public shop page with odds.
- [ ] Atomic crypto-RNG draw with supply limit; statistical distribution test; audit records.

## 3a. Pack-opening animation - #341 (Codex)
- [ ] Shared stage timeline spec; Vue component; OBS overlay auto variant; reduced motion; skip; visual regression screenshots.

## 3b. Donate card-pack activity - #342 (Codex)
- [ ] Activity type, email + consent on the donation form, idempotent draw per payment event, OBS event.
- [ ] Redemption codes (hash-only storage, expiry, atomic redeem, rate limit), email delivery with retry, admin void/re-send.
- [ ] Privacy policy update; concurrency and idempotency tests.

## 4a. Mobile app - #343 (Codex)
- [ ] `apps/mobile` Expo project, site list via QR/deep link, viewer sign-in, redeem with animation, grouped collection.
- [ ] Capture protection (Android FLAG_SECURE, iOS capture blur and screenshot events), wallpaper export per D8.
- [ ] CI job for lint/typecheck/unit tests; device smoke checklist for the owner.

## 4b. Trading - #344 (Codex)
- [ ] Trade API with atomic acceptance, limits, non-tradable sets, audit; app trading UI after the API lands.

## 5. Verification and delivery
- [ ] Each wave: unit/integration/E2E evidence in `.agents/verification/`, Ready PR linked to its issue, no auto-merge or deploy.
