import { expect, test } from "./fixtures.mjs";

const drawId = "fd366ac8-0dae-4677-a3c6-515b5baf137b";

async function installAdminToken(page) {
  const payload = Buffer.from(JSON.stringify({ token_use: "admin", exp: 2_000_000_000 })).toString("base64url");
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), `synthetic.${payload}.signature`);
}

function record(id, overrides = {}) {
  return {
    drawId: id === 1 ? drawId : `fd366ac8-0dae-4677-a3c6-515b5af137${String(id).padStart(2, "0")}`,
    drawnAtUtc: `2026-10-${String(24 - (id % 10)).padStart(2, "0")}T12:00:00Z`,
    winnerParticipantId: `ticket-winner-${id}`,
    winnerDisplayName: `合成觀眾 ${id}`,
    prizeName: id === 1 ? "合成特獎" : "合成禮盒",
    participantCount: 51,
    eligibleEntryCount: 50,
    ...overrides
  };
}

function ruleSnapshot() {
  return {
    algorithmVersion: "secure-random-index-v1",
    eligibilityPolicyVersion: "not-previously-won-v1",
    groupWinnerPolicyVersion: "exclude-lottery-group-after-win-v1",
    eligibleEntryCount: 50,
    prizeSelectionPolicyVersion: "first-remaining-prize-v1",
    prizeQueue: ["合成特獎", "合成禮盒"]
  };
}

function fullRoster() {
  const roster = [
    { participantId: "ticket-winner-1", lotteryGroupId: "viewer-one", displayName: "合成觀眾 1", externalIdentity: { platform: "youtube", accountId: "synthetic-account-1" }, level: "Gold", group: "Members", tags: ["synthetic"], eligibilityStatus: "eligible" },
    { participantId: "ticket-two-1", lotteryGroupId: "viewer-two", displayName: "第二合成觀眾", externalIdentity: { platform: "twitch", accountId: "synthetic-account-2" }, level: "Silver", group: "Guests", tags: ["candidate"], eligibilityStatus: "eligible" },
    { participantId: "ticket-excluded-1", lotteryGroupId: "viewer-three", displayName: "排除合成觀眾", level: "", group: "Guests", tags: [], eligibilityStatus: "already-won" }
  ];
  for (let index = 4; index <= 51; index++) roster.push({
    participantId: `ticket-${index}`, lotteryGroupId: `viewer-${index}`, displayName: `合成觀眾 ${index}`,
    externalIdentity: { platform: "youtube", accountId: `synthetic-account-${index}` }, level: "", group: "Members",
    tags: ["synthetic"], eligibilityStatus: "eligible"
  });
  return roster;
}

function historyPage(items, page, totalCount, pageSize = 20) {
  return { items, page, pageSize, totalCount, totalPages: Math.ceil(totalCount / pageSize) };
}

test("core lottery history is admin-only and directly refreshable", async ({ page, baseURL }) => {
  let recordRequests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/lottery-draw-records") recordRequests++;
  });
  await page.goto(`${baseURL}/lottery-draw-records`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/lottery-draw-records");
  expect(recordRequests).toBe(0);

  await installAdminToken(page);
  const summary = { ...record(1), ruleSnapshot: ruleSnapshot() };
  await page.route("**/api/lottery-draw-records**", route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/summary")) return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(summary) });
    if (path.endsWith("/participants")) {
      const roster = fullRoster();
      const page = Number(new URL(route.request().url()).searchParams.get("page") ?? "1");
      return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ items: roster.slice((page - 1) * 50, page * 50), page, pageSize: 50, totalCount: roster.length, totalPages: 2 }) });
    }
    return route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(1)], 1, 1)) });
  });
  await page.goto(`${baseURL}/lottery-draw-records`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "抽獎紀錄", exact: true })).toBeVisible();
  await expect(page.locator(".lottery-records-table tbody tr")).toContainText("合成觀眾 1");
  await expect(page.locator(".lottery-records-table tbody tr")).toContainText("合成特獎");
  await expect(page.locator(".lottery-records-table tbody tr")).toContainText(drawId);
  await page.getByRole("button", { name: `檢視紀錄 ${drawId}` }).click();
  await expect(page.getByText("抽獎紀錄明細", { exact: true })).toBeVisible();
  await expect(page.getByLabel("完整參與名單").getByRole("row").filter({ has: page.getByText("合成觀眾 1", { exact: true }) })).toBeVisible();
  await expect(page.getByText("synthetic-account-1", { exact: true })).toBeVisible();
  await expect(page.getByText("not-previously-won-v1", { exact: true })).toBeVisible();
  await expect(page.getByText("已得獎排除", { exact: true })).toBeVisible();
  await expect(page.getByText("共 51 張籤", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "下一頁" }).last().click();
  await expect(page.getByText("第 2 / 2 頁", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "關閉" }).click();
  const nav = page.locator(".admin-drawer .admin-nav");
  await expect(nav.locator("details")).toHaveCount(7);
  await expect(nav.locator('.admin-nav-link[aria-current="page"]')).toHaveCount(1);
  await expect(nav.getByRole("link", { name: "抽獎紀錄" })).toHaveAttribute("aria-current", "page");
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.locator(".lottery-records-table tbody tr")).toHaveCount(1);
});

