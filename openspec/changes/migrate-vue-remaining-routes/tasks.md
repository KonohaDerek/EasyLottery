# Tasks

## 1. API helpers and behavior tests

- [x] 1.1 Add marketplace list/install helpers and Vitest coverage proving only published built-ins are offered, install duplicates the source, and only the newly created copy is set to `Published`.
- [x] 1.2 Add the platform-connections API/form-state helpers and Vitest coverage for admin session headers, `If-Match`, unchanged-secret preservation, explicit clearing, response ETags, and recoverable `409` conflicts.
- [x] 1.3 Add the overtime admin settings client and tested merge helper; prove ETag handling and preservation of the latest session-owned fields.
- [x] 1.4 Protect concurrent third-party payment events in the existing full-feed PUT and test stale snapshots against JSON and SQLite repositories, including a mixed-source full feed.
- [x] 1.5 Preserve a newer server-side planned end when the admin leaves that input unchanged; test preservation and intentional edits.

## 2. Vue pages and authenticated navigation

- [x] 2.1 Implement the marketplace page and authenticated navigation; verify loading/empty/error states and that install/reinstall duplicates and publishes a new editable copy without changing the source or previous copies.
- [x] 2.2 Implement platform connections for YouTube, Twitch, and Discord; verify masked values are never displayed, untouched credentials remain unchanged, and stale ETags require reload.
- [x] 2.3 Implement overtime settings, reward rules, animation/fireworks controls, scoped preview-link copy, demo actions, and clear action; verify the existing session-preservation helper, explicit-only mutations, and reduced-motion behavior.
- [x] 2.4 Retain a duplicate's ID and support publication retry without another duplication request.
- [x] 2.5 Give repeated marketplace and platform-credential actions resource-specific accessible names.

## 3. Route ownership and browser coverage

- [x] 3.1 Register all three protected Vue routes and admin navigation entries; verify route unit tests cover direct paths and authentication metadata.
- [x] 3.2 Add `/legacy/...` aliases for all three Blazor pages and update the API host's Vue-shell fallback allowlist; verify direct navigation/refresh and each legacy rollback route in Playwright.
- [x] 3.3 Add focused Playwright coverage for unauthenticated redirects, marketplace duplicate safety, masked-secret/ETag updates, overtime save/error/preview/demo/clear flows, and no feed mutation on page load.
- [x] 3.4 Verify publication recovery, contextual action names, and planned-end concurrency behavior in Playwright.

## 4. Integration verification and delivery

- [x] 4.1 Run `npm test`, `npm run typecheck`, and `npm run build` in `src/EasyLotteryWeb`; run focused and full relevant Playwright tests in `tests/e2e`; run the full .NET solution tests in the Dev Container.
- [x] 4.2 Run `openspec validate --all --strict`, `git diff --check`, and a second-pass review of route fallback, secret handling, ETag conflicts, session preservation, payment-event concurrency, and unrelated changes; record results in `.agents/verification/`.
- [ ] 4.3 Push the issue branch, create a Ready-for-review PR linked to #289/#252, move Project #1 #289 to In review, and verify the PR URL, checks, and board state. Do not merge or deploy.
