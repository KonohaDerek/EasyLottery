import { expect, test } from "./fixtures.mjs";

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
}

function paymentSettings() {
  return {
    donationIntegration: {},
    publicCallback: { customDomain: "", tunnelProvider: "cloudflare-quick", activePublicBaseUrl: "" },
    mailDelivery: { smtpHost: "", smtpPort: 587, smtpUsername: "", smtpPassword: "", fromAddress: "", fromName: "" },
    youTube: { apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__" },
    enableYouTubeSuperChat: true,
    resultNotificationEmail: ""
  };
}

test("admin navigation is grouped and logout remains visible, accessible, and clears the session", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/payments", route => route.fulfill({
    status: 200, headers: { ETag: '"payment-v1"' }, contentType: "application/json", body: JSON.stringify(paymentSettings())
  }));
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: true })
  }));

  await page.goto(`${baseURL}/system/youtube-login`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "YouTube 直播設定" })).toBeVisible();
  const nav = page.locator(".admin-drawer .admin-nav");
  const groups = nav.locator("details");
  await expect(groups).toHaveCount(4);
  await expect(groups.nth(3)).toHaveAttribute("open", "");
  await expect(groups.nth(0)).not.toHaveAttribute("open", "");
  await expect(nav.getByRole("link", { name: "YouTube 直播設定" })).toHaveAttribute("aria-current", "page");

  const firstGroup = groups.nth(0);
  await firstGroup.locator("summary").focus();
  await page.keyboard.press("Enter");
  await expect(firstGroup).toHaveAttribute("open", "");
  await expect(firstGroup.getByRole("link", { name: "抽獎工作區" })).toBeVisible();

  const visualTokens = await page.locator(".eyebrow").evaluate(element => ({
    primary: getComputedStyle(document.documentElement).getPropertyValue("--creator-primary").trim(),
    heading: getComputedStyle(element).color,
    logout: getComputedStyle(document.querySelector(".admin-logout")).color
  }));
  expect(visualTokens).toEqual({ primary: "#5541b8", heading: "rgb(85, 65, 184)", logout: "rgb(255, 255, 255)" });

  const logout = page.getByRole("button", { name: "登出並清除管理員工作階段" });
  await expect(logout).toBeVisible();
  await expect(logout).toHaveText("登出");
  await logout.click();
  await expect(page).toHaveURL(/\/login$/);
  expect(await page.evaluate(() => sessionStorage.getItem("easy-lottery.session-token"))).toBeNull();
});

test("mobile navigation opens grouped creator routes without a drawer gap", async ({ page, baseURL }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/payments", route => route.fulfill({
    status: 200, headers: { ETag: '"payment-v1"' }, contentType: "application/json", body: JSON.stringify(paymentSettings())
  }));
  await page.route("**/api/donate-activities", route => route.fulfill({
    status: 200, contentType: "application/json", body: "[]"
  }));

  await page.goto(`${baseURL}/system/payment`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "開啟管理功能導覽" }).click();
  const mobileNav = page.locator(".admin-nav--mobile");
  await expect(mobileNav).toBeVisible();
  const creatorGroup = mobileNav.locator("details").nth(0);
  await creatorGroup.locator("summary").click();
  await creatorGroup.getByRole("link", { name: "Donate 活動" }).click();
  await expect(page).toHaveURL(/\/donate-activities$/);
  await expect(page.getByRole("heading", { name: "Donate 活動", exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});

test("YouTube Live Chat has one setup editor and payment saves preserve its masked values", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let savedSettings = paymentSettings();
  let configured = false;
  const writes = [];
  let revision = 1;
  await page.route("**/api/settings/payments", async route => {
    const request = route.request();
    if (request.method() === "GET") {
      await route.fulfill({
        status: 200, headers: { ETag: `"payment-v${revision}"` }, contentType: "application/json", body: JSON.stringify(savedSettings)
      });
      return;
    }
    const body = request.postDataJSON();
    writes.push({ etag: request.headers()["if-match"], body });
    savedSettings = body;
    revision++;
    if (body.youTube.apiKey !== "__EASYLOTTERY_SECRET_UNCHANGED__") configured = true;
    await route.fulfill({
      status: 200, headers: { ETag: `"payment-v${revision}"` }, contentType: "application/json", body: JSON.stringify(savedSettings)
    });
  });
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured })
  }));

  await page.goto(`${baseURL}/system/payment`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "支付配置" })).toBeVisible();
  await expect(page.locator("#youtube-api-key")).toHaveCount(0);
  await expect(page.getByText("YouTube Data API Key", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "前往 YouTube 直播設定" })).toBeVisible();
  await expect(page.locator("body")).not.toContainText("__EASYLOTTERY_SECRET_UNCHANGED__");
  await page.getByRole("button", { name: "儲存支付設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  expect(writes[0]).toMatchObject({
    etag: '"payment-v1"',
    body: { youTube: { apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__" }, enableYouTubeSuperChat: true }
  });

  await page.getByRole("link", { name: "前往 YouTube 直播設定" }).click();
  await expect(page.getByRole("heading", { name: "YouTube 直播設定" })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("尚未設定可用的 YouTube API Key");
  const keyInput = page.getByLabel("YouTube Data API Key");
  await expect(keyInput).toHaveAttribute("type", "password");
  await expect(page.getByLabel("啟用 YouTube Super Chat")).toBeChecked();
  await keyInput.fill("test-only-api-key");
  await page.getByRole("button", { name: "儲存 YouTube 設定" }).click();
  await expect(page.locator(".youtube-status")).toContainText("伺服器已設定 API Key");
  expect(writes[1]).toMatchObject({
    etag: '"payment-v2"',
    body: { youTube: { apiKey: "test-only-api-key" }, enableYouTubeSuperChat: true }
  });
});

test("YouTube setup explains an unavailable status without contacting Google", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/payments", route => route.fulfill({
    status: 200, headers: { ETag: '"payment-v1"' }, contentType: "application/json", body: JSON.stringify(paymentSettings())
  }));
  let statusRequests = 0;
  await page.route("**/api/lottery/youtube/status", route => {
    statusRequests++;
    return route.fulfill({ status: 503, contentType: "application/json", body: "{}" });
  });
  const providerRequests = [];
  page.on("request", request => {
    if (/googleapis\.com|youtube\.com\/youtube/.test(request.url())) providerRequests.push(request.url());
  });

  await page.goto(`${baseURL}/system/youtube-login`, { waitUntil: "networkidle" });
  await expect(page.locator(".youtube-status")).toHaveText("目前無法確認 API Key 狀態，請稍後重試。");
  expect(statusRequests).toBe(1);
  expect(providerRequests).toEqual([]);
});

