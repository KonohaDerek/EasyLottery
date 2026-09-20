# Multi-platform Interactions Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a safe MVP that unifies YouTube and Twitch chat events into audience identities and runs sign-in, voting, quiz, points, leaderboard, lottery eligibility, and read-only OBS overlays.

**Architecture:** Platform adapters only authenticate, normalize, and deduplicate events. A new domain interaction engine owns identity resolution, rounds, and immutable scoring decisions; the API persists its documents separately from existing settings and broadcasts snapshots through a scoped SignalR hub. The WASM control page and four transparent OBS pages consume that API without putting platform secrets in the browser.

**Tech Stack:** .NET 10, Blazor WASM, ASP.NET Core minimal APIs, SignalR, YAML/SQLite repositories, MSTest, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-19-multiplatform-interactions-design.md`

## Global Constraints

- Implement only Phase 1: YouTube and Twitch; Discord and self-service code linking are excluded.
- Platform API keys, OAuth access/refresh tokens, and webhook secrets are server-side secrets and are always masked in API responses.
- Use `(Platform, ChannelScope, ExternalEventId)` as the immutable idempotency key.
- Use `AudienceProfileId` for every per-round vote, answer, point adjustment, and eligibility decision.
- Existing Donate, PokeBox, Roulette, Overtime, settings, and OBS URLs must remain compatible.
- OBS receives only resource-scoped read tokens and renders transparent idle state when no round is live.
- Every new behavior starts with a failing test; do not deploy platform credentials or production configuration.

## Review Focus

- Replay of the same platform event must never create a second score, vote, or eligibility ticket (Task 2).
- Two linked identities must be treated as one voter and one quiz contestant (Task 2).
- OAuth callback with incorrect state or expired state must create no connection (Task 3).
- A revoked/expired Twitch connection must degrade only Twitch and leave an active YouTube round usable (Task 3).
- OBS reconnect must receive the current round snapshot before later SignalR deltas (Task 5).

## File Structure

- Create `src/EasyLotteryDomain/Models/Interactions/*` for identities, events, rounds, results, and pure rule engine.
- Create `src/EasyLotteryApplication/Interactions/*` for repository contracts and commands/queries.
- Create `src/EasyLotteryInfrastructure/Interactions/*` for YAML/SQLite persistence and migration.
- Create `src/EasyLotteryAPI/Interactions/*` for connectors, endpoints, SignalR hub, OAuth callback, and broadcaster.
- Create `src/EasyLotteryWasm/Pages/Interactions/*` and `src/EasyLotteryWasm/Pages/Obs/Interaction*.razor` for host and overlay experiences.
- Add unit, API, and browser tests alongside existing test conventions.

### Task 1: Persist interaction identities and rounds separately

**Files:**
- Create: `src/EasyLotteryDomain/Models/Interactions/InteractionModels.cs`
- Create: `src/EasyLotteryApplication/Interactions/InteractionRepositories.cs`
- Create: `src/EasyLotteryInfrastructure/Interactions/YamlInteractionRepository.cs`
- Create: `src/EasyLotteryInfrastructure/Interactions/SqliteInteractionRepository.cs`
- Modify: `src/EasyLotteryDomain/Models/Config/YamlRepositoryDocuments.cs`
- Modify: `src/EasyLotteryInfrastructure/Settings/YamlDocumentMigrations.cs`, dependency injection registration
- Test: `tests/EasyLotteryDomainTests/Interactions/InteractionRepositoryTests.cs`

**Interfaces:** Produces `AudienceProfile`, `PlatformIdentity`, `InteractionRound`, `InteractionEvent`, `InteractionDecision`, and `IInteractionRepository` with atomic `ApplyAsync(InteractionEvent, InteractionDecision)`.

- [ ] **Step 1: Write failing persistence and migration tests**
  - Verify a new document has empty interaction collections.
  - Verify legacy YAML/SQLite documents migrate without changing existing settings/activities/results.
  - Verify a saved identity and round survive a repository reload.
- [ ] **Step 2: Run the focused tests and verify RED**
  - Run `dotnet test tests/EasyLotteryDomainTests/EasyLotteryDomainTests.csproj --filter FullyQualifiedName~InteractionRepositoryTests`.
  - Expected: compile/test failure because interaction repository and models do not exist.
- [ ] **Step 3: Implement minimal models and repositories**
  - Define `PlatformKind { YouTube, Twitch }`, `AudienceProfile(Guid Id, string DisplayName, ...)`, `PlatformIdentity(Guid Id, PlatformKind Platform, string ExternalUserId, string ChannelScope, Guid AudienceProfileId, ...)`.
  - Store interactions in a new versioned `InteractionsYamlDocument`, not in `SettingsYamlDocument`.
  - Give SQLite the equivalent tables and unique event key constraint.
- [ ] **Step 4: Run focused tests and verify GREEN**
- [ ] **Step 5: Commit** `feat: persist interaction identities and rounds`.

### Task 2: Add a deterministic interaction rule engine

**Files:**
- Create: `src/EasyLotteryDomain/Services/InteractionRoundEngine.cs`
- Create: `src/EasyLotteryApplication/Interactions/InteractionCommands.cs`
- Test: `tests/EasyLotteryDomainTests/Interactions/InteractionRoundEngineTests.cs`

**Interfaces:** Consumes `InteractionEvent`; produces `InteractionDecision` with `Accepted`, `Reason`, `PointDelta`, `VoteOption`, `EligibilityTickets`, and `RoundState`.

- [ ] **Step 1: Write failing tests**
  - One profile with two linked identities can cast only one vote.
  - Duplicate external event ID yields `Accepted=false, Reason="duplicate"` and no score change.
  - First correct `!answer` scores configured points; a later correct answer is rejected.
  - `!join` grants the configured ticket exactly once per round.
  - Paused and settled rounds reject all audience events.
- [ ] **Step 2: Run RED** with `dotnet test ... --filter FullyQualifiedName~InteractionRoundEngineTests`.
- [ ] **Step 3: Implement minimal pure engine**
  - Support `SignIn`, `Vote`, and `Quiz` round types; parse only configured command prefixes and options.
  - Resolve identities before applying any decision and return immutable decisions suitable for audit.
- [ ] **Step 4: Run GREEN** and run all domain tests.
- [ ] **Step 5: Commit** `feat: add interaction round rules`.

### Task 3: Implement secure YouTube/Twitch connector configuration

**Files:**
- Create: `src/EasyLotteryAPI/Interactions/PlatformConnectionService.cs`, `TwitchOAuthEndpoints.cs`, `YouTubeChatConnector.cs`, `TwitchChatConnector.cs`
- Create: `src/EasyLotteryWasm/Pages/Config/PlatformConnections.razor`
- Modify: `src/EasyLotteryInfrastructure/Settings/ConfigSecretRedactor.cs`, `src/EasyLotteryAPI/Program.cs`
- Test: `tests/EasyLotteryAPITests/Interactions/PlatformConnectionEndpointTests.cs`

**Interfaces:** Produces `PlatformConnectionStatus` and `IPlatformConnector.StartAsync/StopAsync`; accepts server-side `PlatformConnectionSettings` only through an admin endpoint.

- [ ] **Step 1: Write failing API tests**
  - GET returns masked key/token fields and health metadata.
  - Invalid OAuth state and expired state return 400 and persist no connection.
  - A disconnected Twitch connector reports degraded without changing YouTube status.
- [ ] **Step 2: Run RED** with `dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --filter FullyQualifiedName~PlatformConnectionEndpointTests`.
- [ ] **Step 3: Implement minimal configuration and lifecycle**
  - Add admin-only resource endpoints, ETag concurrency, redaction, and audit records.
  - Implement Twitch OAuth state/PKCE callback server-side; use an injectable HTTP client and clock.
  - Implement YouTube polling/stream adapter respecting returned polling interval; do not send chat messages in MVP.
- [ ] **Step 4: Run GREEN** and settings/security API test suites.
- [ ] **Step 5: Commit** `feat: configure live platform connectors`.

### Task 4: Add host control APIs and management page

**Files:**
- Create: `src/EasyLotteryAPI/Interactions/InteractionRoundEndpoints.cs`
- Create: `src/EasyLotteryWasm/Pages/Interactions/InteractionRounds.razor`
- Create: `src/EasyLotteryWasm/Services/InteractionApiClient.cs`
- Modify: `src/EasyLotteryWasm/Layout/NavMenu.razor`
- Test: `tests/EasyLotteryAPITests/Interactions/InteractionRoundEndpointTests.cs`, `tests/e2e/interaction-rounds.spec.mjs`

**Interfaces:** API exposes create/start/pause/settle/cancel, host point adjustment, and eligibility snapshot commands guarded by admin session.

- [ ] **Step 1: Write failing tests** for round lifecycle ordering, rejected audience events after pause, auditable host adjustment, and unauthenticated 401.
- [ ] **Step 2: Run RED** focused API and Playwright tests.
- [ ] **Step 3: Implement endpoints and UI** with templates for sign-in, vote, and quiz; show connection health, round timer, platform inclusion, activity feed, and explicit pause/settle confirmation.
- [ ] **Step 4: Run GREEN**.
- [ ] **Step 5: Commit** `feat: manage interaction rounds`.

### Task 5: Broadcast safe state to OBS overlays

**Files:**
- Create: `src/EasyLotteryAPI/Interactions/InteractionHub.cs`, `InteractionStateBroadcaster.cs`
- Create: `src/EasyLotteryWasm/Pages/Obs/InteractionStatusObs.razor`, `InteractionQuestionVoteObs.razor`, `InteractionLeaderboardObs.razor`, `InteractionNoticeObs.razor`
- Modify: `src/EasyLotteryAPI/Security/ObsResourceKind.cs`, `ObsSessionAccess.cs`, `Program.cs`
- Test: `tests/EasyLotteryAPITests/Interactions/InteractionHubTests.cs`, `tests/e2e/interaction-obs.spec.mjs`

**Interfaces:** Adds `ObsResourceKind.InteractionRound`, read-only scoped token and `InteractionHub.JoinRound(Guid, string)`; event name `InteractionStateChanged` carries a redacted `InteractionRoundSnapshot`.

- [ ] **Step 1: Write failing tests** for token scope rejection, reconnect snapshot delivery, transparent idle rendering, and no platform secret in snapshot JSON.
- [ ] **Step 2: Run RED** focused API/E2E tests.
- [ ] **Step 3: Implement scoped hub and overlays**; send snapshot on join then deltas, preserve reduced-motion and transparent idle conventions.
- [ ] **Step 4: Run GREEN**.
- [ ] **Step 5: Commit** `feat: add interaction OBS overlays`.

### Task 6: Integrate eligibility with existing lotteries and verify the MVP

**Files:**
- Modify: `src/EasyLotteryWasm/Pages/Home.razor`, `src/EasyLotteryAPI/Interactions/InteractionRoundEndpoints.cs`
- Create: `src/EasyLotteryApplication/Interactions/EligibilitySnapshotService.cs`
- Modify: `tests/EasyLotteryDomainTests/Interactions/InteractionRoundEngineTests.cs`, `tests/e2e/interaction-rounds.spec.mjs`
- Create: `.agents/verification/2026-09-19-multiplatform-interactions-phase1.md`

- [ ] **Step 1: Write failing tests** showing an eligibility snapshot imports each profile once, remains stable after later point changes, and does not modify existing manual participants.
- [ ] **Step 2: Run RED**.
- [ ] **Step 3: Implement explicit host-only snapshot import** with preview count, duplicate explanation, audit record, and no automatic draw.
- [ ] **Step 4: Run GREEN** plus `dotnet test EasyLottery.generated.sln`, build, and all Playwright E2E tests against fresh temporary storage.
- [ ] **Step 5: Record results, unresolved production credentials, rollback, then commit** `test: verify interaction MVP`.

## Plan Self-Review

- Coverage: Tasks 1–6 implement every Phase 1 requirement; Phase 2/3 work is explicitly excluded.
- Type consistency: all connectors emit `InteractionEvent`; only the engine yields `InteractionDecision`; broadcaster emits `InteractionRoundSnapshot`.
- Review focus: duplicate events (Task 2), linked identity dedupe (Task 2), OAuth state (Task 3), connector degradation (Task 3), and OBS reconnect (Task 5) have explicit tests.
- No production credentials, platform callbacks, or deployment settings are in scope.
