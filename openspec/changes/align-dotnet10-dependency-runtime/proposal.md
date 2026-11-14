# Align dependency runtime compatibility

## Why
PR #335 upgrades 20 NuGet dependencies. DevServer 11 requires ASP.NET Core 11 even when restore/build on .NET 10 succeeds.

## What Changes
- Keep DevServer and the WASM SignalR client on 10.0.15, aligned with the net10.0 WebAssembly components.
- Retain the other upgrades, including Fido2 4.3.3 and compatible out-of-band Extensions/SQLite 11 packages.
- Add a real DevServer startup and static-asset smoke check to CI.

## Impact
No API, storage schema, target framework, security settings or deployment change. Preserve all #334 changes through a main integration merge.
