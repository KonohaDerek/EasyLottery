import { expect, test } from "./fixtures.mjs";

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
}

test("unauthenticated root redirects to Passkey login without loading admin APIs", async ({ page, baseURL }) => {
  const apiRequests = [];
  page.on("request", request => {
    const path = new URL(request.url()).pathname;
    if (path.startsWith("/api/settings") || path.startsWith("/api/lottery/youtube")) apiRequests.push(path);
  });

  await page.goto(baseURL, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/");
  await expect(page.getByRole("region", { name: "管理員 Passkey 登入", exact: true })).toBeVisible();
  expect(apiRequests).toEqual([]);
});

test("the legacy Blazor lottery page remains reachable for rollback", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.goto(`${baseURL}/legacy`, { waitUntil: "networkidle" });
  await expect(page.getByText("匯入YT會員名單")).toBeVisible();
  await expect(page.locator("#app[data-v-app]")).toHaveCount(0);
});

test("admin can draw in Vue and capture mocked YouTube chat without contacting YouTube directly", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  const receivedRates = [];
  const liveMessageAt = new Date(Date.now() + 30_000).toISOString();

  await page.route("**/api/settings/drawing-rules", async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ status: 200, headers: { ETag: '"rates-v1"' }, contentType: "application/json", body: JSON.stringify({ levelRates: { Gold: 2 } }) });
      return;
    }
    receivedRates.push({ header: route.request().headers()["if-match"], body: route.request().postDataJSON() });
    await route.fulfill({ status: 200, headers: { ETag: '"rates-v2"' }, contentType: "application/json", body: JSON.stringify({ levelRates: { Gold: 4 } }) });
  });
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: true })
  }));
  await page.route("**/api/lottery/youtube/live-chat/resolve", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ chatId: "chat-123" })
  }));
  await page.route("**/api/lottery/youtube/live-chat/chat-123/messages", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify([{
      id: "chat-message-1", type: "TextMessageEvent", authorName: "Viewer", displayMessage: "抽獎", publishedAt: liveMessageAt
    }])
  }));

  const providerRequests = [];
  page.on("request", request => {
    if (/googleapis\.com|youtube\.com\/youtube/.test(request.url())) providerRequests.push(request.url());
  });
  await page.goto(baseURL, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "抽獎工作區", exact: true })).toBeVisible();
  await expect(page.getByText("API 已設定")).toBeVisible();
  await expect(page.getByLabel("Gold 會員倍率")).toHaveValue("2");

  await page.getByLabel("Gold 會員倍率").fill("4");
  await page.getByRole("button", { name: "儲存倍率" }).click();
  await expect(page.locator(".lottery-alert.notice")).toContainText("會員等級倍率已儲存");
  expect(receivedRates).toEqual([{ header: '"rates-v1"', body: { levelRates: { Gold: 4 } } }]);

  await page.getByLabel("新增獎項").fill("動畫限定禮盒");
  await page.getByRole("button", { name: "新增獎項" }).click();
  await page.getByLabel("參加者名稱").fill("Mika");
  await page.getByRole("button", { name: "新增參加者" }).click();
  await page.getByRole("button", { name: "抽獎", exact: true }).click();
  await expect(page.locator(".winner-list")).toContainText("Mika");
  await expect(page.locator(".winner-list")).toContainText("動畫限定禮盒");

  await page.getByLabel("直播網址").fill("https://www.youtube.com/watch?v=mock-video");
  await page.getByLabel(/通關密語/).fill("抽獎");
  await page.getByRole("button", { name: "開始擷取" }).click();
  await expect(page.locator(".chat-message-list")).toContainText("Viewer", { timeout: 12_000 });
  await expect(page.locator(".participant-list")).toContainText("Viewer");
  expect(providerRequests).toEqual([]);
});

test("canceling a pending chat connection does not start a stale polling session", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/drawing-rules", route => route.fulfill({
    status: 200, headers: { ETag: '"rates-v1"' }, contentType: "application/json", body: JSON.stringify({ levelRates: {} })
  }));
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: true })
  }));
  let resolveStarted;
  const resolveRequestStarted = new Promise(resolve => { resolveStarted = resolve; });
  let messageRequests = 0;
  await page.route("**/api/lottery/youtube/live-chat/resolve", async route => {
    resolveStarted();
    await new Promise(resolve => setTimeout(resolve, 700));
    try {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ chatId: "stale-chat" }) });
    } catch { /* The browser aborts this response after the user cancels. */ }
  });
  await page.route("**/api/lottery/youtube/live-chat/**/messages", async route => {
    messageRequests++;
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });

  await page.goto(baseURL, { waitUntil: "networkidle" });
  await page.getByLabel("直播網址").fill("https://www.youtube.com/watch?v=mock-video");
  await page.getByLabel(/通關密語/).fill("抽獎");
  await page.getByRole("button", { name: "開始擷取" }).click();
  await resolveRequestStarted;
  await page.getByRole("button", { name: "取消連線" }).click();
  await expect(page.getByRole("button", { name: "開始擷取" })).toBeVisible();
  await expect(page.locator(".chat-status")).toContainText("已停止");
  await page.waitForTimeout(800);
  expect(messageRequests).toBe(0);
});

