# Proposal

## Why

Lottery chat currently treats display names as account identity. This merges different viewers with the same name, can duplicate one account after a rename or case change, and lets same-name users share cooldown state. YouTube already provides stable channel IDs, but the current REST projection drops them before Vue receives chat messages.

## What Changes

- Carry YouTube's stable author channel ID and platform through the existing live-chat response.
- Give lottery participants optional platform/account identity metadata.
- Deduplicate chat participants and enforce cooldown by the stable identity tuple when present.
- Do not infer identity from display names when the stable ID is absent.
- Preserve old JSON/CSV imports and extend exports/imports to round-trip optional identity metadata.
- Retain explicit name-based chat keyword/allow/deny behavior and existing draw rules.

## Capabilities

### New Capabilities

- `lottery-participant-identity`: stable source-scoped identity, conservative ID-less fallback, and compatibility with existing participant formats.

### Modified Capabilities

- YouTube live chat API projection adds optional identity fields without changing endpoint authorization or provider behavior.

## Impact

Affected areas: `src/EasyLotteryAPI/Lottery/LotteryYouTubeClient.cs`, its API contract tests, `src/EasyLotteryWeb/src/lottery/{draw,chat,api,import-export}.ts`, related Vitest tests, and the mocked lottery Playwright spec. No database, identity-link, Passkey, payment, production configuration, or legacy Blazor migration is included.

## Risks and rollback

The API and participant export additions are backward-compatible; older records remain ID-less and are not silently merged. If an upstream message lacks a stable author ID, per-message fallback may leave multiple same-name entries and cannot enforce an attributable per-account cooldown. Reverting the change restores the previous behavior; no server data migration or destructive operation is involved.
