import { expect, test } from "./fixtures.mjs";

test("unauthenticated admin settings redirect without requesting settings data", async ({ page, baseURL }) => {
  let settingsRequests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname.startsWith("/api/settings")) settingsRequests++;
  });

  await page.goto(`${baseURL}/system/payment`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/system/payment");
  await expect(page.getByRole("region", { name: "管理員 Passkey 登入", exact: true })).toBeVisible();
  expect(settingsRequests).toBe(0);
});

test("an OBS token cannot restore an admin route", async ({ page, baseURL }) => {
  const payload = btoa(JSON.stringify({ token_use: "obs", exp: Math.floor(Date.now() / 1000) + 3600 }))
    .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  const obsToken = `header.${payload}.signature`;
  let settingsRequests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname.startsWith("/api/settings")) settingsRequests++;
  });
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), obsToken);

  await page.goto(`${baseURL}/system/payment`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(settingsRequests).toBe(0);
});

test("Blazor navigation hands migrated routes to Vue", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  await page.goto(baseURL, { waitUntil: "networkidle" });
  const aboutNavigation = page.waitForRequest(request => request.isNavigationRequest()
    && request.frame() === page.mainFrame()
    && new URL(request.url()).pathname === "/about");
  await page.getByRole("link", { name: "關於 EasyLottery" }).click();
  await aboutNavigation;
  await expect(page.getByRole("heading", { name: "關於 EasyLottery" })).toBeVisible();
  await expect(page.locator("#app[data-v-app]")).toBeVisible();

  await page.goto(baseURL, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: /串接與支付/ }).click();
  const paymentNavigation = page.waitForRequest(request => request.isNavigationRequest()
    && request.frame() === page.mainFrame()
    && new URL(request.url()).pathname === "/system/payment");
  await page.getByRole("link", { name: "支付與 Donate 串接" }).click();
  await paymentNavigation;
  await expect(page.getByRole("heading", { name: "支付配置" })).toBeVisible();
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
});

test("authenticated admin can load and save a Vue settings resource", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  await page.goto(`${baseURL}/system/visual-styles`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "視覺樣式", exact: true })).toBeVisible();
  await page.getByRole("radio").first().check();
  await page.getByRole("button", { name: "儲存設定", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
});

test("payment settings edit environment-specific credentials and enable flags", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  await page.goto(`${baseURL}/system/payment`, { waitUntil: "networkidle" });
  const ecpay = page.locator('[data-provider="ecpay"]');
  await ecpay.getByLabel("Merchant ID").fill("testing-merchant");
  await ecpay.getByLabel("HashKey").fill("testing-hash-key");
  await ecpay.getByLabel("HashIV").fill("testing-hash-iv");
  await ecpay.getByLabel("付款頁 URL").fill("https://example.com/testing-donation");
  await ecpay.getByLabel("啟用").check();
  await page.getByRole("button", { name: "儲存支付設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");

  await page.reload({ waitUntil: "networkidle" });
  await expect(ecpay.getByLabel("Merchant ID")).toHaveValue("testing-merchant");
  await expect(ecpay.getByLabel("HashKey")).toHaveValue("__EASYLOTTERY_SECRET_UNCHANGED__");
  await expect(ecpay.getByLabel("啟用")).toBeChecked();

  await ecpay.getByLabel("環境").selectOption("production");
  await expect(ecpay.getByLabel("啟用")).not.toBeChecked();
  await ecpay.getByLabel("Merchant ID").fill("production-merchant");
  await ecpay.getByLabel("HashKey").fill("production-hash-key");
  await ecpay.getByLabel("HashIV").fill("production-hash-iv");
  await ecpay.getByLabel("付款頁 URL").fill("https://example.com/production-donation");
  await page.getByRole("button", { name: "儲存支付設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");

  await page.reload({ waitUntil: "networkidle" });
  await ecpay.getByLabel("環境").selectOption("production");
  await expect(ecpay.getByLabel("Merchant ID")).toHaveValue("production-merchant");
  await expect(ecpay.getByLabel("HashKey")).toHaveValue("__EASYLOTTERY_SECRET_UNCHANGED__");
  await expect(ecpay.getByLabel("啟用")).not.toBeChecked();

  await ecpay.getByLabel("環境").selectOption("testing");
  await expect(ecpay.getByLabel("Merchant ID")).toHaveValue("testing-merchant");
  await expect(ecpay.getByLabel("啟用")).toBeChecked();

  const providers = [
    ["newebPay", [["Merchant ID", "newpay-merchant"], ["HashKey", "newpay-hash-key"], ["HashIV", "newpay-hash-iv"], ["付款頁 URL", "https://example.com/newpay"]]],
    ["oenTw", [["Creator ID", "oen-creator"], ["Access Token", "oen-access-token"], ["付款頁 URL", "https://example.com/oen"]]],
    ["twitchBits", [["Channel ID", "twitch-channel"], ["Access Token", "twitch-access-token"]]]
  ];
  for (const [key, fields] of providers) {
    const card = page.locator(`[data-provider="${key}"]`);
    await card.getByLabel("環境").selectOption("testing");
    for (const [label, value] of fields) await card.getByLabel(label).fill(value);
    await card.getByLabel("啟用").check();
  }
  await page.getByRole("button", { name: "儲存支付設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");

  await page.reload({ waitUntil: "networkidle" });
  for (const [key, expectedValues] of [
    ["newebPay", [["Merchant ID", "newpay-merchant"], ["HashKey", "__EASYLOTTERY_SECRET_UNCHANGED__"], ["HashIV", "__EASYLOTTERY_SECRET_UNCHANGED__"]]],
    ["oenTw", [["Creator ID", "oen-creator"], ["Access Token", "__EASYLOTTERY_SECRET_UNCHANGED__"]]],
    ["twitchBits", [["Channel ID", "twitch-channel"], ["Access Token", "__EASYLOTTERY_SECRET_UNCHANGED__"]]]
  ]) {
    const card = page.locator(`[data-provider="${key}"]`);
    for (const [label, value] of expectedValues) await expect(card.getByLabel(label)).toHaveValue(value);
    await expect(card.getByLabel("啟用")).toBeChecked();
  }
});

test("payment settings cannot save after a failed initial load until retry succeeds", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  let getCount = 0;
  let putCount = 0;
  await page.route("**/api/settings/payments", async route => {
    if (route.request().method() === "PUT") putCount++;
    if (route.request().method() === "GET" && getCount++ === 0) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "temporary failure" }) });
      return;
    }
    await route.continue();
  });

  await page.goto(`${baseURL}/system/payment`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByRole("button", { name: "儲存支付設定" })).toHaveCount(0);
  expect(putCount).toBe(0);

  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("button", { name: "儲存支付設定" })).toBeVisible();
  await page.getByRole("button", { name: "儲存支付設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  expect(putCount).toBe(1);
});

