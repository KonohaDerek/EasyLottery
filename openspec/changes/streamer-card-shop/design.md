# Design

## Context

EasyLottery is a self-hosted, single-streamer deployment: one admin (Passkey), SQLite storage through `SqliteSchemaMigrator`, Donate payments through `PaymentCallbackProcessor` (ECPay / NewebPay), SMTP through the existing result-notification sender, and Vue 3 admin + OBS pages. The card shop adds a second kind of user (viewers) and a mobile client.

## Decisions

### D1. Tenancy: central Card Hub (owner decision 2026-11-12)

The owner chose a **central Card Hub service** operated by the project owner so one viewer account can collect and trade cards across streamers. Responsibilities are split:

| Streamer site (existing self-hosted EasyLottery) | Card Hub (new central service) |
| --- | --- |
| Card authoring: sets, cards, rarity, image upload / AI generation | Viewer accounts, sessions, account deletion/export |
| Pack configuration (cover, odds, supply) and publish | Canonical published catalog (cards, packs, images) per streamer tenant |
| Donate card-pack activity, email + consent capture, OBS animation | Draw engine, card instances and ownership |
| Calls the Hub with a site credential | Redemption codes and email delivery |
| | Cross-streamer trading, viewer groups, audit log, the only API the mobile app uses |

The Hub is a new deployable, `src/EasyLotteryCardHub` (ASP.NET Core, PostgreSQL, object storage for images), with its own CI, deployment, backups, monitoring, privacy policy and terms, tracked in a separate operations issue. The Hub is the data controller for viewer email and ownership data; streamer sites send a donor email to the Hub only after consent.

### D2. Authentication

- **Streamer site -> Hub:** the streamer creates a Hub streamer account (email + Passkey), registers their site, and receives a site client ID/secret stored encrypted in the site's settings. Site calls use OAuth2 client-credentials tokens scoped to that tenant. Secrets can be rotated and revoked from the Hub.
- **Viewer -> Hub:** email + 6-digit one-time code (10-minute expiry, 5 attempts, rate-limited per email and IP), issuing a viewer JWT (audience `card-hub-viewer`) and a refresh token kept in the app's secure storage. Viewer tokens are never accepted by streamer-site or streamer-admin APIs.
- Account deletion anonymizes the viewer's card instances to retired, and deletes email, groups and sessions.

### D3. Data model

Streamer site (SQLite, via `SqliteSchemaMigrator`, one migration in wave 1): `shop_products(id, type, status, title)` where `type` is an open string and only `card_pack` is implemented; `card_rarities`; `card_sets`; `cards(id, set_id, serial_no, name, description, rarity_id, image_asset_id, source, ai_provider, ai_prompt)`; `card_packs(product_id, set_id, cover_asset_id, odds_json, supply_limit)`; `hub_link(site_client_id, encrypted_secret, tenant_id, last_publish_at)`; `hub_publish_log`.

