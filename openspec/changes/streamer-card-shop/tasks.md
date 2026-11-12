# Tasks

## 0. Planning (Claude)
- [x] Record requirements and owner decisions from #337-#344.
- [x] Write proposal, design, and capability specs for the card-shop first phase.
- [x] Owner decisions: central Card Hub (D1), wallpaper at device resolution with watermark (D8).
- [ ] Owner decides Hub hosting, domain, email provider, legal entity, code expiry and email retention (operations issue).

## 1. Foundation - #338 (Codex, blocks all later waves)
- [ ] New `src/EasyLotteryCardHub` ASP.NET Core project with PostgreSQL migrations for every Hub table in D3, health checks, Dockerfile, and CI job.
- [ ] Hub streamer accounts (email + Passkey), tenants, site registration and client-credentials tokens with rotation/revocation.
- [ ] Hub viewer email-code sign-in, viewer JWT audience, refresh tokens, rate limits, account deletion/export.
- [ ] Site: `Shop:Enabled` setting (default off), site SQLite migration for D3 site tables, Hub link settings page with encrypted secret.
- [ ] Tests: token isolation (viewer vs streamer vs site), credential revocation, code expiry/attempt limits, deletion.

## 2a. Catalog and images - #339 (Codex)
- [ ] Site: rarity tiers, sets, serialized cards, product type registry with `card_pack`; publish to Hub with versioned snapshots.
- [ ] Image upload with crop/thumbnail; `ICardImageProvider` adapters (OpenAI, Gemini/Imagen, Stability, FLUX, OpenAI-compatible) and `ICardTextAssistant` (Claude); encrypted keys; usage caps.
- [ ] Admin Vue pages; tests with fake providers (no real API calls in CI).

## 2b. Packs and draw engine - #340 (Codex)
- [ ] Site: pack configuration, cover upload, odds validation, publish to Hub, public shop page with odds.
- [ ] Hub: idempotent draw API, atomic crypto-RNG draw with supply limit, statistical distribution test, audit records.

## 3a. Pack-opening animation - #341 (Codex)
- [ ] Shared stage timeline spec; Vue component; OBS overlay auto variant; reduced motion; skip; visual regression screenshots.

## 3b. Donate card-pack activity - #342 (Codex)
- [ ] Site: activity type, email + consent on the donation form, Hub draw call with payment-reference idempotency and retry queue, OBS event and pending state.
- [ ] Hub: redemption codes (hash-only storage, expiry, atomic redeem, rate limit), email delivery with retry, streamer void/re-send.
- [ ] Privacy policy update; concurrency and idempotency tests.

## 4a. Mobile app - #343 (Codex)
- [ ] `apps/mobile` Expo project against the Hub API only: viewer sign-in, redeem with animation, collection grouped by streamer/set/rarity plus custom groups.
- [ ] Capture protection (Android FLAG_SECURE, iOS capture blur and screenshot events), wallpaper export at device resolution with viewer watermark (D8).
- [ ] CI job for lint/typecheck/unit tests; device smoke checklist for the owner.

## 4b. Trading - #344 (Codex)
- [ ] Hub cross-streamer trade API with atomic acceptance, limits, non-tradable sets, audit; app trading UI after the API lands.

## 5. Verification and delivery
- [ ] Each wave: unit/integration/E2E evidence in `.agents/verification/`, Ready PR linked to its issue, no auto-merge or deploy.