test("YouTube settings cannot save after a failed initial load until retry succeeds", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  let getCount = 0;
  let putCount = 0;
  await page.route("**/api/settings/payments", async route => {
    if (route.request().method() === "PUT") putCount++;
    if (route.request().method() === "GET" && getCount++ === 0) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "temporary failure" }) });
      return;
    }
    await route.continue();
  });

  await page.goto(`${baseURL}/system/youtube-login`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByRole("button", { name: "儲存 YouTube 設定" })).toHaveCount(0);
  expect(putCount).toBe(0);

  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("button", { name: "儲存 YouTube 設定" })).toBeVisible();
  await page.getByRole("button", { name: "儲存 YouTube 設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  expect(putCount).toBe(1);
});

test("audit settings cannot save after a failed initial load until retry succeeds", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  let getCount = 0;
  let putCount = 0;
  await page.route("**/settings", async route => {
    if (route.request().method() === "PUT") putCount++;
    if (route.request().method() === "GET" && getCount++ === 0) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "temporary failure" }) });
      return;
    }
    await route.continue();
  });

  await page.goto(`${baseURL}/system/audit`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByRole("button", { name: "儲存審計設定" })).toHaveCount(0);
  expect(putCount).toBe(0);

  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("button", { name: "儲存審計設定" })).toBeVisible();
  await page.getByLabel("預設操作人").fill("回歸測試管理員");
  await page.getByRole("button", { name: "儲存審計設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  expect(putCount).toBe(1);
});

test("preset settings cannot save after a failed initial load until retry succeeds", async ({ page, baseURL }) => {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);

  let getCount = 0;
  let putCount = 0;
  await page.route("**/api/settings/visual-style", async route => {
    if (route.request().method() === "PUT") putCount++;
    if (route.request().method() === "GET" && getCount++ === 0) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "temporary failure" }) });
      return;
    }
    await route.continue();
  });

  await page.goto(`${baseURL}/system/visual-styles`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByRole("button", { name: "儲存設定" })).toHaveCount(0);
  expect(putCount).toBe(0);

  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("button", { name: "儲存設定" })).toBeVisible();
  await page.getByRole("radio").first().check();
  await page.getByRole("button", { name: "儲存設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存");
  expect(putCount).toBe(1);
});
