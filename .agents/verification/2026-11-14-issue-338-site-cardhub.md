# Issue #338 site-side Card Hub slice (1c)

- Worktree/branch: `.worktrees/issue-338-site-cardhub` / `codex/issue-338-site-cardhub`
- Base revision: PR #345 OpenSpec head `6dd9ab2822cfea52f0c3a84165e873033b68d243`
- Tested working tree: branch above, uncommitted changes (no commit/push/PR)
- Runtime: Dev Container, .NET SDK `10.0.404`, Node `v22.23.5`, Go `1.27.4`, Python `3.12.3`

## Changes

- Added default-off `Shop:Enabled` status endpoint and fail-closed `/api/shop/*` gate (feature status remains readable).
- Added Card Hub link settings API and Vue admin page. Link settings stay available while shop is disabled, as required by the copy deck. Client secret is encrypted with ASP.NET Data Protection, never returned to the browser, and excluded from logs. HTTPS destinations must match an exact `Shop:TrustedHubOrigins` entry before secret decryption/submission; loopback HTTP is development-only; HTTP redirects are disabled.
- Added `hub_link` SQLite migration. With SQLite storage it uses the configured database; with YAML storage it uses a dedicated `Storage:Directory/card-hub.sqlite` file (override `Shop:SiteDatabasePath`).
- Added route and navigation guards for shop-only routes/groups. Card Hub configuration remains visible while disabled.
- Added key-ring documentation: default local path is `Storage:Directory/card-hub-keyring` (override `Security:DataProtection:KeyRingPath`). A same-volume database/key-ring backup is not at-rest protection; production needs a separately protected persistent key ring.
- Product copy keys for the settings page are tracked in the Card Shop copy deck.

## Verification

All .NET and Node commands below ran through `npx --yes @devcontainers/cli@latest exec` against the credential-free Dev Container config.

- `dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --no-restore --filter FullyQualifiedName~CardShop` — passed, 5/5.
- After adding trusted-origin enforcement, the same focused API command — passed, 10/10, 0 failed, 0 skipped; existing `System.Formats.Cbor` NU1902 warning.
- After adding a positive custom-handler request contract test, the focused API command — passed, 11/11, 0 failed, 0 skipped; exact token URL and form body are asserted without network access.
- `npm run typecheck --prefix src/EasyLotteryWeb` — passed.
- `npm test --prefix src/EasyLotteryWeb` — passed, 32 files / 161 tests.
- `npm run build --prefix src/EasyLotteryWeb` — passed; Rollup printed third-party `@microsoft/signalr` annotation warnings only.
- `dotnet test tests/EasyLotteryAPITests/EasyLotteryApiTests.csproj --no-restore --logger 'console;verbosity=minimal'` — passed, 167/167, after Vue build artifacts were available.
- `npx --yes @fission-ai/openspec@1.7.0 validate streamer-card-shop --strict` — passed, change valid.
- `git diff --check` — passed.

## Failed attempts and limits

- The first full API run overlapped the Vue build and failed 4 SPA fallback cases because `/vue/index.html` was not yet built (163 passed, 4 failed). Rebuilding Vue, then rerunning API tests sequentially passed all 167.
- One combined shell command placed `npm test` outside the Dev Container; the host could not load the container-installed Linux Rollup native module. Do not count it as a test result. The corrected in-container command passed 161/161.
- Restore output reported existing `System.Formats.Cbor` moderate advisory NU1902; the initial frontend `npm ci` reported 2 moderate and 2 critical dependency advisories. Dependency upgrades are outside this slice.
- No live Card Hub, email provider, paid API, user account or production environment was used. The Hub token endpoint was represented by a deterministic fake in API tests.
