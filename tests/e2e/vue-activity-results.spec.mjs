import { expect, test } from "./fixtures.mjs";

const sampleResults = [
  {
    id: 7,
    activityType: 0,
    activityName: "週末戳戳樂",
    activityDateUtc: "2026-10-08T12:00:00Z",
    summary: "已揭曉 2 格",
    items: [{ order: 1, name: "特獎", description: "第一格", imageUrl: "", color: "", resultedAtUtc: null }]
  },
  {
    id: 8,
    activityType: 1,
    activityName: "直播轉盤",
    activityDateUtc: "2026-10-09T12:00:00Z",
    summary: "中獎項目：旅行券",
    items: [{ order: 1, name: "旅行券", description: "第 2 格", imageUrl: "", color: "", resultedAtUtc: "2026-10-09T12:00:02Z" }]
  }
];

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
  return session.token;
}

test("activity results route redirects unauthenticated visitors without fetching results", async ({ page, baseURL }) => {
  let resultRequests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/activity-results") resultRequests++;
  });

  await page.goto(`${baseURL}/activity-results`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/activity-results");
  await expect(page.getByRole("region", { name: "管理員 Passkey 登入", exact: true })).toBeVisible();
  expect(resultRequests).toBe(0);
});

test("admin can filter, expand, and export activity results through Vue", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  let sentToken = "";
  await page.route("**/api/activity-results", async route => {
    sentToken = route.request().headers()["x-easylottery-session-token"] ?? "";
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(sampleResults) });
  });

  await page.goto(`${baseURL}/activity-results`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "活動結果", exact: true })).toBeVisible();
  expect(sentToken).toBe(token);
  await expect(page.locator(".result-card")).toHaveCount(2);

  await page.getByLabel("搜尋").fill("特獎");
  await expect(page.locator(".result-card")).toHaveCount(1);
  const card = page.locator(".result-card");
  await expect(card).toContainText("週末戳戳樂");
  await card.getByText("查看活動結果").click();
  await expect(card.getByText("特獎", { exact: true })).toBeVisible();

  const downloadEvent = page.waitForEvent("download");
  await page.getByRole("button", { name: "匯出目前結果" }).click();
  expect((await downloadEvent).suggestedFilename()).toMatch(/^activity-results-.*\.csv$/);
});

test("activity results show retry instead of treating API failure as an empty archive", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let attempts = 0;
  await page.route("**/api/activity-results", async route => {
    attempts++;
    if (attempts === 1) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary failure" }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(sampleResults) });
  });

  await page.goto(`${baseURL}/activity-results`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("temporary failure");
  await expect(page.getByRole("heading", { name: "尚無活動結果紀錄" })).toHaveCount(0);
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.locator(".result-card")).toHaveCount(2);
  expect(attempts).toBe(2);
});

test("Blazor results navigation performs a full-page handoff to the Vue route", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  await page.route("**/api/activity-results", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(sampleResults) });
  });

  await page.goto(baseURL, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "結果與紀錄" }).click();
  const navigation = page.waitForRequest(request => request.isNavigationRequest()
    && request.frame() === page.mainFrame()
    && new URL(request.url()).pathname === "/activity-results");
  await page.getByRole("link", { name: "檢視抽獎結果與紀錄" }).click();
  await navigation;
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
  await expect(page.getByRole("heading", { name: "活動結果", exact: true })).toBeVisible();
});
