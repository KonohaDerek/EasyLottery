# Feature Spec: Vue public shell and Passkey login foundation

> This worktree implements GitHub issue #266 under migration epic #252.

## Branch

- Branch: `feat/vue-frontend-phase1`
- Base: `origin/main`
- Worktree: `/Users/derek/Documents/Project/EasyLottery/.worktrees/vue-frontend-phase1`

## Goal

Add the first independently verifiable Vue frontend slice while keeping the existing Blazor frontend and .NET backend available for coexistence and rollback.

## Scope

- Add a Vue 3 + TypeScript + Vite application under `src/EasyLotteryWeb`.
- Use Vue Router, Pinia, Vuetify, and the existing REST/WebAuthn protocol.
- Serve Vue for `/about`, `/privacy-policy`, and `/login` through the API host.
- Keep `/` and all non-migrated routes on the existing Blazor frontend.
- Add client-side email validation and Passkey login/register bridging.
- Add Vitest coverage and public-route Playwright coverage.

## Acceptance

- Vue builds into the API host's static assets and CI validates the build.
- Public routes make no authenticated settings request.
- Login starts blank, rejects malformed email before calling Passkey API, and uses the existing options/verify endpoints.
- Successful Vue login stores the existing session token and returns to the current Blazor home until later phases migrate it.
- Existing Blazor routes and backend protocols remain unchanged.

## Constraints

- Do not purchase, hide, or bypass Blazorise licensing.
- Do not remove `EasyLotteryWasm`, Blazorise, Domain, Payment, YouTube, SMTP, SignalR hubs, or Passkey backend code in this phase.
- Do not introduce a custom API abstraction where a small fetch boundary is sufficient.

## Verification

- `npm ci --prefix src/EasyLotteryWeb`
- `npm run typecheck --prefix src/EasyLotteryWeb`
- `npm run test --prefix src/EasyLotteryWeb`
- `npm run build --prefix src/EasyLotteryWeb`
- `dotnet test EasyLottery.generated.sln --no-restore`
- `dotnet build EasyLottery.generated.sln --no-restore`
- `BASE_URL=http://127.0.0.1:18930 npm test --prefix tests/e2e` with the API in Development mode
