# Implementation plan: EasyLottery live-site fixes #317–#323

This is a planning deliverable only. I made no writes, ran no builds or tests, and called no external services. The only tools available to me in this session were read and search tools, so the plan is below rather than in a plan file.

**Base and route:** worktree `EasyLottery-live-fixes-20261112`, branch `codex/live-fixes-317-323`, base `43f69b8` plus `ab5af48`. You confirmed v0.0.7 is the latest published stable release, so the route is Claude plans and Codex implements. `TYPESAFE_API_KEY` is absent, so Codex records a manual structured decision in `.agents/decisions/` instead of calling Jev. The baseline API tests passed 147/147 and the web build passed (from the logs in the review-evidence folder).

---

## Context
A live-site exploration on 2026-11-12 found seven defects. Each has a read-only Playwright case in `tests/e2e/live/` marked `test.fail`. These are known defects, not real passes. You asked for the issues to be fixed directly. The goal is real fixes in this branch, each proven by a local regression test with no `test.fail`, without breaking existing Blazor legacy routes, legacy `/settings` clients, the passkey first-registration flow, or normal transparent OBS overlays.

## Constraints that shaped the design
- **Blazor deep links depend on the catch-all.** `app.MapFallbackToFile("index.html")` (`Program.cs:288`) serves Blazor for every `/legacy/*` page. Those are linked from Vue (`PokeBoxView.vue:250`, `RouletteView.vue:212`, `DonateActivitiesView.vue:181`) and covered by E2E (`vue-remaining-routes.spec.mjs:332`, `overtime-obs.spec.mjs:94`, …). The catch-all also serves the Blazor-only `/pokebox/editor/{id}` and `/roulette/editor/{id}`. `MapFallbackToFile("/legacy", …)` matches only the exact path `/legacy`. Swapping the catch-all to a Vue 404 without mapping these first would break rollback.
- **There is only one pending passkey challenge per flow** (`PasskeyStateStore.SetPendingAsync`). If decoy responses wrote a pending challenge, any anonymous request could wipe out the real admin's in-progress login.
- **Passkey E2E expects specific responses.** It needs options `409` first (loopback is allowlisted), and verify failures with code `passkey_verification_failed` (`passkey-login.spec.mjs:68,151`).
- **OBS errors are visually hidden unless `?controls=1`.** Roulette and PokeBox put them in an `sr-only` element; Donate only renders them with controls. Donate also shows nothing at all when the activity is unknown (`activity === null`, no error). Overtime already shows errors whenever nothing has loaded (`!settings`), and its live test passes.
- **#313 (core draw history) is already in this base** (PR #314) but is not confirmed deployed or accepted (`docs/verification/issue-295-313.md:42`).

---

## Per-issue design

### #318 Routing (`src/EasyLotteryAPI/Program.cs`, `Settings/Endpoints/SettingsEndpoints.cs`, `src/EasyLotteryWeb/src/routes.ts`, new `views/NotFoundView.vue`)
1. **Unknown `/api` and `/hubs` paths:** before the SPA fallbacks, add `app.Map("/api/{**path}", …)` and `app.Map("/hubs/{**path}", …)`. Both return `Results.Problem(statusCode: 404, title: "找不到 API 端點")`, which is `application/problem+json`. Specific endpoints take precedence over these catch-alls, and SignalR negotiate/connect stays intact (to be checked by test). A side effect: a wrong HTTP method on an existing `/api` path now returns 404 JSON instead of today's 200 Blazor HTML.
2. **Keep Blazor reachable:** add `MapFallbackToFile("/legacy/{**path}", "index.html")`, `"/pokebox/editor/{id:int}"` and `"/roulette/editor/{id:int}"`.
3. **Replace the final catch-all** with `app.MapFallback("{*path:nonfile}", handler)`:
   - GET/HEAD: status 404, `text/html; charset=utf-8`, `Cache-Control: no-cache`, body streamed from `app.Environment.WebRootFileProvider.GetFileInfo("vue/index.html")`. If that file is missing, a plain 404.
   - Other methods: a 404 problem response.
   - The `nonfile` constraint keeps today's behaviour of real 404s for missing static files.
