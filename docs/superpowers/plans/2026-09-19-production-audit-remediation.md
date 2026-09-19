# Production Audit Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate the audited public-page, login, validation, accessibility, and privacy-policy defects without changing production infrastructure or external credentials.

**Architecture:** A small route policy selects a new public-only layout for `/about` and `/privacy-policy`, keeping `NavMenu` and its authenticated settings request out of public routes. The login component gains local email validation, and the home-page validation flow validates only when live-chat capture is enabled. Playwright regression tests cover rendered browser behavior; production-only configuration and licensing problems remain GitHub-tracked work.

**Tech Stack:** .NET 10, Blazor WebAssembly, Blazorise, Playwright 1.62, MSTest.

**Spec:** `openspec/changes/remediate-production-audit/proposal.md` and `openspec/changes/remediate-production-audit/design.md`

## Global Constraints

- Do not purchase, bypass, hide, or alter Blazorise licensing in code.
- Do not modify Synology, DNS, reverse-proxy, certificates, Tunnel, SMTP, payment, or YouTube credentials.
- Do not send a payment, draw a lottery, send a notification, or alter production data during verification.
- Keep public About and Privacy pages reachable without a session.
- Use traditional Chinese UI copy and do not claim legal compliance.
- Every production-code change starts with a test observed failing before the implementation.

## Review Focus

- Unauthenticated `/about` and `/privacy-policy` must never request `/settings`; assert no such request occurs in the public-page E2E test.
- A whitespace-only email must not call `/api/auth/passkey/options`; assert request count stays zero in the login E2E test.
- A valid email must still allow the existing virtual-authenticator login test to reach the control panel.
- Initial home-page form state must be free of visible validation errors, but enabling chat capture with empty required fields must surface them.
- Mobile public pages must not render the sidebar or horizontally overflow; assert sidebar absence and document width in Playwright.

---

## File Structure

- Create: `src/EasyLotteryWasm/Layout/PublicLayout.razor` — minimal anonymous layout with About/Privacy links and an article content region.
- Create: `src/EasyLotteryWasm/Layout/PublicLayout.razor.css` — responsive public-page layout independent of authenticated navigation.
- Modify: `src/EasyLotteryWasm/App.razor` — select public layout for anonymous public routes while retaining the current authenticated/OBS behavior.
- Modify: `src/EasyLotteryWasm/Components/AdminLogin.razor` — empty initial email, local validation, accessible error feedback.
- Modify: `src/EasyLotteryWasm/Pages/Home.razor` — defer chat-source validation until user enables capture.
- Modify: `src/EasyLotteryWasm/Pages/PrivacyPolicy.razor` — accurate self-hosted privacy copy and semantic page title.
- Modify: `src/EasyLotteryWasm/Pages/Config/PaymentSettings.razor`, `YoutubeLogin.razor`, `AuditSettings.razor` — add one semantic `h1` per page.
- Modify: `src/EasyLotteryWasm/wwwroot/index.html` — set `lang="zh-Hant"`, product title, and description metadata.
- Create: `tests/e2e/public-pages.spec.mjs` — anonymous route, layout, metadata, and no-401 regression coverage.
- Modify: `tests/e2e/passkey-login.spec.mjs` — preserve normal login and cover blank-email guard.
- Create: `.agents/verification/2026-09-19-production-audit-remediation.md` — exact verification evidence and external limits.

### Task 1: Create the external remediation issues

**Files:**
- Modify: `openspec/changes/remediate-production-audit/tasks.md`

**Interfaces:**
- Consumes: audit evidence and accepted OpenSpec proposal.
- Produces: four GitHub issue URLs for the final verification record.

- [ ] **Step 1: Create the Blazorise licensing issue**

Create a GitHub issue titled `P0: Resolve Blazorise commercial-license banner in production` with the payment-page evidence, the requirement not to hide or bypass the banner, and acceptance criteria: a valid license or an approved component replacement, no banner in production, and documented license decision.

- [ ] **Step 2: Create the deployment-security issue**

Create `P1: Enforce HTTPS and add production security headers` describing HTTP's Synology default page, required 301/308 redirect, and CSP, frame-ancestors, nosniff, Referrer-Policy, and Permissions-Policy acceptance checks.

- [ ] **Step 3: Create the external-integrations issue**

Create `P1: Complete production payment, notification, and YouTube configuration` covering fixed public Notify URL, SMTP, YouTube API key, enabled-provider credentials, and a safe sandbox callback verification.

- [ ] **Step 4: Create the code-remediation issue**

Create `P1: Remediate public-page, login, validation, and privacy audit findings`, link the three issues above, and use this OpenSpec change as the acceptance source.

- [ ] **Step 5: Record issue URLs**

Add the four URLs to the OpenSpec tasks file and to the verification record; do not close externally-owned issues.

- [ ] **Step 6: Commit tracking artifacts**

```bash
git add openspec/changes/remediate-production-audit/tasks.md .agents/verification/2026-09-19-production-audit-remediation.md
git commit -m "docs: track production audit remediation"
```

