# Verification: #338 part 1a (Card Hub base)

- Base: origin/claude/card-shop-openspec b7d1aab (PR #345). Implemented by Codex (dev lead); verified by Claude (product lead) on macOS, .NET SDK 10.0.404, Docker available.
- `npm run build` (src/EasyLotteryWeb, needed by SPA fallback tests): passed.
- `dotnet test EasyLottery.generated.sln`: Domain 112/112, CardHub 12/12 (includes PostgreSQL Testcontainers integration tests), API 162/162 passed.
- `docker build -f src/EasyLotteryCardHub/Dockerfile .`: passed.
- `openspec validate streamer-card-shop --strict`: valid. `git diff --check`: passed.
- Not run: a real browser/authenticator Passkey ceremony (options route covered by tests only); invite email delivery (provider undecided, #346).
- Known limits from Codex: Passkey ceremony state is process-local (needs sticky routing or shared store before multiple replicas); tenant 30-day deletion/image purge belongs to later waves.
