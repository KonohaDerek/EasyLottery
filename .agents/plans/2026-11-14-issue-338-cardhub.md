# Issue #338 Card Hub foundation plan

## Scope

Implement Wave 1 foundation from the current Card Shop OpenSpec on branch `codex/issue-338-cardhub-foundation`, based on PR #345 head `6dd9ab2`. Keep work local and isolated. Do not deploy, use external provider accounts, or add paid services.

## Delivery split

1. Hub base (ASP.NET Core, PostgreSQL data access/migrations, tenant/site credentials, health, CI and security tests).
2. Viewer accounts.
3. Existing EasyLottery site flag, schema migration and Card Hub connection settings.

This first implementation slice is only the Hub base. Viewer account endpoints and site integration remain follow-up slices. Preserve the PR #345 OpenSpec as the source of truth and record any discrepancy before changing behavior.

## Acceptance

- Hub builds under .NET 10 in the Dev Container.
- Tenant/site credentials are isolated; rotation/revocation and disabled-tenant behavior have focused tests.
- PostgreSQL migration/schema and health checks are deterministic; no live credentials are needed for tests.
- No production configuration, account, payment, email provider or paid API is used.

## Verification

- `dotnet build` for the new Hub solution/project in the Dev Container.
- Focused Hub unit/integration tests; PostgreSQL tests use local container only if already available at no cost, otherwise record as not run and use a test double.
- `git diff --check`.

## Rollback and observability

Revert the branch. New Hub migrations are isolated to the new service and no production deployment is performed. Add health endpoints and structured, secret-free auth/tenant lifecycle logs.

## Product decisions

Use only owner decisions already recorded in D1, D11 and D12. Any unresolved product question goes to Claude for a recommendation; no recommendation is represented as owner approval. Human escalation is limited to nonzero cost, production changes, and required user-account operations.