### Task 2: Isolate anonymous public routes from the admin shell

**Files:**
- Create: `src/EasyLotteryWasm/Layout/PublicLayout.razor`
- Create: `src/EasyLotteryWasm/Layout/PublicLayout.razor.css`
- Modify: `src/EasyLotteryWasm/App.razor`
- Test: `tests/e2e/public-pages.spec.mjs`

**Interfaces:**
- Consumes: the public route set `about`, `privacy-policy` from `App.razor`.
- Produces: `PublicLayout`, selected by `App.razor` for anonymous public routes.

- [ ] **Step 1: Write the failing anonymous public-page tests**

```javascript
test("anonymous public pages do not load the admin shell or settings", async ({ page }) => {
  const settingsRequests = [];
  page.on("response", response => {
    if (new URL(response.url()).pathname === "/settings") settingsRequests.push(response.status());
  });

  await page.goto("/privacy-policy", { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "隱私權政策" })).toBeVisible();
  await expect(page.locator(".sidebar")).toHaveCount(0);
  await expect.poll(() => settingsRequests).toEqual([]);
});
```

- [ ] **Step 2: Run the new test and verify RED**

Run: `cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test public-pages.spec.mjs`

Expected: FAIL because the current anonymous page renders `.sidebar` and receives a `/settings` 401.

- [ ] **Step 3: Implement the minimal public layout and layout selection**

```razor
@inherits LayoutComponentBase
<main class="public-shell">
    <header><a href="/about">關於 EasyLottery</a><a href="/privacy-policy">隱私權政策</a></header>
    <article>@Body</article>
</main>
```

Use `typeof(PublicLayout)` for the two anonymous public routes and retain `typeof(MainLayout)` for authenticated routes.

- [ ] **Step 4: Extend the test for About and mobile reflow**

Add a `/about` assertion for no sidebar, a single `h1`, and `document.documentElement.scrollWidth <= window.innerWidth` at a 390px viewport.

- [ ] **Step 5: Run the test and verify GREEN**

Run: `cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test public-pages.spec.mjs`

Expected: PASS with no console errors or `/settings` request from public pages.

- [ ] **Step 6: Commit**

```bash
git add src/EasyLotteryWasm/App.razor src/EasyLotteryWasm/Layout/PublicLayout.razor src/EasyLotteryWasm/Layout/PublicLayout.razor.css tests/e2e/public-pages.spec.mjs
git commit -m "fix: isolate public pages from admin shell"
```

### Task 3: Guard authentication input and defer initial form errors

**Files:**
- Modify: `src/EasyLotteryWasm/Components/AdminLogin.razor`
- Modify: `src/EasyLotteryWasm/Pages/Home.razor`
- Modify: `tests/e2e/passkey-login.spec.mjs`
- Modify: `tests/e2e/public-pages.spec.mjs`

**Interfaces:**
- Consumes: `PasskeyAuthService.LoginAsync(string)` and existing `OnCatchChatChanged` event flow.
- Produces: local login validation before API invocation and on-demand home validation.

- [ ] **Step 1: Write failing login and home-form regression tests**

```javascript
test("blank login email stays client-side", async ({ page }) => {
  let optionsCalls = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/auth/passkey/options") optionsCalls++;
  });
  await page.goto("/", { waitUntil: "networkidle" });
  await expect(page.locator("#admin-email")).toHaveValue("");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(page.getByRole("alert")).toContainText("請輸入有效的管理員 email");
  expect(optionsCalls).toBe(0);
});
```

After virtual-authenticator login, assert the home page does not initially show `請輸入 YT 網址`; toggle chat capture and assert the validation message becomes visible.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test passkey-login.spec.mjs public-pages.spec.mjs`

Expected: FAIL because the email is prefilled and the home page initially renders validation errors.

- [ ] **Step 3: Implement minimal validation behavior**

Set `email` to `string.Empty`; in `LoginAsync`, return with an accessible error when `MailAddress`-compatible validation fails. Change the home `Validations` mode and the capture-toggle handler so `ValidateAll()` runs only when enabling capture; retain the current block on invalid input.

- [ ] **Step 4: Update the normal Passkey test**

Before its first login click, fill `#admin-email` with the configured test address so the existing virtual-authenticator registration remains covered.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run: `cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test passkey-login.spec.mjs public-pages.spec.mjs`

Expected: PASS; blank email produces no options API request and valid login still reaches `名單與獎項匯入 / 匯出`.

- [ ] **Step 6: Commit**

```bash
git add src/EasyLotteryWasm/Components/AdminLogin.razor src/EasyLotteryWasm/Pages/Home.razor tests/e2e/passkey-login.spec.mjs tests/e2e/public-pages.spec.mjs
git commit -m "fix: validate login and defer initial form errors"
```

### Task 4: Correct metadata, semantic headings, and privacy content