Card Hub (PostgreSQL): `streamers(tenant)`, `streamer_sites`, `catalog_sets`, `catalog_cards`, `catalog_packs` (published snapshots with version numbers; a published card's serial and rarity are immutable), `card_instances(id, card_id, owner_viewer_id NULL, status{reserved|owned|retired}, origin{pack|trade})`, `pack_draws(id, pack_id, instance_id, site_payment_ref, rng_version, drawn_at)` unique on `(tenant, site_payment_ref)`, `redemption_codes`, `viewers`, `viewer_login_codes`, `viewer_groups`, `viewer_group_items`, `card_trades`, `audit_events` (append-only).

Serial numbers are `{tenant-slug}-{set.code}-{NNN}` so they are globally unique across streamers.

### D4. Draw engine (Hub)

The Hub's `RandomNumberGenerator` (crypto RNG) selects a rarity by published weights, then a card uniformly within that rarity, inside one PostgreSQL transaction that also checks supply and inserts `card_instances(status=reserved)` and `pack_draws`. Provably-fair commit/reveal (#324) is a later extension; `rng_version` keeps that path open.

### D5. Donate card-pack activity and redemption codes

- The donation form requires an email and an explicit consent checkbox (purpose, retention 180 days after redemption or expiry, deletion contact). No email means the activity is not triggered; the donation itself still succeeds.
- On a confirmed paid callback over the threshold, the site calls `POST /hub/v1/draws` with the pack, the consented email and its payment reference (idempotency key). The Hub draws one card, issues a redemption code, and queues the email; the site receives the drawn card and publishes the OBS event. If the Hub is unreachable the site queues the request with retry and shows a pending state in OBS and admin.
- Code: 120 random bits, Crockford base32, grouped `XXXX-XXXX-XXXX-XXXX-XXXX-XXXX`; only a SHA-256 hash is stored; default expiry 90 days; redemption is `UPDATE ... SET status='redeemed' WHERE code_hash=? AND status='issued' AND expires_at>now` (single winner); per-viewer and per-IP rate limits; admin can void and re-send (re-send issues a new code and voids the old one).
- Email is sent by the Hub (its own transactional email provider) with retry and recorded status; streamer sites never store redemption codes.

### D6. Images and AI generation

Uploads reuse the OBS asset storage (PNG/JPEG/WebP, max 10 MB, stored with a 3:4 card crop and thumbnails). AI generation goes through `ICardImageProvider` adapters configured by the streamer (provider, model, encrypted API key, daily/monthly caps): OpenAI Images, Google Gemini/Imagen, Stability AI, FLUX, and an optional OpenAI-compatible endpoint. Claude (text-only) is used through `ICardTextAssistant` for prompt rewriting and card name/description suggestions. Model lists are fetched or configured, never hard-coded. Provider content-policy refusals map to a typed error. The streamer must confirm image rights on upload.

### D7. Pack-opening animation

One Vue component (web + OBS) and one React Native implementation share a stage timeline spec: select -> focus (3-5 s) -> ready flip (2-3 s) -> tear (swipe or auto) -> flash and reveal (1.5-2 s) -> result; rarity controls glow, particles, and sound; skippable; honors reduced motion. OBS plays the automatic variant (8-10 s). Exact visuals follow #341.

### D8. Mobile app (React Native, Expo)

`apps/mobile` with Expo development builds and config plugins for native modules: Android `FLAG_SECURE` on card screens; iOS capture detection (`UIScreen.isCaptured` -> blur overlay, screenshot notification -> audit event). Wallpapers: Android `WallpaperManager` for home/lock; iOS saves to Photos and shows a guided flow. **Owner decision:** exported wallpapers are rendered at device resolution with a subtle viewer watermark, because exporting necessarily leaves the protected area. Camera photos cannot be prevented by any app.

### D9. Trading (Hub)

Viewers may trade cards across streamers. A trade is accepted inside one transaction that re-checks ownership of every instance and moves `owner_viewer_id`; concurrent trades on the same instance leave exactly one winner. Limits: 20 proposals per viewer per day; each streamer can mark their sets non-tradable. No currency, prices, or cash-out.

### D10. Compliance

Odds are shown on the shop page and in the app before any donation. Terms state cards have no cash value and must not be sold for money. The privacy policy is updated for email collection, retention, and deletion. App store review notes declare the odds disclosure.

## Delivery waves (each item is a separate branch/worktree/PR)

0. Card Hub operations issue: hosting, domain, PostgreSQL, object storage, email provider, backups, monitoring, terms and privacy policy (owner decisions; blocks production use, not development).
1. #338 Card Hub foundation: new `src/EasyLotteryCardHub` project, streamer tenants and site linking (client credentials), viewer accounts, all Hub tables; site-side `Shop:Enabled` flag and site schema migration.
2. #339 site catalog/images/AI + publish to Hub || #340 site pack configuration + Hub draw engine.
3. #341 animation (Vue + OBS) || #342 Donate card-pack activity on the site calling the Hub draw API, Hub codes and email.
4. #343 React Native app against the Hub || #344 Hub trading API; then trading UI in the app.

## Open decisions

- Card Hub hosting, domain, email provider, and the legal entity named in the Hub terms and privacy policy.
- Redemption code expiry (default 90 days) and email retention (default 180 days).
- D8 wallpaper default confirmed by the owner: device resolution with a subtle viewer watermark.
