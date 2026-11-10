# Tasks

Status legend: `[x]` = implemented with tests written by Claude; **no test, build, typecheck or OpenSpec command has been executed** (Claude was not permitted to run commands). Codex verification is still required for every item.

## 1. Contract and stable source data

- [x] 1.1 Extend eligibility preview/import response with stable snapshot ID, round name/status and the profile identity projection needed by an admin-only core lottery import; keep credentials/chat text out.
  - `EligibilityPreview` gains optional `SnapshotId`, `RoundName`, `RoundStatus`, `SnapshotCreatedAtUtc`, `Entries` (profile ID, display name, `platform`/`accountId` identities). Existing positional fields keep their meaning. Import rejects Cancelled and empty rounds with HTTP 409 without persisting a snapshot; imports are serialized in-process.
- [x] 1.2 Add optional source round/snapshot/profile identity to core lottery participants and immutable draw participant snapshots; include fields in normalization, copy, idempotency comparison, validation and authorized history reads.
- [x] 1.3 Add .NET tests for empty/missing/unsupported rounds, repeated import, snapshot stability, profile merge/unlink after snapshot, old history compatibility and authorization.
  - `tests/EasyLotteryAPITests/CoreLotteryInteractionEligibilityTests.cs` (self-contained `WebApplicationFactory<Program>` harness; not executed).

## 2. Preview, explicit import and deterministic conflicts

- [x] 2.1 Add Vue round selection/preview with eligible count, added/duplicate/conflict counts and explicit confirmation.
- [x] 2.2 Import using AudienceProfile ID, preserve provenance, dedupe stable identity idempotently, keep same-name/unlinked manual rows separate, and make import errors atomic/retryable.
- [x] 2.3 Cover duplicate profile, linked manual identity, same display name with different identity, empty snapshot, expired/missing round, API failure and rapid repeated clicks in Vitest.
  - Pure-logic Vitest coverage in `src/lottery/interaction-eligibility.test.ts`. Missing-round and API-failure UI paths and the pending-button guard are covered in Playwright, not Vitest (no Vue component test harness exists in the project).

## 3. Locked pool and draw history

- [x] 3.1 Add a versioned, hashed sessionStorage locked-pool snapshot; restore after reload and reject corrupt/version-mismatched snapshots.
- [x] 3.2 Disable every participant/prize/chat mutation during lock; add explicit unlock that does not mutate existing pending draws or saved history.
- [x] 3.3 Include source provenance and lock-time snapshot in draw payload/history; verify profile merge/unlink/name changes do not rewrite old draw evidence or appear on public OBS surfaces.
  - Provenance in payload/history and merge/unlink immutability: implemented and tested.
  - Codex follow-up A: optional `poolSnapshotVersion` (`lockHash`, `lockedAtUtc`) added to the draw request, `CoreLotteryDrawRecord`, and admin summary/detail; sent on every locked prepared/selected/result payload, preserved across reload/reconcile/retry, validated, copied and compared in API/repository idempotency; null for unlocked/legacy draws. Tests: .NET `DrawRecordEndpoint_PersistsPoolSnapshotVersion_IdempotencyIncludesIt_AndLegacyReadsNull`, Vitest `records-provenance.test.ts`/`locked-pool.test.ts`, Playwright payload/history assertions.
  - Traceability presentation: the paginated `GET /api/lottery-draw-records` summary now includes `PoolSnapshotVersion`; the admin history detail dialog (`LotteryDrawRecordsView.vue`) shows lock hash and lock time (em dash for unlocked/legacy) and an interaction-source column with round ID, snapshot ID and AudienceProfile ID (em dash for manual rows). Tests: .NET list assertion in the pool-version test; Playwright detail assertions for locked/imported and unlocked/manual draws.
  - OBS non-exposure basis (scoped inspection): every history endpoint in `CoreLotteryDrawEndpoints.cs` (list, detail, summary, participants, record) calls `ObsSessionAccess.RequireAdmin` before reading or writing, and the history DTOs (`CoreLotteryDrawSummary`, `CoreLotteryDrawHistoryPage`, `CoreLotteryDrawParticipantPage`, `RecordCoreLotteryDrawRequest`) are `internal` records declared in that file and only returned by those endpoints. Public OBS endpoint files were not read or searched; Codex should confirm with a repository search that no OBS response references these DTOs or `CoreLotteryDrawInteractionSource`/`CoreLotteryDrawPoolSnapshotVersion`. No automated test asserts OBS non-exposure.
- [x] 3.5 Codex follow-up B: link manual entries only on platform + channelScope + accountId (`PlatformIdentity.EventScopeKey`); missing or different scope is reported as an identity conflict and never auto-linked; `channelScope` carried through the admin eligibility projection, imported participants and locked pools; older scope-less identities remain readable. Tests: .NET `Preview_ProjectsChannelScope_SameAccountIdUnderDifferentScopesStaysDistinct`; Vitest in `interaction-eligibility.test.ts` and `locked-pool.test.ts`. Draw-history `externalIdentity` still stores platform/accountId only.
- [x] 3.4 Add Vitest and Playwright coverage for lock, blocked mutations, reload, unlock, stale/corrupt storage, history provenance, and unchanged manual draw rules.
  - `src/lottery/locked-pool.test.ts`, `src/core-lottery-records/records-provenance.test.ts`, `tests/e2e/core-lottery-eligibility.spec.mjs`. Existing manual-draw suites were not modified.

## 4. Verification and delivery

- [ ] 4.1 Run focused .NET, Vue unit, TypeScript/build, Playwright tests and full relevant regression suites.
- [ ] 4.2 Run `openspec validate --all --strict`, `git diff --check`, Codex and Claude independent review on the same final SHA; record exact evidence and limits in `.agents/verification/`.
- [ ] 4.3 Push branch and create Ready-for-review PR linked to #305 only after reviewer approval; do not merge/deploy.
