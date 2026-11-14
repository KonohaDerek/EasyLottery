# Proposal

## Why

Streamers want a personal shop whose first product type is a collectible card pack. Viewers donate to open a pack on stream, receive a single-use redemption code by email, and claim the card in a mobile app where they collect, group, trade, and use cards as wallpapers. Epic #337 records the original requirements and the owner's decisions of 2026-11-12: React Native app, one viewer may collect from multiple streamers with grouping, and AI image generation through streamer-configured mainstream providers.

## What Changes

- Add a shop product model whose first product type is `card_pack`; other product types are reserved but not implemented.
- Add card sets and cards with a unique serial number, a configurable rarity, and an image that is uploaded or AI-generated.
- Add card packs with a streamer-defined cover, published odds, optional supply limit, and a one-card-per-pack draw that uses a cryptographic RNG and records an auditable draw.
- Add a Donate activity type "card pack": an eligible paid donation with a consented email draws one card, plays a pack-opening OBS animation, and emails a single-use redemption code.
- Add a central **Card Hub** service (owner decision) that holds viewer accounts, the published catalog, draws, card ownership, redemption codes, viewer groups and cross-streamer trades; streamer sites link to it with client credentials.
- Add a React Native (Expo) mobile app: sign in, redeem codes with the pack-opening animation, browse a grouped collection, export wallpapers, and apply capture protection.

## Capabilities

### New Capabilities

- `viewer-accounts`: Hub viewer identity, email-code sign-in, viewer tokens isolated from streamer APIs, account deletion/export.
- `card-hub-federation`: streamer tenants, site linking, catalog publishing, and the Hub draw API used by streamer sites.
- `card-catalog`: shop products, card sets, cards, rarity tiers, image upload and AI generation adapters.
- `card-packs`: pack configuration, published odds, supply, and the auditable draw engine.
- `card-pack-donate-redemption`: the Donate card-pack activity, email consent, single-use redemption codes and email delivery.
- `card-trading`: viewer-to-viewer atomic card trades across streamers on the Hub.
- `card-mobile-app`: React Native client, grouped collection, wallpaper export, capture protection.

### Modified Capabilities

- Donate activities gain one activity type; existing activity types are unchanged.
- The privacy policy page describes email collection for card-pack activities.

## Impact

Streamer site: new SQLite tables via `SqliteSchemaMigrator`, admin endpoints under `/api/shop/*`, public shop pages, a Hub client, new Vue admin pages and an OBS overlay. New `src/EasyLotteryCardHub` service (ASP.NET Core + PostgreSQL + object storage) with its own deployment and operations. New `apps/mobile` React Native project. CI jobs for both new projects. YAML storage is not extended for the new tables; card-shop features require the SQLite provider.

## Risks and rollback

- Legal: donation-for-random-content is a probability-based product in Taiwan; odds are published, cash-out of cards is out of scope and prohibited by terms, and email collection requires consent and a retention policy (see design).
- Platform limits: iOS cannot block screenshots or set wallpapers programmatically; camera photos of a screen cannot be prevented (see design).
- Operations: the Card Hub is a central service holding all viewers' personal data; the owner becomes responsible for its hosting, security, backups and legal terms.
- Rollback: every site feature is behind the `Shop:Enabled` setting (default off); new tables are additive and unused when disabled. Disabling stops new draws and redemptions while preserving data.
