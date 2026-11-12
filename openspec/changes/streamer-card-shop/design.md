# Design

## Context

EasyLottery is a self-hosted, single-streamer deployment: one admin (Passkey), SQLite storage through `SqliteSchemaMigrator`, Donate payments through `PaymentCallbackProcessor` (ECPay / NewebPay), SMTP through the existing result-notification sender, and Vue 3 admin + OBS pages. The card shop adds a second kind of user (viewers) and a mobile client.

## Decisions

### D1. Tenancy: one deployment = one streamer; the app aggregates deployments (NEEDS OWNER CONFIRMATION)

The owner decided one viewer can collect cards from many streamers. Because each streamer self-hosts, there is no shared server. The app therefore keeps a list of **streamer sites** (added by scanning a QR code or opening a deep link `easylottery://add?site=https://...`) and holds one viewer session per site. Viewer accounts are keyed by the same verified email on every site, so the app presents them as one profile and groups the collection by streamer.

Consequences: no central infrastructure or data sharing between streamers; trades are only possible between viewers of the same streamer site. The alternative, a central "card hub" service operated by the project, would allow cross-streamer trades and one login but introduces a hosted service, operator liability, and shared personal data. This is the main open decision; the schema below works for both because every row is scoped to one site.

### D2. Viewer authentication

Email + 6-digit one-time code (10-minute expiry, 5 attempts, rate-limited per email and IP), issuing a viewer JWT with audience `easylottery-viewer` and a refresh token stored in the app's secure storage. Admin endpoints reject viewer tokens (distinct audience and signing purpose). Viewer Passkeys may be added later. Account deletion anonymizes owned card instances to "unowned/retired" and deletes email and groups.

### D3. Data model (all tables added in one migration in the first implementation step to avoid parallel migration conflicts)

- `shop_products(id, type, status, title, created_at)` - `type` is an open string; only `card_pack` is implemented.
- `card_rarities(id, code, label, sort_order, color)` - streamer-defined tiers, seeded with N/R/SR/SSR/UR.
- `card_sets(id, code, title, status)`
- `cards(id, set_id, serial_no, name, description, rarity_id, image_asset_id, source{upload|ai}, ai_provider, ai_prompt, created_at)` - `serial_no` is `{set.code}-{NNN}`, unique, never reused.
- `card_packs(product_id, set_id, cover_asset_id, odds_json, supply_limit, opened_count)` - `odds_json` maps rarity -> weight; must sum to 100.00 and every rarity with weight > 0 must contain at least one card.
- `card_instances(id, card_id, owner_viewer_id NULL, status{reserved|owned|retired}, origin{pack|trade}, created_at)` - one row per physical copy.
- `pack_draws(id, pack_id, card_instance_id, trigger{donate|admin_test}, payment_event_id NULL, rng_version, drawn_at)`
- `redemption_codes(id, code_hash, card_instance_id, email_hash, status{issued|redeemed|void|expired}, expires_at, redeemed_by_viewer_id, redeemed_at, delivery_status, delivery_attempts)`
- `viewers(id, email, email_verified_at, created_at, deleted_at)`, `viewer_login_codes(...)`, `viewer_groups(id, viewer_id, name)`, `viewer_group_items(group_id, card_instance_id)`
- `card_trades(id, proposer_id, recipient_id, offered_instance_ids, requested_instance_ids, status{proposed|accepted|declined|cancelled|expired}, created_at, resolved_at)`
- `shop_audit_events(id, kind, subject_id, payload_json, at)` - append-only.

### D4. Draw engine

`RandomNumberGenerator` (crypto RNG) selects a rarity by published weights, then a card uniformly within that rarity, inside one SQLite transaction that also checks supply and inserts `card_instances(status=reserved)` and `pack_draws`. Provably-fair commit/reveal (#324) is a later extension; `rng_version` keeps that path open.

### D5. Donate card-pack activity and redemption codes

- The donation form requires an email and an explicit consent checkbox (purpose, retention 180 days after redemption or expiry, deletion contact). No email means the activity is not triggered; the donation itself still succeeds.
- On a confirmed paid callback over the threshold, the processor draws one card (idempotent per `payment_event_id`), publishes an OBS event, issues a redemption code and queues the email.
- Code: 120 random bits, Crockford base32, grouped `XXXX-XXXX-XXXX-XXXX-XXXX-XXXX`; only a SHA-256 hash is stored; default expiry 90 days; redemption is `UPDATE ... SET status='redeemed' WHERE code_hash=? AND status='issued' AND expires_at>now` (single winner); per-viewer and per-IP rate limits; admin can void and re-send (re-send issues a new code and voids the old one).
- Email delivery uses the existing SMTP settings with retry and recorded status.

### D6. Images and AI generation

Uploads reuse the OBS asset storage (PNG/JPEG/WebP, max 10 MB, stored with a 3:4 card crop and thumbnails). AI generation goes through `ICardImageProvider` adapters configured by the streamer (provider, model, encrypted API key, daily/monthly caps): OpenAI Images, Google Gemini/Imagen, Stability AI, FLUX, and an optional OpenAI-compatible endpoint. Claude (text-only) is used through `ICardTextAssistant` for prompt rewriting and card name/description suggestions. Model lists are fetched or configured, never hard-coded. Provider content-policy refusals map to a typed error. The streamer must confirm image rights on upload.

### D7. Pack-opening animation

One Vue component (web + OBS) and one React Native implementation share a stage timeline spec: select -> focus (3-5 s) -> ready flip (2-3 s) -> tear (swipe or auto) -> flash and reveal (1.5-2 s) -> result; rarity controls glow, particles, and sound; skippable; honors reduced motion. OBS plays the automatic variant (8-10 s). Exact visuals follow #341.

### D8. Mobile app (React Native, Expo)

`apps/mobile` with Expo development builds and config plugins for native modules: Android `FLAG_SECURE` on card screens; iOS capture detection (`UIScreen.isCaptured` -> blur overlay, screenshot notification -> audit event). Wallpapers: Android `WallpaperManager` for home/lock; iOS saves to Photos and shows a guided flow. **Default (owner may change):** exported wallpapers are rendered at device resolution with a subtle viewer watermark, because exporting necessarily leaves the protected area. Camera photos cannot be prevented by any app.

### D9. Trading

Same-site only (D1). A trade is accepted inside one transaction that re-checks ownership of every instance and moves `owner_viewer_id`; concurrent trades on the same instance leave exactly one winner. Limits: 20 proposals per viewer per day; streamer can mark sets non-tradable. No currency, prices, or cash-out.

### D10. Compliance

Odds are shown on the shop page and in the app before any donation. Terms state cards have no cash value and must not be sold for money. The privacy policy is updated for email collection, retention, and deletion. App store review notes declare the odds disclosure.

## Delivery waves (each item is a separate branch/worktree/PR)

1. #338 viewer accounts + full schema migration + `Shop:Enabled` flag (blocks everything else).
2. #339 catalog/images/AI adapters || #340 packs and draw engine.
3. #341 animation (Vue + OBS) || #342 Donate card-pack activity, codes, email.
4. #343 React Native app || #344 trading API; then trading UI in the app.

## Open decisions

- D1 tenancy (per-site accounts aggregated by the app vs. a central hub).
- D8 wallpaper export quality/watermark default.
- Redemption code expiry (default 90 days) and email retention (default 180 days).
