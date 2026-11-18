# Verification: #338 part 1b (Card Hub viewer accounts)

- Base: codex/issue-338-card-hub-foundation d205668 (PR #350). Implemented by Codex (dev lead); verified by Claude (product lead) on macOS, .NET SDK 10.0.404, Docker available.
- `npm run build` (src/EasyLotteryWeb, needed by SPA fallback tests): passed.
- `dotnet test EasyLottery.generated.sln`: Domain 112/112, CardHub 23/23 (PostgreSQL Testcontainers), API 162/162 passed.
- `docker build -f src/EasyLotteryCardHub/Dockerfile .`: passed.
- Codex also ran `dotnet ef migrations has-pending-model-changes`: no pending changes; 1a migration unchanged.
- `openspec validate streamer-card-shop --strict`: valid. `git diff --check`: passed.
- Product check: sign-in email strings use copy-deck keys (10-minute expiry, "如果不是你本人操作請忽略").
- Not run: real email delivery (provider undecided, #346); the dev sender only logs in Development/Testing.
- Known limits: rate-limit buckets are process-local; group membership writes wait for card-instance tables.
