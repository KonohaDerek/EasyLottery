# Issue #338 slice 1b viewer accounts verification

- Worktree/branch: `.worktrees/issue-338-viewer-accounts` / `codex/issue-338-viewer-accounts`
- Base: `97d3ce266268c5a58b3d717517b952a3b8770bdc`
- Source: approved Card Shop plan, OpenSpec `streamer-card-shop` D2/D11, and issue #338 slice 1b.
- Scope: viewer code sign-in, viewer-specific access tokens and rotating refresh tokens, age/terms, handle/group management, owned-card boundaries, account deletion, audited JSON export, and encrypted queued-only email payloads.
- Non-goals: live email delivery or provider setup, payment/draw implementation, production migration, push/PR, or remote GitHub changes.

## Checks

- `npx --yes @devcontainers/cli@latest up --workspace-folder /Users/derek/Documents/Project/EasyLottery/.worktrees/issue-338-viewer-accounts --config /Users/derek/Documents/Project/EasyLottery/.worktrees/devcontainer-card-shop-20261114/.devcontainer/local/devcontainer.json`: passed; created the Dev Container for this worktree from the checked-in local configuration. Runtime is Dev Container only; .NET SDK was not installed locally.
- `npx --yes @devcontainers/cli@latest exec --workspace-folder /Users/derek/Documents/Project/EasyLottery/.worktrees/issue-338-viewer-accounts --config /Users/derek/Documents/Project/EasyLottery/.worktrees/devcontainer-card-shop-20261114/.devcontainer/local/devcontainer.json dotnet test tests/EasyLotteryCardHubTests/EasyLotteryCardHubTests.csproj --no-restore --filter FullyQualifiedName~Viewer`: passed, 2 passed, 2 skipped, 0 failed. The two database integration tests skipped because `CARD_HUB_TEST_CONNECTION` is not configured. Compilation passed. NuGet emitted existing NU1902 for `System.Formats.Cbor` 9.0.0 (moderate).
- `npx --yes @devcontainers/cli@latest exec --workspace-folder /Users/derek/Documents/Project/EasyLottery/.worktrees/issue-338-viewer-accounts --config /Users/derek/Documents/Project/EasyLottery/.worktrees/devcontainer-card-shop-20261114/.devcontainer/local/devcontainer.json openspec validate streamer-card-shop --strict`: passed, change valid.
- `git diff --check`: passed.

## Implementation and verification limits

- Email payloads remain encrypted and queued only. No provider, email account, or credentials were used; sign-in is not end-to-end until a sender exists.
- Expired email codes are removed one day after expiry by the hourly Hub retention service; pending encrypted messages are cancelled on expiry and removed with the code. This cleanup path is not integration-tested without PostgreSQL.
- Viewer endpoints set `Cache-Control: no-store`, `Pragma: no-cache`, and `Expires: 0`, including errors and auth/session responses.
- Independent review is pending for the final immutable SHA. PostgreSQL integration tests are pending a disposable test database. Claude reviewer tooling is not available in this session.
- No commit, push, PR, production operation, or real user/provider account access was performed.
