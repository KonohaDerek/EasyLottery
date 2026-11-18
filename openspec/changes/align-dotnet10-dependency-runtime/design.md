# Design
DevServer 11.0.0 runtimeconfig targets net11.0 / Microsoft.AspNetCore.App 11.0.0; direct execution on SDK10 fails exit150. Use 10.0.15. SignalR.Client 11 lacks a net10.0 asset and falls back to netstandard2.0 with extra browser polyfills; keep the framework-aligned 10.0.15 client. The five Extensions upgrades and SQLite 11 provide net10.0 assets and remain upgraded, subject to integrated verification. Other patch/minor upgrades remain unchanged. Fido2 4.3.3 brings System.Formats.Cbor 9.0.24.

## Risks and verification
Mixed-version out-of-band libraries require full solution build, Domain/API tests, frontend typecheck/unit/build, normal E2E and production-container health smoke. DevServer smoke starts the actual project and requests its page and framework bootstrap; CI must not rely on restore alone. Preserve passkey lifecycle and decoy regressions. Audit transitive dependencies.

## Observability and rollback
Retain startup logs and test reports externally by candidate SHA. Check /health/live and /health/ready in container verification. Roll back the dependency-update commits if incompatible; no database migration is introduced. No production deployment is authorized.
