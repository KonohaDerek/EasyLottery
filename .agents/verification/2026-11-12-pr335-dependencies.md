# PR #335 verification snapshot

Task tree: integrated main `388f1e9884306a35850ce8a4ebdb2a828534aaf2` plus dependency compatibility changes. Exact immutable candidate, later CI and dual-review results live in the external SHA-keyed ledger, not this historical snapshot.

## Executed checks
- Original PR: `dotnet restore EasyLottery.generated.sln` succeeds; direct `dotnet /root/.nuget/packages/microsoft.aspnetcore.components.webassembly.devserver/11.0.0/tools/blazor-devserver.dll --help` on SDK10 fails exit150 because ASP.NET Core11 is missing.
- Corrected tree: `dotnet build EasyLottery.generated.sln -c Release --no-restore` passes. Domain112/112 and API162/162 pass (`dotnet test` with Release/no-build). Passkey lifecycle/decoy regressions from #334 retained unchanged.
- `bash tests/smoke-devserver.sh` passes in SDK10, including bounded page/framework requests.
- `dotnet publish src/EasyLotteryWasm -c Release --no-restore` passes; optional wasm-tools optimization not installed, standard SDK publish used.
- Vue locked install, typecheck, unit159/159, build pass.
- OpenSpec strict validation49/49 passes (48 existing +1 new change).
- NuGet transitive vulnerability and deprecation queries report none. Fido2 resolves4.3.3 and CBOR9.0.24; DevServer/SignalR resolve10.0.15. Other upgraded majors retained.
- Production npm audit0; full dev audit4 (2moderate,2critical: @vitest/mocker, tinypool, vite, vitest). Frontend lockfile is identical to main; these are existing toolchain dependencies outside this NuGet PR, not fixed or waived here.
- Official Dockerfile builds with Node22 and .NET10. Production image smoke: /health/live, /health/ready, /, /index.html and /_framework/blazor.webassembly.js pass; image shared frameworks10.0.15.
- Normal E2E completed: 148/148 passed in the API network namespace; confirmed against preserved `e2e-loopback.log` on 2026-11-13. No production/live test suite run.

## Diagnosed harness issues
Initial API run was started before frontend build completed:161passed/1failed (Vue index404). Following CI order (frontend build before solution build) resolves it to162passed. Initial local API harness omitted NuGet staticwebassets mount and then encountered a stale failed port mapping; corrected isolated container uses matching read-only NuGet mount and dedicated loopback port. No application fix was needed for these setup errors. Host-to-Docker bridge E2E then passed147/148; passkey bootstrap was correctly denied for a non-loopback source. Its trace is preserved externally. Browser verification is rerun inside the API network namespace so loopback setup policy remains unchanged. Existing worktrees and production data untouched.

## Limits and gates
No direct legacy Blazor SignalR integration browser coverage is added; retained framework-aligned client is covered by build/publish and overall normal suite. No merge, release or deploy. Required independent Codex/Claude same-SHA review and new GitHub CI remain external delivery gates. Main branch protection baseline is not satisfied and no exception is assumed.
