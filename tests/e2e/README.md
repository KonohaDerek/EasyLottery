# Browser E2E

The Passkey E2E uses Chromium's WebAuthn CDP virtual authenticator for browser-generated registration and positive login assertions against the running API. Targeted negative cases change only the origin, RP ID hash, or UV bit and re-sign the assertion with the test authenticator's private key; the test verifies that signature locally before the real API checks the policy. It does not mock `navigator.credentials` or replace server verification.

For repeatable runs, start the Development API with `Storage__Directory` and set `E2E_STORAGE_DIRECTORY` to the same directory. It must be a dedicated `easy-lottery-e2e-*` child of `RUNNER_TEMP` (or the local OS temp directory). Before each Passkey test attempt, the test removes only `admin-passkey.json` from that directory; it never clears the directory or other files.

CDP coverage is Chromium-only. It does not validate physical security keys, Face ID/Touch ID prompts, iOS behavior, credential sync, QR handoff, or cross-device authentication. Those require a separate human smoke test on the target browser/device and authenticator. OBS validation is also outside this test suite.
