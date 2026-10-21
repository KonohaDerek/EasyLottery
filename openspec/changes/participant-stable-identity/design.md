# Design

## Identity representation

Add optional `identity: { platform, accountId }` to the in-memory Vue participant. Normalize `platform` using trim + lowercase; trim but otherwise preserve `accountId` exactly because provider identifiers are opaque and may be case-sensitive. A usable identity requires both values.

The YouTube API DTO exposes `platform: "youtube"` and `authorId`, sourced first from `AuthorDetails.ChannelId` and then from `Snippet.AuthorChannelId`. The client endpoint itself is YouTube-scoped, so it can supply the platform fallback for older server responses. No provider API key, channel message body, or account identity is logged.

## Deduplication and cooldown

Use an unambiguous tuple key for participant identity and cooldown. Do not compare names when a participant or message has no stable identity. Every accepted ID-less message creates an ordinary participant without identity; it is not merged with another ID-less or manual participant. For cooldown, do not record an account-level timestamp for an ID-less event, so the application does not falsely attribute a different viewer's message to the same person.

Keyword and allow/deny rules remain explicit display-name rules for compatibility. They are separate from participant identity and cooldown keys.

Cross-platform accounts remain distinct because platform is part of the tuple. The existing verified identity-link service is not changed or implicitly consulted; this lottery issue does not create a new link or merger.

## Import/export compatibility

Participant JSON and CSV accept optional `Platform` and `AccountId` fields. New exports include those fields when present; CSV includes additive empty columns for records without identity. Older JSON and CSV without those fields continue to parse with an undefined identity.

For YouTube Studio member CSV, parse a stable account ID only from a canonical HTTPS YouTube `/channel/UC…` profile URL. A mutable `@handle`, invalid URL, other host, or unknown profile path yields no identity rather than a guessed ID. Weighted ticket rows from one imported member share the same lottery group and platform identity.

## Verification, observability, rollback

Vitest covers identity normalization, same/different identities, platform isolation, renamed/case-varied names, ID-less fallback, cooldown boundaries, old/new JSON/CSV, and Studio URL parsing. API tests verify the additive response contract. Playwright uses intercepted API responses only and verifies the Vue participant list behavior without contacting YouTube.

No migration is required. Existing chat state is in memory and old exports remain importable. Existing chat status and API errors remain the only runtime signals. Rollback is a source revert.
