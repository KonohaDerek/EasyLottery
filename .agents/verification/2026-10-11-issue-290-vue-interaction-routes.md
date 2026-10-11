# Issue #290 verification

Date: 2026-10-11
Branch: `feat/vue-interaction-routes`
Worktree: `.worktrees/vue-interaction-routes`

## Automated checks

- `npm test`: 23 files, 102 tests passed.
- `npm run typecheck`: passed.
- `npm run build`: passed; Rollup emitted only its existing annotation warning from `@microsoft/signalr`.
- `dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --no-restore --filter "FullyQualifiedName~IdentityLinkEndpointTests|FullyQualifiedName~InteractionRoundEndpointTests|FullyQualifiedName~InteractionHubTests"`: 3 passed, 0 failed.
- `npm test -- interaction-rounds.spec.mjs` in the Dev Container after fixing CI run 38150348569: 3 passed, 0 failed. The CI failure was test isolation: the Vue test's `page.route("**/api/interactions/rounds**", ...)` stayed active while the same page navigated to the legacy Blazor route, so the Blazor client received the mock's non-GUID id and failed to deserialize it. The test now removes that route before navigating to `/legacy/interactions/rounds`.
- `node --check tests/e2e/interaction-rounds.spec.mjs`: passed.
- `openspec validate --all --strict`: 42 passed, 0 failed; one unrelated existing informational note is reported for `donate-draw-reveal-sequence`.
- `git diff --check`: passed after the implementation and documentation updates.

## Browser coverage

Focused Playwright verified admin route authorization and REST behavior; safe identity-link error and success presentation; direct Vue refresh and legacy Blazor rollback route; all four OBS modes; live snapshot and empty state; lowercase vote matching; reconnect plus a new event after rejoining the SignalR group; invalid token and missing round messages; paused notice transparency; and reduced-motion behavior.

Manual ego-browser verification also exercised round creation/start, event ingestion, SignalR snapshot rendering, all four OBS modes, invalid token, missing round, and safe identity-code errors.

## Review and remaining manual gate

Independent read-only review findings were fixed and covered by tests: JSON identity-link body binding and one-time replay rejection, case-insensitive vote rendering, no non-live notice panel, and retryable SignalR invoke failures. A previously unregistered Blazor `InteractionApiClient` is now registered so the `/legacy/interactions/rounds` rollback route renders.

Native OBS size/transparency and hardware/browser-source behavior were not tested; the user owns that verification. No production, payment, or data migration was performed.

## Delivery

PR: [#292](https://github.com/KonohaDerek/EasyLottery/pull/292), open and non-draft; it links #252, #269, and #270 and uses `Closes #290`.
GitHub Project #1 #290: In review, with PR #292 linked on the card.
GitHub run [38150348569](https://github.com/KonohaDerek/EasyLottery/actions/runs/38150348569) passed Vue, .NET build, Domain/API tests, container smoke, and OpenSpec; its 70-test browser suite had 69 passes and the single mock-leak failure described above. After the local fix, the focused interaction spec passed 3/3; the updated branch's remote result should be checked on PR #292.
Merge/deployment: not performed.
