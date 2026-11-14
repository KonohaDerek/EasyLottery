# Issue #338 site-side slice

## Scope

Implement Wave 1 site integration only on `codex/issue-338-site-cardhub`, from PR #345 OpenSpec head `6dd9ab2`. Add the default-off `Shop:Enabled` gate, SQLite site schema migration, and Vue Card Hub connection settings flow specified by D3, D11 and the copy deck. Do not implement Hub APIs or viewer accounts in this slice.

## Acceptance

- Flag off hides all shop navigation/pages and blocks shop APIs.
- Existing data upgrades safely through `SqliteSchemaMigrator`.
- Connection settings distinguish not-linked, linked, invalid credential and unreachable Hub.
- Site secret is encrypted at rest and never returned to clients; only newly issued credentials are shown once.
- Focused API/UI tests cover gating, secret redaction and connectivity states.

## Constraints

- No production environment, provider account, email service or paid API.
- Run .NET and Node commands in the Dev Container, using `.devcontainer/local/devcontainer.json` from the separate Dev Container worktree if needed.
- Preserve the current OpenSpec and note material gaps before changing behavior.