4. **Vue:** add a last route `{ path: "/:pathMatch(.*)*", component: NotFoundView, meta: { title: "找不到頁面｜EasyLottery" } }`. It renders in `PublicLayout` with one `<h1>找不到頁面</h1>` and links to `/about` and `/login`. Side effect: OBS URLs with malformed IDs (e.g. `/obs/roulette/not-a-guid`) now return HTTP 404 with the Vue page and its "網址無效" message, instead of the Blazor shell.
5. **`/settings`:** change `app.MapGet("/settings", readSettings)` to `MapMethods("/settings", ["GET","HEAD"], …)`, so HEAD returns the same 401 as GET instead of 200 HTML. At the top of `readSettings`, only when the path is `/settings` and the request is a browser navigation (`Sec-Fetch-Mode: navigate` or `Sec-Fetch-Dest: document`), return `Results.Redirect("/")`; the Vue route guard then sends anonymous users to `/login?redirect=/`.
   - Do not use an `Accept` header heuristic. `fetch()` from Vue, Blazor JS and OBS always sends `Sec-Fetch-Mode: cors`/`same-origin`, and non-browser clients send no `Sec-Fetch-*` headers, so all legacy API clients behave exactly as before.
   - Moving the API to `/api/settings` is out of scope.

### #319 Passkey enumeration (`Passkey/PasskeyAuthenticationService.cs`, `Passkey/Endpoints/PasskeyEndpoints.cs`, `Passkey/PasskeyStateStore.cs`)
**`BeginAsync`:**
- Run `NormalizeFlow` first.
- **Register:** if `!_issuancePolicy.CanIssue(context)`, return 403 `registration_not_allowed` before any email check. Today the email check runs first, which leaks the email through the register flow. After that, keep the current behaviour: `ValidateEmail`, then 409 `passkey_already_registered`.
- **Login:**
  - Admin email with credentials: real options and pending challenge, unchanged.
  - Admin email, no credentials, and `CanIssue(context)`: 409 `registration_required`. Only allowlisted sources get this, which keeps first registration and the passkey E2E working.
  - Everything else: **decoy options**, and no pending challenge is written. Decoys come from the same `fido2.GetAssertionOptions(...)` call with `UserVerification.Required`, so they have the same shape.
  - Decoy `allowCredentials` count is `max(1, realCount)`. ID lengths copy the real IDs' lengths (16 bytes if there are none). Bytes come from `HKDF.DeriveKey(SHA256, seed, len, info: "easylottery-passkey-decoy|{normalizedEmail}|{i}")`, so the same email always gets the same IDs.
  - `seed` is a 32-byte random `DecoySeed` added to `PasskeyState`. It is created once by a new `PasskeyStateStore.GetOrCreateDecoySeedAsync()` under the existing gate. It must be persistent: a per-process key would change on restart and reveal which email is real. The field is additive JSON, and System.Text.Json ignores it on older builds.

**`FinishAsync(context, request, ct)`** (the endpoint passes `HttpContext`):
- **Login:** a non-admin email, a missing or mismatched pending challenge, an unknown credential, and an assertion failure all return the same 401 `passkey_verification_failed` / "Passkey 驗證失敗。". Do not take the pending challenge for a non-admin email.
- **Register:** if `!CanIssue`, return 403 `registration_not_allowed` before the email check. Otherwise keep the current codes (`challenge_invalid` tests stay valid).

