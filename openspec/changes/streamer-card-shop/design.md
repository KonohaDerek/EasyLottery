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
- Native streamer and viewer accounts are separate principals. The platform-admin credential is configuration-only, distinct from viewer and site credentials, fails closed when missing, and is never issued to a user through this service. Streamer invite enrollment uses Passkeys; an expiring session must be renewable only after a verified Passkey assertion.

### D3. Data model

Streamer site (SQLite, via `SqliteSchemaMigrator`, one migration in wave 1): `shop_products(id, type, status, title)` where `type` is an open string and only `card_pack` is implemented; `card_rarities`; `card_sets`; `cards(id, set_id, serial_no, name, description, rarity_id, image_asset_id, source, ai_provider, ai_prompt)`; `card_packs(product_id, set_id, cover_asset_id, odds_json, supply_limit)`; `hub_link(site_client_id, encrypted_secret, tenant_id, last_publish_at)`; `hub_publish_log`.

Card Hub (PostgreSQL): `streamers(tenant)`, `streamer_sites`, `catalog_sets`, `catalog_cards`, `catalog_packs` (published snapshots with version numbers; a published card's serial and rarity are immutable), `card_instances(id, card_id, owner_viewer_id NULL, status{reserved|owned|retired}, origin{pack|trade})`, `pack_draws(id, pack_id, instance_id, site_payment_ref, rng_version, drawn_at)` unique on `(tenant, site_payment_ref)`, `redemption_codes`, `viewers`, `viewer_login_codes`, `viewer_groups`, `viewer_group_items`, `card_trades`, `audit_events` (append-only).

Serial numbers are `{tenant-slug}-{set.code}-{NNN}` so they are globally unique across streamers.

### D4. Draw engine (Hub)

The Hub's `RandomNumberGenerator` (crypto RNG) selects a rarity by published weights, then a card uniformly within that rarity, inside one PostgreSQL transaction that also checks supply and inserts `card_instances(status=reserved)` and `pack_draws`. Provably-fair commit/reveal (#324) is a later extension; `rng_version` keeps that path open.

### D5. Donate card-pack activity and redemption codes

- The donation form requires an email and explicit, legally reviewed consent (purpose, retention, deletion contact) plus an 18+ declaration. A minor's donation still succeeds but does not trigger a paid card-pack draw. No email or consent means the activity is not triggered.
- On a confirmed paid callback over the threshold, the site calls `POST /hub/v1/draws` with the pack, the consented email and its payment reference (idempotency key). The Hub draws one card, issues a redemption code, and queues the email; the site receives the drawn card and publishes the OBS event. If the Hub is unreachable the site queues the request with retry and shows a pending state in OBS and admin.
- Code: 120 random bits, Crockford base32, grouped `XXXX-XXXX-XXXX-XXXX-XXXX-XXXX`; only a SHA-256 hash is stored; expiry is 90 days; redemption is `UPDATE ... SET status='redeemed' WHERE code_hash=? AND status='issued' AND expires_at>now` (single winner); per-viewer and per-IP rate limits. An admin may resend the same code only while it remains valid, to the original consented email, without changing its expiry. Expired codes are not reissued in Wave 1.
- Email is sent by the Hub with retry and recorded status. The recipient address and any recoverable code copy are retained only in encrypted delivery data for delivery and support, and deleted from databases, outboxes, logs and backups no later than 180 days after the later of redemption or code expiry. The redemption record stores only the code hash; streamer sites never store redemption codes.

### D6. Images and AI generation

Uploads reuse the OBS asset storage (PNG/JPEG/WebP, max 10 MB, stored with a 3:4 card crop and thumbnails). AI generation goes through `ICardImageProvider` adapters configured by the streamer (provider, model, encrypted API key, daily/monthly caps): OpenAI Images, Google Gemini/Imagen, Stability AI, FLUX, and an optional OpenAI-compatible endpoint. Claude (text-only) is used through `ICardTextAssistant` for prompt rewriting and card name/description suggestions. Model lists are fetched or configured, never hard-coded. Provider content-policy refusals map to a typed error. The streamer must confirm image rights on upload.

### D7. Pack-opening animation

One Vue component (web + OBS) and one React Native implementation share a stage timeline spec: select -> focus (3-5 s) -> ready flip (2-3 s) -> tear (swipe or auto) -> flash and reveal (1.5-2 s) -> result; rarity controls glow, particles, and sound; skippable; honors reduced motion. OBS plays the automatic variant (8-10 s). Exact visuals follow #341.

### D8. Mobile app (React Native, Expo)

`apps/mobile` with Expo development builds and config plugins for native modules: Android `FLAG_SECURE` on card screens and Android wallpaper export. Native iOS capture handling, wallpaper export, and store submission are deferred until the payment model is reviewed. Shared React Native code remains portable to iOS and iOS Safari web flows stay supported. **Owner decision:** exported Android wallpapers are rendered at device resolution with a subtle viewer watermark, because exporting necessarily leaves the protected area. Camera photos cannot be prevented by any app.

### D9. Trading (Hub)

Viewers may trade cards across streamers. A trade is accepted inside one transaction that re-checks ownership of every instance and moves `owner_viewer_id`; concurrent trades on the same instance leave exactly one winner. Limits: 20 proposals per viewer per day; each streamer can mark their sets non-tradable. No currency, prices, or cash-out.

### D11. Wave 1 product decisions (product lead + dev lead, 2026-11-14)

All user-facing zh-TW strings come from `copy-deck.md` (stable i18n keys). Strings marked [需法務確認] need legal review before production.

- Streamer onboarding is invite-only in beta: a Hub platform admin issues a single-use, time-limited invite bound to the recipient email (stored hashed, redeemed atomically); the streamer then registers with email + Passkey. `Streamer:SelfServeSignupEnabled` exists, separate from `Shop:Enabled`, and defaults to off.
- Tenant lifecycle: `active | disabled | pending_deletion | deleted`. A disabled tenant's cards stay visible read-only to holders; the Hub rejects new publishes, draws and trades for it. Deletion starts a 30-day notice to holders using Hub and redemption-page banners, in-Hub and email notices to account holders, and email to holders of unredeemed codes only where service-notice consent allows it; notice delivery is recorded and the streamer receives prepared text to post. After 30 days images are purged; card instances keep name, serial and rarity and render a "removed" placeholder. Card instances and serials are never cascade-deleted or reused. Tenant status is checked on every API call, and site access tokens are short-lived.
- Viewer sign-up stores `terms_version_accepted`, `terms_accepted_at` and a self-declared `age_bracket` (`13_17 | 18_plus`; under 13 cannot register). No birthdate is stored. Ages 13–17 may collect and redeem gifted cards but cannot access paid pack entry points in Wave 1. No Wave 1 copy or behavior claims guardian consent.
- Viewers have a reserved `handle` (nullable, unique, case-insensitive). The UI to set it ships with trading (#344). Email is never shown to other viewers.
- Sign-in email is zh-TW first, names the streamer tenant when sign-in starts from a streamer context, and states the expiry. The server enforces a 60-second resend cooldown.
- Site credential rotation keeps the current and the previous secret, and the previous one expires automatically after 24 hours. Either secret can be revoked immediately. The Hub stores only secret hashes; the site stores its secret encrypted. The Hub link page shows `not linked | linked | credentials invalid | Hub unreachable`, so a network failure is never reported as invalid credentials. It also has a "test connection" action and shows a new secret only once.
- `Shop:Enabled=false` hides every shop navigation item and blocks the shop pages and APIs.
- Wave 1 export is a synchronous, authenticated JSON download of the viewer's own account and consent records, groups, collection, draws and redeemed-code history with `Cache-Control: no-store`; each request is audited. It excludes other viewers' data and fraud signals and explains irreversibly anonymized omissions. Trade history joins the export as that table lands.
- Hub tables land with the wave that uses them: wave 1 creates the tenant, site, credential, invite, viewer, login-code, group and audit tables. Catalog, draw, redemption and trade tables arrive with #339, #340, #342 and #344.

### D12. Research-driven product decisions (product lead, 2026-11-14; evidence in research-ux.md)

- Odds integrity: every published pack has an immutable `odds_version`; each draw records that version. Draw records are kept at least 180 days after the activity ends. If any rarity in a finite-supply pack version is depleted, sales for that version stop. A streamer must publish a new version with new odds before sales resume. Odds are always shown as percentages. If a paid callback races with the final available instance, the draw remains unresolved and issues neither another card nor a redemption code until an approved payment recovery procedure resolves it. The refund/chargeback policy is an operational decision and must be approved before production payments.
- Donate page (#342): only viewers who declare `18_plus` may enter paid pack flows in Wave 1; viewers aged 13–17 may collect and redeem gifted cards. Do not claim guardian consent. Age declaration and any withdrawal-right consent wording remain [需法務確認]. No guardian-requested refund or chargeback automation is specified until the owner approves a recovery policy.
- Onboarding (#342/#343): the redemption email carries one universal/app link that opens the redeem screen with the code prefilled, plus a web preview of the pack. The app never reads the clipboard by itself; it offers a paste button instead. The 24-character code remains the fallback.
- Pack opening (#341): a "quick reveal" preference that is remembered per viewer; no casino or slot-machine imagery; after the reveal, the next actions are share (watermarked) and set as wallpaper on Android phase one.
- Capture protection (#343): explained once on first use; a watermarked share action is offered right where the block applies; marketing never claims "screenshot-proof" (iOS can only detect screenshots).
- Trading (#344): a side-by-side review screen showing rarity and serial, hold-to-confirm, offers that expire after 72 hours, a trade hold only for new accounts or new devices, no free-text trade chat, and monitoring of one-way transfer patterns plus an account-recovery support process.
- **Owner decision (2026-11-14): Android first.** The first app release targets Google Play with in-app code redemption. iOS is deferred until counsel and an App Store pre-review settle the payment model, because Apple guidelines 3.1.1 / 3.1.3(b) / 3.2.1(vii) put in-app redemption of web-donated packs at risk. The RN code stays cross-platform, but iOS store submission is out of scope for #343 phase 1.

### D13. Wave 2 product decisions (product lead, 2026-11-14; screens in ux-spec-339.md / ux-spec-340.md)

- Supply: each pack may set a quantity per rarity and an optional total. When a rarity's quantity runs out, its weight is removed, the remaining odds are renormalized, and a new `odds_version` is published automatically before the next draw. A pack whose total runs out stops selling.
- `odds_version` increments only when odds or supply change what a viewer can draw. A title, description or cover change republishes the pack without a new odds version.
- Rarity tiers have an immutable key once any card using them is published; display name and color may be edited later and are republished.
- A published card is never deleted. "停止發行" (stop issuing) is the only removal, and it excludes the card from future draws. Serials are never reused.
- AI providers: an OpenAI-compatible endpoint may point at a private or LAN host, since generation runs on the self-hosted streamer site. Only the admin can set it. Usage caps reset by the site's time zone (default Asia/Taipei).
- Packs link to a Donate activity in #342. Until #342 ships, the public shop page shows packs and odds but no Donate button.
- When the Hub is unreachable, the shop shows the last published odds with an "最後更新" time and disables donating; no sale happens without current odds.
- Draw history shows a masked donor email (for example `a***@gmail.com`) [需法務確認]. CSV export is audited, and there is no delete action.

### D10. Compliance

Odds are shown on the shop page and in the app before any donation. Terms state cards have no cash value and must not be sold for money. The privacy policy is updated for email collection, retention, and deletion. App store review notes declare the odds disclosure.

## Delivery waves (each item is a separate branch/worktree/PR)

0. Card Hub operations issue: hosting, domain, PostgreSQL, object storage, email provider, backups, monitoring, terms and privacy policy (owner decisions; blocks production use, not development).
1. #338 Card Hub foundation: new `src/EasyLotteryCardHub` project, streamer tenants and site linking (client credentials), viewer accounts, all Hub tables; site-side `Shop:Enabled` flag and site schema migration.
2. #339 site catalog/images/AI + publish to Hub || #340 site pack configuration + Hub draw engine.
3. #341 animation (Vue + OBS) || #342 Donate card-pack activity on the site calling the Hub draw API, Hub codes and email.
4. #343 React Native app against the Hub || #344 Hub trading API; then trading UI in the app.

## Open decisions

- Card Hub hosting, domain, email provider, and the legal entity named in the Hub terms and privacy policy (operations work; no provider or paid service is selected here).
- The payment recovery procedure for a paid callback that cannot receive a card because finite supply is exhausted, including any refund behavior. This blocks production payment handling and needs the human owner because it may cause a monetary transaction.
- Legal review of age declaration, digital-content withdrawal-right language, privacy notices and service-notice consent before production.
- Native iOS release and payment acceptance after counsel and App Store pre-review; Android is the phase-one target.
