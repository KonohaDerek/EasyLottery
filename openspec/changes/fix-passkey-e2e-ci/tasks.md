# Tasks

## 1. Isolate and verify Passkey browser ceremonies

- [x] 1.1 Add safe per-attempt Passkey state cleanup and a unique CI temp directory; verify cleanup is constrained to `admin-passkey.json` under an explicitly named test directory. (Passkey spec passed twice sequentially against the same API storage.)
- [x] 1.2 Extend the CDP virtual-authenticator E2E through UI logout/relogin, admin pages, and wrong-origin/RP-ID/missing-UV rejection using re-signed assertions; verify positive flows reach the real API and negative flows issue no session. (Full Playwright 86/86 passed after review correction.)
- [x] 1.3 Add a Development-and-test-session-gated authentication limit override and API tests proving the Production default remains five; run the focused SecurityEndpointTests. (Focused and full API tests passed.)
- [x] 1.4 Document Chromium CDP coverage and the Face ID/Touch ID, iOS, sync, and QR manual smoke boundary; verify instructions match the implemented E2E. (See `tests/e2e/README.md`.)

## 2. Make YouTube retry verification non-persistent

- [x] 2.1 Mock the YouTube settings load/retry/save and API-key status in Playwright; assert zero writes before successful retry, exactly one afterward, unknown-field preservation, ETag use, and distinct feedback/status.
- [x] 2.2 Run the focused YouTube retry E2E and confirm the API storage remains unchanged by its mocked save. (Save is intercepted and mocked; full Playwright suite passed.)

## 3. Integrate and deliver evidence

- [x] 3.1 Run frontend, API, full Playwright, smoke, and strict OpenSpec checks; record exact results and inspect `git diff --check`. (Local full E2E 86/86; GitHub Actions run #428 passed, including smoke and OpenSpec.)
- [ ] 3.2 Push the review correction, keep PR #308 Ready for review, reference #301, and verify Project #1 card #301 is In review.

## Workflow follow-up

- Reconcile CI for the pushed signed-assertion correction before marking delivery complete; hardware and cross-device smoke remain outside this issue's automated CI boundary.