**Wording and logging:**
- The `ValidateEmail` message becomes "只有設定的管理員 email 可以登入。" (also covers #321 item 3). It is now reachable only on the allowlisted register path.
- Add one `LogInformation` when a decoy is issued. Never log the email.

### #321 Login accessibility (`views/LoginView.vue`, `auth/passkey.ts`)
- Change the title to `<v-card-title tag="h1" id="admin-login-title" class="login-title text-h5 text-sm-h4">`. This is a minimal markup change and does not redo the #255 heading work.
- Add a `fieldInvalid` ref that is set only on client-side validation failure. Bind the input's `:aria-invalid="fieldInvalid ? 'true' : undefined"` and `:aria-describedby="fieldInvalid ? 'admin-email-error' : undefined"`, and give the alert `id="admin-email-error"`. Reset it on submit.
- In `completeFlow`, map `DOMException` `NotAllowedError`/`AbortError` to "Passkey 登入已取消，或此裝置沒有可用的 Passkey。". Without this, decoy logins (and cancelled real logins) show English browser text.

### #320 Mobile layout (`src/EasyLotteryWeb/src/styles.css`, `components/PublicLayout.vue` only if needed)
- `.nav-link { white-space: nowrap; }`.
- Add `@media (max-width: 420px)` with `.nav-link { padding: .5rem .4rem; font-size: .875rem }` and `.brand { font-size: 1rem }`.
- Add `.login-title { white-space: normal; overflow: visible; text-overflow: clip; overflow-wrap: anywhere; }`.
- If it still overflows at 375px, shorten the labels with a short/long `<span>` pair (accessible name unchanged).

### #317 About copy (`views/AboutView.vue`, new `src/about/highlights.ts` + `.test.ts`)
- Move `highlights` into a `.ts` module so it can be unit-tested.
- Replace the third card with copy that is true for both deployed v0.0.7 and this build, and states scope per type: "活動設定與操作稽核紀錄保存在伺服器；戳戳樂／轉盤結果可記錄到活動結果，Donate 抽獎紀錄也會保存，方便管理與備份。"
- Make no claim about general (core) draw history, either way. Codex must check each claimed capability against the code before committing.
- The vitest asserts that the old sentence is gone and that no text claims general draw results can be queried after reload or restart.

### #322 OBS error states (new `src/EasyLotteryWeb/src/obs/errors.ts` + `.test.ts`; `views/RouletteObsView.vue`, `PokeBoxObsView.vue`, `DonateObsView.vue`, `OvertimeObsView.vue`)
**Shared messages:** `obsErrorMessage(cause, kind)`. Every message ends with a next step ("請回管理頁重新複製 OBS 網址" or "重新載入"):

| Cause | Message |
| --- | --- |
| 401 / missing token | 「OBS 工作階段無效或已過期，…」 |
| 403 | permission message |
| 404 | 「找不到此轉盤／戳戳樂／Donate 活動，…」 |
| 429 | retry later |
| `TypeError` (network) | connection message |
| Other `ApiError` | 「無法載入…（HTTP n），…」 — never the bare 「API 回傳 HTTP n。」 |
| Other `Error` | keeps its own domain message |

The existing "網址無效" errors get the same next-step wording.

**Visibility policy:** adopt the existing Overtime rule everywhere. Fatal errors (nothing renderable loaded) are always visible. Non-fatal status messages and load spinners keep their current controls-only behaviour.
- **Roulette/PokeBox:** change `v-else-if="error && controlsEnabled"` to `v-else-if="error"`, show the buttons only when `controlsEnabled`, and remove the `sr-only` branch.
- **Donate:** a visible notice when `!loading && !activity`. Show `error` if set, otherwise 「找不到此 Donate 活動，請回管理頁重新複製 OBS 網址。」. A loaded activity with no record stays fully transparent. A transient poll error keeps the last activity and shows nothing, as today.

`?quiet=1` is **not** added; that is a deliberate non-goal. Interaction OBS views are out of scope.

### #323 Favicon and robots.txt
- **Favicon:** byte-copy `src/EasyLotteryWasm/wwwroot/favicon.png` to `src/EasyLotteryWeb/public/favicon.png`, and add `<link rel="icon" type="image/png" href="/favicon.png" />` to `src/EasyLotteryWeb/index.html`. Vite should rewrite this to `/vue/favicon.png`; check the built `wwwroot/vue/index.html`.
- **robots.txt:** new `src/EasyLotteryWasm/wwwroot/robots.txt`, served at the site root like `favicon.png`:
  ```
  User-agent: *
  Allow: /about
  Allow: /privacy-policy
  Disallow: /obs/
  Disallow: /system/
  Disallow: /api/
  Disallow: /hubs/
  Disallow: /legacy
  Disallow: /
  ```
  robots.txt does not restrict access; it only asks crawlers not to index these paths.

---

## Tests (regression and acceptance)
**API (MSTest, `tests/EasyLotteryAPITests/`)**
- New `SpaRoutingTests.cs`:
  - Unknown page GET and HEAD return 404 HTML containing `/vue/assets/`.
  - `/api/x` GET and POST, and `/hubs/x`, return 404 non-HTML problem JSON.
  - Existing `/api/settings/backups` without auth still returns 401, not 404 (precedence check). `POST /hubs/overtime/negotiate` does not return 404.
  - `/legacy/market`, `/legacy/obs/donate/x`, `/pokebox/editor/1` and `/roulette/editor/1` serve Blazor; `/obs/donate/x` serves Vue 200.
  - `/settings`: with `Sec-Fetch-Mode: navigate` it returns 302 `Location: /` (client created with `AllowAutoRedirect=false`); without it, 401; HEAD returns 401.
  - `/robots.txt` returns 200 and contains `Disallow: /obs/`. The favicon `href` parsed from `/` returns 200 `image/png`.
- `SecurityEndpointTests.cs`:
  - Public mode with no allowlist: admin and non-admin login options give 200 with the same JSON property set, the same `allowCredentials` count and ID lengths, and no `admin_email_required`. This holds both with no credentials and with one stored credential.
  - Decoy IDs are stable for the same email, differ from the real IDs, and survive a factory restart that uses the same storage.
  - A non-admin options call does not overwrite the admin's pending challenge (`TakePendingAsync(Login)` still returns the admin email).
  - Register options and register verify from a non-allowlisted source return 403 `registration_not_allowed` for both emails.
  - Login verify for a non-admin email and for an admin with no pending challenge return identical 401 bodies.
  - Rewrite `PasskeyEndpoints_RejectNonAdminEmail` to expect decoy 200. This is an intended behaviour change, and the evidence must say so.

**Vitest (`src/EasyLotteryWeb`)**
- `obs/errors.test.ts` (each status and no raw-only HTTP text), `about/highlights.test.ts`, and a `NotAllowedError` mapping test in the existing passkey test file if there is one, otherwise a new file.

**Local Playwright (`tests/e2e/`, no `test.fail`)**
These are the migrated, real passing regression cases:
- New `routing-errors.spec.mjs` (#318): 404 status plus the Vue 404 `h1`; `/api` returns non-HTML 404; `page.goto("/settings")` lands on `/login` with no 401 document; legacy request 401.
- Extend `public-pages.spec.mjs`:
  - #320: nav link heights < 48px and title `scrollWidth <= clientWidth` at 375px.
  - #323: favicon and robots.
  - #317: the About sentence is absent.
- New `login-a11y.spec.mjs`:
  - #321: exactly one `h1`; `aria-invalid` and `aria-describedby` point to the error.
  - #319: a non-admin options call returns no 403 and no `admin_email_required`.
- New `obs-error-states.spec.mjs` (#322): overtime with no session; roulette with a random GUID shows a visible 「找不到此轉盤」; donate with no session shows a visible message; donate with an admin token in `#sessionToken=` and an unknown key shows 「找不到此 Donate 活動」.
- Add a zero-visible-notice assertion for a valid idle overlay in `donate-obs.spec.mjs` and `pokebox-roulette-obs.spec.mjs`.
- `passkey-login.spec.mjs` must pass unchanged.

**Live suite (`tests/e2e/live/`):** leave the `test.fail` markers for #318–#323 in place. Production still runs v0.0.7 and deployment is not authorized, so removing them would claim a production fix that hasn't happened. Optionally run the live suite once against the local server (`LIVE_BASE_URL=http://127.0.0.1:18930 … --grep "#31[89]|#32[0-3]"` or by test title) and record "unexpected pass" as supporting evidence only, not as a pass. The #253 and HSTS cases are out of scope.

**Verification commands** (as in `ci.yml`): `npm ci/typecheck/test/build --prefix src/EasyLotteryWeb`; `dotnet build`, then API and Domain tests; start the API on `127.0.0.1:18930`; run E2E with `E2E_STORAGE_DIRECTORY=$RUNNER_TEMP/easy-lottery-e2e-*`; `openspec validate --all --strict`; `git diff --check`. Record commands, exit codes and counts in `.agents/verification/`.

## OpenSpec change `fix-live-site-findings-317-323`
- **`proposal.md`:** the 7 issues, scope, non-goals. Non-goals: no new history, no move to `/api/settings`, no `?quiet`, no interaction OBS views, no deployment, no production tests.
- **`design.md`:** the decisions above (routing order, the `Sec-Fetch` check, decoy and HKDF seed design and the pending-challenge protection, the OBS fatal-error visibility rule).
- **`tasks.md`.**
- **Spec deltas:**
  - `spa-routing`: unknown nav → 404 HTML; `/api` and `/hubs` → 404 problem; legacy paths preserved; `/settings` navigate → redirect.
  - `passkey-login-privacy`: indistinguishable options and verify responses; allowlist-only registration signals.
  - `obs-overlay-errors`.
  - `public-experience`: mobile nav, login `h1`/ARIA, About accuracy, favicon/robots.
- **Acceptance criteria and test strategy** as listed in the Tests section.
- **Rollback:** revert the PR. No migration. The `DecoySeed` field is harmless to older builds; if they rewrite the state file and drop it, a new seed is generated later.
- **Observability:** the decoy log line without email; the existing security middleware already logs 401/403/429. No new metrics.

## Risks and uncertainties
1. **Timing side channel (#319):** the real path writes the pending challenge and decoys don't, so response times can differ. The rate limit lowers the risk but doesn't remove it. Record this as a residual finding for the reviewers to rule on; it needs your decision if accepted, because the policy grants no risk-acceptance exception.
2. **Production allowlist config is unknown:** if production's proxy IP is in `AllowedClientIps` without `TrustedProxyIps`, every internet client counts as allowlisted. Then the `registration_required` signal (only while no passkey exists) and the register-flow messages would reach the public. I can't check this without production access.
3. **Unconfirmed library behaviour:** Vite rewriting the public favicon `href` with the `/vue/` base, and Vuetify `v-card-title` supporting the `tag` prop. Both are checked by the build output and the tests above.
4. **Stale page title:** the #317 copy is deliberately neutral about core history. Once #313 is deployed and accepted, the copy can be updated.
5. **Behaviour changes to call out to reviewers:** unknown-path status goes from 200 to 404; wrong-method `/api` calls become 404 JSON; OBS fatal errors are now visible on stream when nothing could be rendered (deliberate); the non-admin login test's expectation changes.
6. **Server and client routes must stay in sync:** any Vue route added later without a server fallback entry will now return 404 instead of 200. The new `SpaRoutingTests` cases protect the current list.

## Blockers and workflow items
- **No blockers to implementation.**
- **Process:**
  - Codex keeps the round ledger outside the reviewed tree (in `EasyLottery-review-evidence/live-fixes-20261112/`).
  - Independent real Codex and Claude reviews must cover the same final SHA, with at most 5 rounds.
  - No deployment and no production testing, so the live markers stay until a separately authorized deploy and recheck.
  - The original `claude/live-site-e2e-20261112` worktree and its uncommitted reports, traces and screenshots must not be touched.
- **Suggested structure:** one Ready PR with one commit per issue for easier review. It references #317–#323, uses no "closes" keyword (live acceptance is still pending), and notes the overlap with #255 and #313.

## Implementation refinements
Real and decoy options both perform gated storage writes; unknown verify attempts preserve unrelated pending ceremonies while following the same gated IO path. This mitigates an obvious timing distinction, not a claim of constant-time distributed execution. No email, credential or decoy seed logging.

Verification clarification: #317 is covered by a newly added normal-suite regression, not a dedicated original live test.fail case. Public mobile layout uses a verified two-row bar below600px.

## Review-driven implementation refinement

Round 1 identified that a credential index is not a stable credential identity: removal shifts indices, and initial registration replaces the empty credential state. Decoy HKDF context is now keyed by normalized email and the stored credential ID, with a distinct unregistered marker. This supersedes any index-based design in the original plan. Tests verify stable IDs, initial replacement, append preservation and removal evolution for real and decoy paths.

Donate control previews (controls=1) preserve visible polling errors after a successful initial load. Formal overlays (controls=0) remain transparent while idle and retain their current content during transient failures. Both behaviours have normal-suite browser regressions.