**Files:**
- Modify: `src/EasyLotteryWasm/wwwroot/index.html`
- Modify: `src/EasyLotteryWasm/Pages/PrivacyPolicy.razor`
- Modify: `src/EasyLotteryWasm/Pages/Config/PaymentSettings.razor`
- Modify: `src/EasyLotteryWasm/Pages/Config/YoutubeLogin.razor`
- Modify: `src/EasyLotteryWasm/Pages/Config/AuditSettings.razor`
- Modify: `tests/e2e/public-pages.spec.mjs`

**Interfaces:**
- Consumes: existing `PageTitle` components and the public page routes from Task 2.
- Produces: correct root metadata and one visible semantic `h1` per audited page.

- [ ] **Step 1: Write the failing metadata and policy tests**

```javascript
test("public metadata and policy describe the hosted product", async ({ page }) => {
  await page.goto("/privacy-policy", { waitUntil: "networkidle" });
  await expect(page.locator("html")).toHaveAttribute("lang", "zh-Hant");
  await expect(page).toHaveTitle(/EasyLottery/);
  await expect(page.getByText("不會收集、存儲或使用您的任何個人信息")).toHaveCount(0);
  await expect(page.getByText("部署者控制的儲存體")).toBeVisible();
});
```

Add authenticated-route assertions that payment, YouTube, and audit each expose exactly one `h1`.

- [ ] **Step 2: Run the test and verify RED**

Run: `cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test public-pages.spec.mjs`

Expected: FAIL because the root language is `en`, the generic title is used, the old privacy claim remains, and the audited management pages have no `h1`.

- [ ] **Step 3: Implement the minimal semantic and copy changes**

Set `lang="zh-Hant"`, title `EasyLottery｜直播互動抽獎系統`, and a Chinese description. Replace the policy with scoped self-hosted copy that names deployment-controlled storage, Passkey public credentials, configuration/activity/result/payment-callback records, third-party services chosen by the deployer, and a prompt to contact the deployer for retention/deletion requests. Add visible page headings matching existing `PageTitle` values.

- [ ] **Step 4: Run focused test and verify GREEN**

Run: `cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test public-pages.spec.mjs`

Expected: PASS with correct metadata, revised policy copy, and semantic headings.

- [ ] **Step 5: Commit**

```bash
git add src/EasyLotteryWasm/wwwroot/index.html src/EasyLotteryWasm/Pages/PrivacyPolicy.razor src/EasyLotteryWasm/Pages/Config/PaymentSettings.razor src/EasyLotteryWasm/Pages/Config/YoutubeLogin.razor src/EasyLotteryWasm/Pages/Config/AuditSettings.razor tests/e2e/public-pages.spec.mjs
git commit -m "fix: improve metadata privacy and page semantics"
```

### Task 5: Verify, record, and review the complete change

**Files:**
- Modify: `.agents/verification/2026-09-19-production-audit-remediation.md`
- Modify: `openspec/changes/remediate-production-audit/tasks.md`

**Interfaces:**
- Consumes: all task test results and GitHub issue URLs.
- Produces: reproducible verification record and completed OpenSpec task list.

- [ ] **Step 1: Run complete automated verification**

Run:

```bash
dotnet test EasyLottery.generated.sln
dotnet build EasyLottery.generated.sln --no-restore
cd tests/e2e && BASE_URL=http://127.0.0.1:18930 npx playwright test
```

Expected: all commands pass. If the local server is unavailable for E2E, record the exact limitation and run the authenticated Ego Lite browser check instead.

- [ ] **Step 2: Perform browser acceptance checks**

In Ego Lite, inspect `/`, `/about`, `/privacy-policy`, `/system/payment`, `/system/youtube-login`, and `/system/audit`; confirm the code changes without changing configuration. Record screenshots and observed limitations.

- [ ] **Step 3: Update records**

Mark completed OpenSpec tasks, list issue URLs, test commands, pass/fail counts, and the unresolved external-owner work.

- [ ] **Step 4: Commit verification artifacts**

```bash
git add openspec/changes/remediate-production-audit/tasks.md .agents/verification/2026-09-19-production-audit-remediation.md
git commit -m "test: verify production audit remediation"
```

- [ ] **Step 5: Run a whole-branch review**

Generate the review package, run a fresh review focused on anonymous route isolation, client-side validation bypasses, semantic duplication, and privacy-copy overclaims. Fix any critical or important findings with a new RED→GREEN test cycle before handoff.

## Plan Self-Review

- Spec coverage: Tasks 1–5 cover issue tracking, public layout, login and home validation, metadata/headings/privacy content, and verification. Licensing, infrastructure, and credentials are explicitly tracked rather than implemented.
- Placeholder scan: no implementation placeholder is required; exact files, routes, commands, expected outcomes, and code shapes are included.
- Type consistency: all tasks use the existing `PasskeyAuthService.LoginAsync(string)`, `App.razor` routes, and Playwright E2E conventions.
- Review Focus: all five high-risk cases are assigned to Task 2–4 browser tests.
