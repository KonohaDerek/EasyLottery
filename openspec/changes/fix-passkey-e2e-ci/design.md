# Design

## Context

See `proposal.md` - Why. Passkey state is file-backed in `Storage:Directory/admin-passkey.json`; Playwright retries reuse the same API process and storage. Authentication endpoints have a production fixed-window limit of five requests per minute. YouTube settings are returned as an extensible JSON object with an ETag, and the existing retry test currently continues requests to the real test API.

## Goals / Non-Goals

**Goals:** deterministic E2E attempts, real browser-generated WebAuthn ceremonies against the API, no persistence from the YouTube retry test, and strong evidence that test-only configuration cannot relax production limits.

**Non-Goals:** changes to production WebAuthn validation, auth limits, endpoint contracts, persisted schemas, frontend behavior, native authenticator support, or OBS verification.

## Decisions

- Use a fresh, uniquely named CI temp storage directory per workflow attempt and delete only `admin-passkey.json` before the Passkey test. Validate that the configured directory is a direct child of the runner temp root and has the `easy-lottery-e2e-` prefix; do not add a credential-reset endpoint or remove the directory.
- Keep real Chromium CDP virtual-authenticator registration and assertion ceremonies. For negative cases, mutate only the captured assertion fields immediately before forwarding the request to the real API, then assert rejection and absence of a session. This avoids mocking `navigator.credentials` or weakening server policy.
- Raise the authentication test limit only when both `Development` and `Testing:EnableAdminSessionToken` are active, with a minimum of the existing default. CI sets this test-only value; an API test verifies Production still enforces five requests.
- Mock the YouTube settings GET/PUT and API-key status endpoints in the retry test. Capture the write and return an ETag-bearing success response so the test proves payload preservation and UI feedback without persisting test settings.
- Document that CDP validates the Chromium protocol flow only; Face ID/Touch ID, device sync, iOS, and QR/cross-device ceremonies remain manual smoke tests.

## Risks / Trade-offs

- [A negative assertion could be rejected for more than one invalid field] → assert the API rejects the actual request and issues no token; retain separate focused API tests for challenge and policy behavior, and do not claim the mutated browser payload proves hardware behavior.
- [A misconfigured test path could target non-test data] → require the explicit temp-root/prefix boundary and unlink one exact state file only.
- [CI's broader test rate limit could accidentally reach production] → require Development plus the existing test-session gate and verify the Production limit in API tests.
- [Virtual authenticators do not reproduce physical authenticators] → explicitly retain the hardware and cross-device manual test boundary.

## Migration Plan

No production data migration. CI switches to a per-attempt temporary directory; rollback is to revert the workflow/test-only configuration and coverage changes. The production auth policy remains fixed at five requests per minute regardless of test configuration.

## Open Questions

None.
