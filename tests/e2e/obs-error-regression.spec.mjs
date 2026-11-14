import { expect, test } from "./fixtures.mjs";

const unknownId = "00000000-0000-0000-0000-000000000000";
const recovery = "請回管理頁重新複製 OBS 網址";
const overlays = [
  { path: "/obs/overtime", endpoint: "**/settings" },
  { path: `/obs/roulette/${unknownId}`, endpoint: "**/api/roulette-templates/public/*" },
  { path: `/obs/pokebox/${unknownId}`, endpoint: "**/api/poke-templates/public/*" },
  { path: "/obs/donate/abc", endpoint: "**/settings" },
];

// Deterministic transport failures, rendered by the real Vue application. No production traffic.
for (const overlay of overlays) {
  for (const [status, wording] of [[401, "工作階段無效或已過期"], [403, "權限不足"], [404, "找不到此"], [429, "請稍候再試"], [503, "請稍後重新整理"]]) {
    test(`${overlay.path} visibly explains HTTP ${status} without controls`, async ({ page }) => {
      await page.route("**/api/overtime-feed", route => route.fulfill({ json: [] }));
      await page.route(overlay.endpoint, route => route.fulfill({ status, json: { error: "private transport detail" } }));
      await page.goto(overlay.path);
      const error = page.getByRole("alert");
      await expect(error).toBeVisible();
      await expect(error).toContainText(wording);
      await expect(error).not.toContainText("private transport detail");
      if (status !== 429) await expect(error).toContainText(recovery);
      await expect(page.locator(".sidebar")).toHaveCount(0);
      await expect(error.getByRole("button")).toHaveCount(0);
    });
  }

  test(`${overlay.path} explains a network failure`, async ({ page }) => {
    await page.route("**/api/overtime-feed", route => route.fulfill({ json: [] }));
    await page.route(overlay.endpoint, route => route.abort("failed"));
    await page.goto(overlay.path);
    await expect(page.getByRole("alert")).toContainText("請確認網路連線後重新整理");
  });
}

for (const kind of ["roulette", "pokebox"]) {
  test(`${kind} malformed URL remains actionable and controls retain reload`, async ({ page }) => {
    await page.goto(`/obs/${kind}/abc?controls=1`);
    await expect(page.getByRole("alert")).toContainText(`OBS 網址無效，${recovery}`);
    await expect(page.getByRole("button", { name: "重新載入" })).toBeVisible();
  });
}

test("Donate unknown activity in a successful snapshot is not silently blank", async ({ page }) => {
  await page.route("**/settings", route => route.fulfill({ contentType: "text/yaml", body: "DonateLotteryActivities: []\n" }));
  await page.goto("/obs/donate/abc");
  await expect(page.getByRole("alert")).toContainText(`找不到此 Donate 活動，${recovery}`);
});

test("valid idle Donate stays transparent through a transient poll failure and recovers", async ({ page }) => {
  let mode = "valid";
  let polls = 0;
  await page.route("**/settings", route => {
    polls++;
    if (mode === "failed") return route.fulfill({ status: 503, body: "unavailable" });
    return route.fulfill({ contentType: "text/yaml", body: mode === "missing"
      ? "DonateLotteryActivities: []\n"
      : "DonateLotteryActivities:\n  - Id: 1\n    PublicId: idle-test\n    Name: Idle\nDonateLotteryDrawRecords: []\n" });
  });
  await page.goto("/obs/donate/idle-test");
  await expect.poll(() => polls).toBeGreaterThan(0);
  await expect(page.locator(".donate-obs-shell")).toBeEmpty();
  mode = "failed";
  const previousPolls = polls;
  await expect.poll(() => polls).toBeGreaterThan(previousPolls);
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.locator(".donate-reveal")).toHaveCount(0);
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).backgroundColor)).toBe("rgba(0, 0, 0, 0)");
  mode = "missing";
  await expect(page.getByRole("alert")).toContainText("找不到此 Donate 活動");
  mode = "valid";
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.locator(".donate-obs-shell")).toBeEmpty();
});

test("idle Donate controls show polling errors and clear them after recovery", async ({ page }) => {
  let failed = false;
  let polls = 0;
  await page.route("**/settings", route => {
    polls++;
    return failed
      ? route.fulfill({ status: 503, body: "unavailable" })
      : route.fulfill({ contentType: "text/yaml", body: "DonateLotteryActivities:\n  - Id: 1\n    PublicId: idle-test\n    Name: Idle\nDonateLotteryDrawRecords: []\n" });
  });
  await page.goto("/obs/donate/idle-test?controls=1");
  await expect.poll(() => polls).toBeGreaterThan(0);
  await expect(page.locator(".donate-obs-shell")).toBeEmpty();
  await expect(page.getByRole("alert")).toHaveCount(0);
  failed = true;
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(page.getByRole("alert")).toContainText("請稍後重新整理");
  await expect(page.locator(".donate-reveal")).toHaveCount(0);
  failed = false;
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.locator(".donate-obs-shell")).toBeEmpty();
});

test("valid idle Overtime stays transparent", async ({ page }) => {
  await page.route("**/settings", route => route.fulfill({ contentType: "text/yaml", body: "overtimeOverlay:\n  sessionState: idle\n" }));
  await page.route("**/api/overtime-feed", route => route.fulfill({ json: [] }));
  await page.goto("/obs/overtime");
  await expect(page.locator(".overtime-overlay")).toHaveCount(0);
  await expect(page.getByRole("alert")).toHaveCount(0);
});

// Run with the Development API and isolated E2E storage, not the Vite-only test server.
test.describe("integrated OBS errors", () => {
  for (const path of ["/obs/overtime", "/obs/donate/abc"]) {
    test(`${path} without a session shows recovery instructions`, async ({ page }) => {
      await page.goto(path);
      await expect(page.getByRole("alert")).toContainText(`工作階段無效或已過期，${recovery}`);
    });
  }

  test("unknown roulette id shows the resource-specific missing message", async ({ page }) => {
    await page.goto(`/obs/roulette/${unknownId}`);
    await expect(page.getByRole("alert")).toContainText(`找不到此轉盤活動，${recovery}`);
  });

  test("authorized Donate unknown key shows a missing activity message", async ({ page, request }) => {
    const response = await request.get("/api/test/session-token");
    expect(response.ok()).toBeTruthy();
    const { token } = await response.json();
    await page.goto(`/obs/donate/abc#sessionToken=${encodeURIComponent(token)}`);
    await expect(page.getByRole("alert")).toContainText(`找不到此 Donate 活動，${recovery}`);
    expect(page.url()).not.toContain("sessionToken=");
  });
});