test("saving rates keeps edits made while the save is in flight dirty", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let resolveFirstSave;
  let markFirstSaveStarted;
  const firstSaveStarted = new Promise(resolve => { markFirstSaveStarted = resolve; });
  const firstSaveGate = new Promise(resolve => { resolveFirstSave = resolve; });
  const writes = [];
  await page.route("**/api/settings/drawing-rules", async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ status: 200, headers: { ETag: '"rates-v1"' }, contentType: "application/json", body: JSON.stringify({ levelRates: { Gold: 2 } }) });
      return;
    }
    writes.push({ etag: route.request().headers()["if-match"], body: route.request().postDataJSON() });
    if (writes.length === 1) {
      markFirstSaveStarted();
      await firstSaveGate;
    }
    await route.fulfill({ status: 200, headers: { ETag: `"rates-v${writes.length + 1}"` }, contentType: "application/json", body: JSON.stringify({ levelRates: { Gold: writes.at(-1).body.levelRates.Gold } }) });
  });
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: false })
  }));

  await page.goto(baseURL, { waitUntil: "networkidle" });
  const rate = page.getByLabel("Gold 會員倍率");
  await rate.fill("3");
  await page.getByRole("button", { name: "儲存倍率" }).click();
  await firstSaveStarted;
  await rate.fill("4");
  resolveFirstSave();

  await expect(page.locator(".lottery-alert.notice")).toContainText("期間新增的變更仍未儲存");
  await expect(page.getByRole("button", { name: "儲存倍率" })).toBeEnabled();
  await page.getByRole("button", { name: "儲存倍率" }).click();
  await expect.poll(() => writes.length).toBe(2);
  expect(writes).toEqual([
    { etag: '"rates-v1"', body: { levelRates: { Gold: 3 } } },
    { etag: '"rates-v2"', body: { levelRates: { Gold: 4 } } }
  ]);
  await expect(page.locator(".lottery-alert.notice")).toContainText("會員等級倍率已儲存");
});

test("missing DrawingRules ETag disables saves and never sends an overwrite", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let writes = 0;
  await page.route("**/api/settings/drawing-rules", async route => {
    if (route.request().method() === "PUT") writes++;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ levelRates: {} }) });
  });
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: false })
  }));

  await page.goto(baseURL, { waitUntil: "networkidle" });
  await page.locator(".import-panel").first().locator("textarea").fill(JSON.stringify([{ name: "Viewer", level: "Gold" }]));
  await page.getByRole("button", { name: "匯入名單" }).click();
  await expect(page.getByRole("button", { name: "儲存倍率" })).toBeDisabled();
  expect(writes).toBe(0);
});

test("YouTube member import waits for DrawingRules before expanding ticket weights", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let releaseRules;
  let markRulesStarted;
  const rulesStarted = new Promise(resolve => { markRulesStarted = resolve; });
  const rulesGate = new Promise(resolve => { releaseRules = resolve; });
  await page.route("**/api/settings/drawing-rules", async route => {
    markRulesStarted();
    await rulesGate;
    await route.fulfill({ status: 200, headers: { ETag: '"rates-v1"' }, contentType: "application/json", body: JSON.stringify({ levelRates: { Gold: 5 } }) });
  });
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: false })
  }));

  await page.goto(baseURL, { waitUntil: "domcontentloaded" });
  const fileInput = page.getByLabel("YouTube Studio 會員 CSV");
  await rulesStarted;
  await expect(fileInput).toBeDisabled();
  releaseRules();
  await expect(fileInput).toBeEnabled();
  await fileInput.setInputFiles({
    name: "members.csv",
    mimeType: "text/csv",
    buffer: Buffer.from("會員,目前級別\nMika,Gold\n", "utf8")
  });
  await expect(page.locator(".participant-list li")).toHaveCount(5);
});

test("YouTube member import stays disabled when DrawingRules fail to load", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/drawing-rules", route => route.fulfill({ status: 503, contentType: "application/json", body: "{}" }));
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: false })
  }));

  await page.goto(baseURL, { waitUntil: "networkidle" });
  await expect(page.getByLabel("YouTube Studio 會員 CSV")).toBeDisabled();
  await expect(page.locator(".lottery-alert.error")).toContainText("抽獎倍率載入失敗");
});

test("lottery workspace fits a phone viewport without a hidden drawer gap", async ({ page, baseURL }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/drawing-rules", route => route.fulfill({
    status: 200, headers: { ETag: '"rates-v1"' }, contentType: "application/json", body: JSON.stringify({ levelRates: {} })
  }));
  await page.route("**/api/lottery/youtube/status", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify({ configured: false })
  }));
  await page.goto(baseURL, { waitUntil: "networkidle" });

  const layout = await page.evaluate(() => ({
    viewportWidth: window.innerWidth,
    documentWidth: document.documentElement.scrollWidth,
    contentLeft: document.querySelector(".admin-content").getBoundingClientRect().left,
    heroRight: document.querySelector(".lottery-hero").getBoundingClientRect().right
  }));
  expect(layout.documentWidth).toBeLessThanOrEqual(layout.viewportWidth);
  expect(layout.contentLeft).toBeLessThan(40);
  expect(layout.heroRight).toBeLessThanOrEqual(layout.viewportWidth);

  await page.getByRole("button", { name: "管理選單" }).click();
  await page.getByRole("link", { name: "Donate 活動" }).click();
  await expect(page).toHaveURL(/\/donate-activities$/);
});
