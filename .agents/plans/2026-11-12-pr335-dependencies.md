# PR #335 dependency compatibility

Source: authorized maintenance of existing PR #335. Stable release v0.0.7 locks Claude planning / Codex implementation. Actual Claude Opus 5.5 session `44dd97b8-1533-4dc0-85af-ecc88ac4dd09` produced the plan; complete evidence retained externally.

Integrate main388f1e9884306a35850ce8a4ebdb2a828534aaf2 preserving #334; align DevServer and WASM SignalR with net10.0 at10.0.15; retain all remaining upgrades. Add actual DevServer smoke in CI. Validate build, Domain/API, Vue, published WASM, production container health, normal E2E, OpenSpec and advisories. Plan's suggestion to create another PR is superseded by the explicit assignment to fast-forward the existing Dependabot branch only. No merge/release/deploy or security-setting changes.

OpenSpec: `align-dotnet10-dependency-runtime`. Detailed acceptance, risk, rollback and observability are in its design/tasks. Frozen repository records are historical evidence, never live approval. Independent actual Codex and Claude review on the immutable full candidate SHA is required, maximum five rounds; authoritative ledger stays outside the tree.
