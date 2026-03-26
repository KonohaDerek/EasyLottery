# EasyLottery repository instructions

## Project overview

- EasyLottery is a .NET 8 solution with three main projects:
  - `src/EasyLotteryAPI` for ASP.NET Core APIs
  - `src/EasyLotteryDomain` for core business logic
  - `src/EasyLotteryWasm` for Blazor WebAssembly UI
- Test projects live under `tests/` and use MSTest.

## Working conventions

- Prefer keeping changes aligned with the existing Chinese README and UI wording.
- Make changes in the Domain layer first when behavior spans API and UI.
- Preserve existing behavior unless the request explicitly says to change it.
- Keep code style consistent with the current repository; do not introduce unrelated refactors.

## Build and test

- Use `dotnet test EasyLottery.generated.sln` for the main verification pass.
- Use the API and WASM `dotnet run` commands from the README when manual verification is needed.
- If a change touches only one area, run the smallest relevant test project first.

## Repo structure notes

- Do not assume there is a front-end-only or API-only deployment; this repo includes both.
- When adding settings or shared behavior, check all three layers: API, Domain, and Wasm.
- Treat `openspec/` as the repo's planning and artifact context for issue-first work.

