# Issue #338 Card Hub backend slice 1a verification

- Tested revision: `6dd9ab2822cfea52f0c3a84165e873033b68d243` plus the uncommitted worktree changes. No commit, push, PR or deployment was made.
- Worktree: `.worktrees/issue-338-cardhub-foundation`, branch `codex/issue-338-cardhub-foundation`.
- Runtime: approved Dev Container using .NET SDK 10. No local .NET SDK was used.
- Scope: Hub API/data layer, tenant and site credentials, invite/Passkey enrollment and login, encrypted queued-only outbox, health endpoints and CI job. Viewer email-code auth and site Vue/SQLite integration remain separate slices.

## Commands and results

| Check | Command | Result |
| --- | --- | --- |
| Release build | `npx --yes @devcontainers/cli@latest exec --workspace-folder /Users/derek/Documents/Project/EasyLottery/.worktrees/issue-338-cardhub-foundation --config /private/tmp/issue338-container/devcontainer.json sh -lc 'dotnet build EasyLottery.generated.sln --configuration Release --no-restore'` | Passed; 2 NU1902 warnings for the existing System.Formats.Cbor 9.0.0 dependency. |
| Hub tests | `npx --yes @devcontainers/cli@latest exec --workspace-folder /Users/derek/Documents/Project/EasyLottery/.worktrees/issue-338-cardhub-foundation --config /private/tmp/issue338-container/devcontainer.json sh -lc 'dotnet test tests/EasyLotteryCardHubTests/EasyLotteryCardHubTests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"'` | 11 passed, 3 skipped. PostgreSQL migration/credential, invite transaction, and Passkey discoverable-login/counter tests skipped because `CARD_HUB_TEST_CONNECTION` is not configured. |
| Diff whitespace | `git diff --check` | Passed. |

## Limitations

- PostgreSQL migration serialization, tenant deletion SQL guard, invite transactions, and login challenge replay protection were not integration-executed because no test connection was configured. The tests include concurrent/idempotent migration and deletion-guard cases for CI or a local test database.
- Passkey enrollment requires resident credentials. Login options are email-less and contain no credential allow-list; assertion verification identifies the account from the discoverable credential and returns a fresh 15-minute token. The challenge is persisted for five minutes and consumed once. The database-backed assertion/counter path remains unverified here.
- Production startup requires explicit HTTPS Passkey RP ID/origins and a base64 identity HMAC key (at least 32 bytes). Admin, signing and outbox encryption keys are also configuration-only; no production defaults are provided.
- Invite payloads are encrypted in Postgres and marked `queued`; no sender or worker is registered, so emails are not delivered. Provider/account selection and setup remain a gate. No external provider or account was used.
- Tenant deletion requires a 30-day `pending_deletion` period before transition to `deleted`. Image purge and holder notice remain future operational work.