test("marketplace artwork has accessible previews and a failed image does not block installation", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  const poke = {
    id: 1, publicId: "poke-market", marketSourcePublicId: "00000000-0000-0000-0000-000000000000",
    name: "月光戳戳樂", description: "VTuber 直播用獎品預覽", isBuiltIn: true, publicationStatus: 0, updatedAt: "2026-10-10T00:00:00Z"
  };
  const roulette = {
    id: 2, publicId: "roulette-market", marketSourcePublicId: "00000000-0000-0000-0000-000000000000",
    name: "星夜轉盤", description: "直播抽獎轉盤預覽", isBuiltIn: true, publicationStatus: 0, updatedAt: "2026-10-10T00:00:00Z"
  };
  await page.route("**/api/poke-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([poke]) });
    } else if (path.endsWith("/duplicate")) {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: 23 }) });
    } else if (path.endsWith("/publication-status")) {
      await route.fulfill({ status: 204, body: "" });
    } else {
      await route.fulfill({ status: 404, contentType: "application/json", body: "{}" });
    }
  });
  await page.route("**/api/roulette-templates", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify([roulette])
  }));
  await page.route("**/vue/images/marketplace/pokebox-moonlit-prizes.webp", route => route.fulfill({ status: 404, body: "missing" }));

  await page.goto(`${baseURL}/market`, { waitUntil: "networkidle" });
  await expect(page.getByRole("img", { name: "戳戳樂範本預覽" })).toBeVisible();
  const rouletteImage = page.getByRole("img", { name: "原創動畫風格魔法獎項轉盤預覽" });
  await expect(rouletteImage).toBeVisible();
  await expect(rouletteImage).toHaveAttribute("loading", "lazy");
  await rouletteImage.scrollIntoViewIfNeeded();
  await expect.poll(() => rouletteImage.evaluate(image => image.naturalWidth)).toBeGreaterThan(0);
  const install = page.getByRole("button", { name: "月光戳戳樂：建立可編輯副本" });
  await expect(install).toBeEnabled();
  await install.click();
  await expect(page).toHaveURL(/\/pokebox\?edit=23$/);
});

test("Donate list shows schedule separately from enablement without writing derived status", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  const now = Date.now();
  const activity = (id, name, startsAtUtc, endsAtUtc, isEnabled) => ({
    id, publicId: `donate-${id}`, name, type: 0, minimumDonationAmount: 100,
    startsAtUtc, endsAtUtc, isEnabled, prizes: []
  });
  const activities = [
    activity(1, "待開始但停用", new Date(now + 3_600_000).toISOString(), new Date(now + 7_200_000).toISOString(), false),
    activity(2, "進行中但停用", new Date(now - 3_600_000).toISOString(), new Date(now + 3_600_000).toISOString(), false),
    activity(3, "已結束但啟用", new Date(now - 7_200_000).toISOString(), new Date(now - 3_600_000).toISOString(), true),
    activity(4, "時間不完整", "not-a-date", "", false)
  ];
  const writes = [];
  await page.route("**/api/donate-activities**", async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(activities) });
    } else {
      writes.push(route.request().method());
      await route.fulfill({ status: 204, body: "" });
    }
  });

  await page.goto(`${baseURL}/donate-activities`, { waitUntil: "networkidle" });
  const scheduled = page.getByRole("row", { name: /待開始但停用/ });
  await expect(scheduled.locator(".state-disabled")).toHaveText("設定：停用");
  await expect(scheduled.locator(".schedule-status")).toHaveText("時段：已排程");
  const active = page.getByRole("row", { name: /進行中但停用/ });
  await expect(active.locator(".state-disabled")).toHaveText("設定：停用");
  await expect(active.locator(".schedule-status")).toHaveText("時段：進行中");
  const ended = page.getByRole("row", { name: /已結束但啟用/ });
  await expect(ended.locator(".state-enabled")).toHaveText("設定：啟用");
  await expect(ended.locator(".schedule-status")).toHaveText("時段：已結束");
  await expect(page.getByRole("row", { name: /時間不完整/ }).locator(".schedule-status")).toHaveText("時段：時間未完整");
  expect(writes).toEqual([]);
});
