# Tasks
- [x] Obtain actual Claude planning and inspect all upgraded packages.
- [x] Integrate main without losing #334.
- [x] Align DevServer/SignalR and add runtime smoke to CI.
- [ ] Verify restore, build, Domain/API tests, frontend checks and dependency audit.
- [ ] Verify DevServer, published WASM, production container and normal browser suite.
- [ ] Validate OpenSpec and review final immutable SHA with independent Codex and Claude.

Acceptance requires no .NET11 runtime dependency for DevServer, healthy framework assets on .NET10, preserved #334 regressions and passing checks. This task does not authorize merge/release.
