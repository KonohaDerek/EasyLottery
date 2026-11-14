# EasyLottery live-site QA — 2026-11-12

## Scope and setup

- Target: `https://easylotter.k-derek.synology.me/` (production deployment).
- Method: ego-browser v2.2.0 Chromium Page API (Playwright-style browser interaction); browser UI and DOM assertions only, plus read-only HTTP GETs for response headers.
- TaskSpace: `EasyLottery production QA 2026-11-12` (space 23; finished after the run).
- Repository baseline: `main` / `origin/main` at `43f69b802bb12e3c1bda2b4636f892bbc8d775fd`; report is kept in worktree `.worktrees/live-site-e2e-20261112`, branch `claude/live-site-e2e-20261112`.
- Site access: unauthenticated. No administrator credential was supplied. No Passkey ceremony, lottery draw, payment, settings write, data deletion, or production mutation was attempted.
- Result scope: public pages and unauthenticated routing only. Authenticated admin features, OBS, integrations, and end-to-end draw flows were not tested.

## Retained test cases

| ID | Case | Steps / observation | Expected | Actual | Result |
|---|---|---|---|---|---|
| LS-001 | Root route redirects to login | Navigate to `/` in ego-browser and inspect final URL/title. | Unauthenticated visitor is directed to admin login. | Final URL `/login?redirect=/`; title `管理員登入｜EasyLottery`. | PASS |
| LS-002 | Protected admin route requires authentication | Navigate directly to `/system/audit`. | No unauthenticated audit content is shown; redirect to login. | Final URL `/login?redirect=/system/audit`; only the Passkey login form is visible. | PASS |
| LS-003 | Public About page loads without admin settings request | Navigate to `/about`; inspect title, `lang`, H1, links and resource URLs matching settings APIs. | Public information page loads, has a page heading, and does not request authenticated settings. | HTTP 200 in direct GET; title `關於 EasyLottery`; `lang=zh-Hant`; one H1; no matching settings API resource request. Copy says activity settings, lottery results and audit records are separately persisted. | FAIL — misleading persistence claim; tracked by new issue [#317](https://github.com/KonohaDerek/EasyLottery/issues/317), related to #313. |
| LS-004 | Public privacy policy loads | Navigate to `/privacy-policy`; inspect page title, language, heading hierarchy, visible content and links. | Anonymous visitor can read the policy and identify its sections. | HTTP 200; title `隱私權政策｜EasyLottery`; `lang=zh-Hant`; one H1 and five H2 sections; policy states actual retention/access/deletion depend on deployment operator. | PASS |
| LS-005 | Public and login pages fit narrow viewports | At widths 320, 390 and 768 CSS px, inspect `/about`, `/privacy-policy` and `/login`; compare `documentElement.scrollWidth` with `innerWidth`. | No horizontal overflow at tested widths. | All 9 route/width combinations had equal scroll and inner widths. | PASS |
| LS-006 | Login email starts blank and is labelled | At `/login`, inspect the email input value and associated label without submitting. | No placeholder identity is prefilled; email input has a programmatic label. | Input type `email`, initial value empty, `autocomplete=username`; label `管理員 email` is associated with `admin-email`. | PASS |
| LS-007 | Login page has a semantic heading | At `/login`, inspect `h1`–`h6` elements and page snapshot. | Login form has a semantic page heading for screen-reader heading navigation. | No `h1`–`h6`; visible text “管理員 Passkey 登入” is a generic container, not a heading. | FAIL — specifically tracked by [#321](https://github.com/KonohaDerek/EasyLottery/issues/321); no duplicate issue opened. |
| LS-008 | HTTPS response security headers | Send read-only GETs to `/`, `/about`, `/privacy-policy`; inspect response headers. | CSP/frame protection, MIME sniff protection, referrer policy, and restrictive permissions policy are present. | All 3 HTTPS responses returned 200 with CSP (`frame-ancestors 'none'`), `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and `Permissions-Policy: camera=(), microphone=(), geolocation=()`. | PASS for sampled headers. |
| LS-009 | HTTP origin redirects to HTTPS | Send a read-only GET to `http://easylotter.k-derek.synology.me/` with redirects disabled. | HTTP responds with 301/308 to the equivalent HTTPS URL. | Response was HTTP 200 with no `Location` header and no sampled security headers. | FAIL — already tracked by [#253](https://github.com/KonohaDerek/EasyLottery/issues/253); no duplicate issue opened. |

## Evidence and limitations

- ego-browser commands used: `ego-browser skill`; `ego-browser nodejs -e '…'` against the target using the documented `taskSpace`, Page navigation/snapshot/evaluate, and `task.finish` APIs.
- A concurrent change appeared in this worktree after the initial clean status: `tests/e2e/live/*.spec.mjs`, a Playwright config edit, and existing test artifacts. These files were not authored or modified by this QA run and were left untouched. GitHub search found the newly filed [#321](https://github.com/KonohaDerek/EasyLottery/issues/321) matching the login-heading observation.
- The current branch head is `01aa1d1cfafde98e1c14ca28ba38efd4f636eb2c` (`test(e2e): add read-only live-site Playwright suite`), ahead of the starting base. Its committed `.agents/verification/2026-11-12-live-site-e2e.md` records the full suite command as 28 passed (15 regular tests plus 13 expected known-defect failures), with each known defect also independently checked after disabling `test.fail`. That is branch evidence, not a run performed by this QA session.
- Playwright CLI attempt: `./node_modules/.bin/playwright test -c live/live.config.mjs live/public-pages.spec.mjs live/routing.spec.mjs --output=/private/tmp/easylottery-live-playwright-20261112 --reporter=list` (from `tests/e2e`). Result: 4 passed, 15 failed. Browser-driven cases could not start Chromium (`MachPortRendezvousServer ... Permission denied`, `SIGTRAP`); request-context cases reported `getaddrinfo ENOTFOUND easylotter.k-derek.synology.me`. These failures are environment/transport blockers and are not application assertions. The manual ego-browser checks above did reach the site. The temp Playwright output is outside the repository.
- Read-only response probe used Node `fetch(url, { redirect: "manual" })` for HTTP/HTTPS routes and read status plus response headers.
- Mobile checks changed only the browser's emulated viewport using CDP `Emulation.setDeviceMetricsOverride`; they did not alter the site.
- No screenshots were needed to resolve the semantic/overflow cases; DOM snapshots and computed widths were retained above.
- This run does not verify authenticated admin behavior, real Passkey authentication, data correctness after mutations, OBS Browser Source behavior, or production deployment parity beyond the visited routes.
- Open issue overlap was checked before filing: #253 tracks HTTP redirect/security headers; #321 specifically tracks the login heading; #255 is the broader public/login acceptance issue; #313 tracks missing persistent core lottery history. The distinct About-copy mismatch was filed as #317.

## Manual routing decision

- Project release evidence: authoritative GitHub `releases/latest` lookup at task start returned published, non-draft, non-prerelease stable release `v0.0.7`, published 2026-10-22 UTC. Route policy is therefore Claude plans / Codex implements; this QA task required no implementation role assignment.
- Jev: `TYPESAFE_API_KEY` was absent, so no Jev request was made. Manual route: bounded, main-agent QA; low risk to application data because scope was read-only; browser integration/manual verification; no parallel workers; no production or authenticated action authorized.
- Scope: preserve runnable/repeatable test cases and record observed issues. Non-goals: application code changes, production writes, issue triage beyond deduplication and filing observed defects, push, PR, merge, or deployment.
