# Verification: #338 part 1c (site side: Shop flag, site tables, Card Hub link page)

- Base: origin/claude/card-shop-openspec b7d1aab (PR #345). Implemented by Codex (dev lead); reviewed, test-fixed and verified by Claude (product lead) on macOS, .NET SDK 10.0.404.
- `dotnet test EasyLottery.generated.sln`: Domain 112/112 passed, API 167/167 passed.
  - Claude fixed two tests: secret assertion now matches the exact `"clientSecret"` JSON property (the `hasClientSecret` flag is allowed), and the schema version test expects 3.
- `src/EasyLotteryWeb`: `npm run typecheck` passed; `npm test` 166/166 passed; `npm run build` passed.
- `openspec validate streamer-card-shop --strict`: valid. `git diff --check`: passed.
- Not run: e2e Playwright suite; live Hub connection (Hub not built yet, covered by fake HTTP handler tests).

## Follow-up: copy deck wiring (2026-11-14)
- Codex wired copy-deck section 4 (labels, four state descriptions/actions, toasts, rotation note) into `src/shop/copy.ts` and the link page.
- Product fix by Claude: the status description uses the generic "你的實況主帳號" instead of the raw tenant ID until the Hub returns a display name.
- `npm run typecheck` passed; `npm test` passed (see count in PR comment); `npm run build` passed; `git diff --check` passed.
