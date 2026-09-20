# Discord Connector and Identity Linking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add Discord ingestion and secure cross-platform audience identity linking without changing existing lottery data.

**Architecture:** Extend the existing interaction repository document with Discord connection state, one-time link requests, and immutable audit records. Keep identity/link rules pure in the Domain/Application layers; expose admin and audience endpoints separately, with YAML as default and SQLite as optional.

**Tech Stack:** .NET 10, Minimal API, YamlDotNet, SQLite adapter, SignalR, Blazor WASM, MSTest, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-19-multiplatform-interactions-design.md`

## Global Constraints

- Never expose platform secrets to browser or OBS.
- Link codes are short-lived, hashed at rest, single-use, and replay-safe.
- Unlink changes future eligibility only; historical decisions remain immutable.
- Every repository write must work with YAML default and SQLite optional providers.

## Review Focus

- Expired/replayed link code cannot create a link.
- A profile cannot link two identities with conflicting ownership without host confirmation.
- Discord degradation is isolated from YouTube/Twitch.
- Merge/unlink audit records contain no secrets and preserve source identities.

### Task 1: Link domain and persistence

**Files:** `src/EasyLotteryDomain/Models/Interactions/InteractionModels.cs`, `src/EasyLotteryApplication/Interactions/IdentityLinkService.cs`, repository adapters, domain/API tests.

- [ ] Write failing tests for issue/complete/expire/replay/link/unlink and audit preservation.
- [ ] Implement hashed one-time code, expiry, consumed marker, source/target identities, and merge audit.
- [ ] Run focused domain/API tests and full .NET tests.
- [ ] Commit `feat: add secure identity linking`.

### Task 2: Discord connector and configuration

**Files:** `src/EasyLotteryAPI/Interactions/DiscordChatConnector.cs`, platform settings/UI, connector tests and Playwright flow.

- [ ] Write failing tests for guild/channel validation, source signature/token failure, and isolated degradation.
- [ ] Implement Discord Bot adapter that emits normalized `InteractionEvent` and never evaluates round rules.
- [ ] Add masked Discord configuration and health status beside YouTube/Twitch.
- [ ] Commit `feat: add Discord interaction connector`.

### Task 3: Host merge and audience self-service UI/API

**Files:** interaction endpoints, Blazor linking/host pages, API clients, E2E tests.

- [ ] Add admin search/confirm merge and unlink endpoints with explicit reason.
- [ ] Add audience code issue/complete endpoints with rate limits and safe errors.
- [ ] Add UI for pending code, confirmation, unlink, audit timeline, and Discord disable action.
- [ ] Run focused/full tests and Playwright flow.
- [ ] Commit `feat: manage identity links and Discord`.

### Task 4: Phase 2 verification and delivery

- [ ] Record production credential limits, rollback, and test evidence in `.agents/verification/`.
- [ ] Run build, full .NET tests, Playwright against fresh storage, diff check.
- [ ] Push branch and create Ready PR linked to issue #261.
