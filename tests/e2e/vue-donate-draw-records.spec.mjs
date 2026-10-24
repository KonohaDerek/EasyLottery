import { expect, test } from "./fixtures.mjs";

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
}

function record(id, overrides = {}) {
  return {
    id,
    activityId: 7,
    activityName: "週末 Donate 活動",
    drawnAtUtc: `2026-10-${String(24 - (id % 10)).padStart(2, "0")}T12:00:00Z`,
    donorName: `測試觀眾 ${id}`,
    amount: 100,
    drawCount: 1,
    prizeName: id === 1 ? "特獎" : "銘謝惠顧",
    isWinning: id === 1,
    ...overrides
  };
}

function historyPage(items, page, totalCount, pageSize = 20) {
  return { items, page, pageSize, totalCount, totalPages: Math.ceil(totalCount / pageSize) };
}

test("Donate draw history is an authenticated direct route with one active nav item", async ({ page, baseURL }) => {
  let recordRequests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/donate-lottery-draw-records") recordRequests++;
  });
  await page.goto(`${baseURL}/donate-lottery-records`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/donate-lottery-records");
  expect(recordRequests).toBe(0);
});

test("history loads, filters, paginates, refreshes, and keeps result types distinct", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/donate-activities", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify([{ id: 7, name: "週末 Donate 活動" }])
  }));
  const firstPageRecords = Array.from({ length: 20 }, (_, index) => record(index + 1));
  let initialResponseReady;
  const initialResponse = new Promise(resolve => { initialResponseReady = resolve; });
  let holdInitialResponse = true;
  const observedQueries = [];
  await page.route("**/api/donate-lottery-draw-records**", async route => {
    const query = new URL(route.request().url()).searchParams;
    observedQueries.push(query);
    const pageNumber = Number(query.get("page") ?? 1);
    if (holdInitialResponse) {
      holdInitialResponse = false;
      await initialResponse;
    }
    if (query.get("search") === "特獎") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(1)], 1, 1)) });
    } else if (pageNumber === 2) {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(21)], 2, 21)) });
    } else {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage(firstPageRecords, 1, 21)) });
    }
  });

  await page.goto(`${baseURL}/donate-lottery-records`, { waitUntil: "domcontentloaded" });
  await expect(page.getByRole("heading", { name: "Donate 抽獎紀錄", exact: true })).toBeVisible();
  await expect(page.getByRole("status")).toContainText("正在載入 Donate 抽獎紀錄");
  initialResponseReady();
  await expect(page.locator(".records-table tbody tr")).toHaveCount(20);
  await expect(page.locator(".records-outcome").first()).toHaveText("中獎");
  await expect(page.locator(".records-outcome").nth(1)).toHaveText("未中獎");
  await expect(page.getByText("特獎", { exact: true })).toBeVisible();
  await expect(page.getByText("private-payment-id")).toHaveCount(0);

  const nav = page.locator(".admin-drawer .admin-nav");
  await expect(nav.locator("details")).toHaveCount(7);
  await expect(nav.locator('.admin-nav-link[aria-current="page"]')).toHaveCount(1);
  await expect(nav.getByRole("link", { name: "Donate 抽獎紀錄" })).toHaveAttribute("aria-current", "page");
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.locator(".records-table tbody tr")).toHaveCount(20);

  await page.getByRole("button", { name: "下一頁" }).click();
  await expect(page.locator(".records-table tbody tr")).toHaveCount(1);
  await expect(page.getByText("第 2 / 2 頁")).toBeVisible();
  expect(observedQueries.at(-1).get("page")).toBe("2");

  await page.getByLabel("搜尋活動、觀眾或獎項").fill("特獎");
  await page.getByLabel("Donate 活動").selectOption("7");
  await page.getByLabel("結果類型").selectOption("winning");
  await page.getByLabel("開始日期").fill("2026-10-02");
  await page.getByLabel("結束日期").fill("2026-10-24");
  await page.getByRole("button", { name: "套用篩選" }).click();
  await expect(page.locator(".records-table tbody tr")).toHaveCount(1);
  const filteredQuery = observedQueries.at(-1);
  expect(filteredQuery.get("activityId")).toBe("7");
  expect(filteredQuery.get("outcome")).toBe("winning");
  expect(filteredQuery.get("fromUtc")).toBeTruthy();
  expect(filteredQuery.get("beforeUtc")).toBeTruthy();
});

test("history errors offer retry and an empty response is not reported as an error", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/donate-activities", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  let requests = 0;
  await page.route("**/api/donate-lottery-draw-records**", route => {
    requests++;
    return route.fulfill(requests === 1
      ? { status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary failure" }) }
      : { status: 200, contentType: "application/json", body: JSON.stringify(historyPage([], 1, 0)) });
  });
  await page.goto(`${baseURL}/donate-lottery-records`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("紀錄載入失敗");
  await expect(page.getByRole("heading", { name: "尚無 Donate 抽獎紀錄" })).toHaveCount(0);
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("heading", { name: "尚無 Donate 抽獎紀錄" })).toBeVisible();
  expect(requests).toBe(2);
});

test("narrow navigation opens the record group and keeps the page within the viewport", async ({ page, baseURL }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await installAdminToken(page, baseURL);
  await page.route("**/api/donate-activities", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route("**/api/donate-lottery-draw-records**", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(1)], 1, 1))
  }));
  await page.goto(`${baseURL}/donate-lottery-records`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "開啟管理功能導覽" }).click();
  const nav = page.locator(".admin-nav--mobile");
  await expect(nav).toBeVisible();
  await expect(nav.locator('.admin-nav-link[aria-current="page"]')).toHaveCount(1);
  await expect(nav.getByRole("link", { name: "Donate 抽獎紀錄" })).toHaveAttribute("aria-current", "page");
  await expect(page.locator(".records-table")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
