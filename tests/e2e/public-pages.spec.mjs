import { expect, test } from "./fixtures.mjs";

test("anonymous public pages use no admin shell or settings request", async ({ page }) => {
  const settingsResponses = [];
  page.on("response", response => {
    if (new URL(response.url()).pathname.endsWith("/settings")) {
      settingsResponses.push(response.status());
    }
  });

  await page.goto("/privacy-policy", { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "隱私權政策" })).toBeVisible();
  await expect(page.locator(".sidebar")).toHaveCount(0);
  expect(settingsResponses).toEqual([]);

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/about", { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "關於 EasyLottery" })).toHaveCount(1);
  await expect(page.locator(".sidebar")).toHaveCount(0);
  await expect(page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).resolves.toBe(true);
});

test("authenticated public pages do not initialize settings", async ({ page, request }) => {
  const tokenResponse = await request.get("/api/test/session-token");
  expect(tokenResponse.ok()).toBeTruthy();
  const { token } = await tokenResponse.json();
  const settingsRequests = [];

  await page.addInitScript(sessionToken => {
    window.sessionStorage.setItem("easy-lottery.session-token", sessionToken);
  }, token);
  page.on("request", request => {
    if (new URL(request.url()).pathname.includes("/settings")) {
      settingsRequests.push(request.url());
    }
  });

  await page.goto("/about", { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "關於 EasyLottery" })).toBeVisible();
  expect(settingsRequests).toEqual([]);
});

test("public metadata and policy describe the hosted product", async ({ page }) => {
  await page.goto("/privacy-policy", { waitUntil: "networkidle" });

  await expect(page.locator("html")).toHaveAttribute("lang", "zh-Hant");
  await expect(page).toHaveTitle(/EasyLottery/);
  await expect(page.getByText("不會收集、存儲或使用您的任何個人信息")).toHaveCount(0);
  await expect(page.getByText("部署者控制的儲存體")).toBeVisible();
});