test("core-history page distinguishes insufficient permission from a read failure", async ({ page, baseURL }) => {
  await installAdminToken(page);
  await page.route("**/api/lottery-draw-records**", route => route.fulfill({
    status: 403, contentType: "application/json", body: JSON.stringify({ error: "forbidden" })
  }));
  await page.goto(`${baseURL}/lottery-draw-records`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("權限不足");
  await expect(page.getByRole("heading", { name: "尚無已保存的抽獎紀錄" })).toHaveCount(0);
});

test("core history handles loading, empty, errors, search, date filters and pagination", async ({ page, baseURL }) => {
  await installAdminToken(page);
  let resolveInitial;
  const initialGate = new Promise(resolve => { resolveInitial = resolve; });
  const seenQueries = [];
  let requests = 0;
  await page.route("**/api/lottery-draw-records**", async route => {
    requests++;
    const query = new URL(route.request().url()).searchParams;
    seenQueries.push(query);
    if (requests === 1) {
      await initialGate;
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage(Array.from({ length: 20 }, (_, index) => record(index + 1)), 1, 21)) });
      return;
    }
    if (query.get("search") === "合成特獎") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(1)], 1, 1)) });
    } else if (query.get("page") === "2") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(21)], 2, 21)) });
    } else if (query.get("search") === "無符合") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([], 1, 0)) });
    } else if (requests === 5) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary" }) });
    } else {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(historyPage([], 1, 0)) });
    }
  });

  await page.goto(`${baseURL}/lottery-draw-records`, { waitUntil: "domcontentloaded" });
  await expect(page.getByRole("status")).toContainText("正在載入抽獎紀錄");
  resolveInitial();
  await expect(page.locator(".lottery-records-table tbody tr")).toHaveCount(20);
  await page.getByRole("button", { name: "下一頁" }).click();
  await expect(page.getByText("第 2 / 2 頁", { exact: true })).toBeVisible();
  expect(seenQueries.at(-1).get("page")).toBe("2");

  await page.getByLabel("搜尋得獎者或獎項").fill("合成特獎");
  await page.getByLabel("開始日期").fill("2026-10-02");
  await page.getByLabel("結束日期").fill("2026-10-24");
  await page.getByRole("button", { name: "套用篩選" }).click();
  await expect(page.locator(".lottery-records-table tbody tr")).toHaveCount(1);
  expect(seenQueries.at(-1).get("search")).toBe("合成特獎");
  expect(seenQueries.at(-1).get("fromUtc")).toBeTruthy();
  expect(seenQueries.at(-1).get("beforeUtc")).toBeTruthy();

  await page.getByLabel("搜尋得獎者或獎項").fill("無符合");
  await page.getByRole("button", { name: "套用篩選" }).click();
  await expect(page.getByRole("heading", { name: "找不到符合條件的抽獎紀錄" })).toBeVisible();
  await page.getByLabel("抽獎紀錄篩選").getByRole("button", { name: "清除篩選" }).click();
  await expect(page.getByRole("alert")).toContainText("載入失敗");
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("heading", { name: "尚無已保存的抽獎紀錄" })).toBeVisible();
});

test("narrow view keeps core-history page visible and marks one active nav item", async ({ page, baseURL }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await installAdminToken(page);
  await page.route("**/api/lottery-draw-records**", route => route.fulfill({
    status: 200, contentType: "application/json", body: JSON.stringify(historyPage([record(1)], 1, 1))
  }));
  await page.goto(`${baseURL}/lottery-draw-records`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "開啟管理功能導覽" }).click();
  const nav = page.locator(".admin-nav--mobile");
  await expect(nav).toBeVisible();
  await expect(nav.locator('.admin-nav-link[aria-current="page"]')).toHaveCount(1);
  await expect(nav.getByRole("link", { name: "抽獎紀錄", exact: true })).toHaveAttribute("aria-current", "page");
  await expect(page.locator(".lottery-records-table")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
