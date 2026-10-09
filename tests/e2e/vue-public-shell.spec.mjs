import { expect, test } from "./fixtures.mjs";

test("Vue public routes do not initialize admin settings", async ({ page }) => {
  const settingsRequests = [];
  page.on("request", request => {
    if (new URL(request.url()).pathname.includes("/settings")) {
      settingsRequests.push(request.url());
    }
  });

  await page.goto("/about", { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "關於 EasyLottery", exact: true })).toBeVisible();
  await expect(page.locator('a[href="/login"]')).toHaveText("管理員登入");

  await page.goto("/privacy-policy", { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "隱私權政策", exact: true })).toBeVisible();
  await expect(page.locator(".v-navigation-drawer")).toHaveCount(0);
  expect(settingsRequests).toEqual([]);
});

test("Vue login validates email before Passkey API", async ({ page }) => {
  let optionsCalls = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/auth/passkey/options") {
      optionsCalls++;
    }
  });

  await page.goto("/login", { waitUntil: "networkidle" });
  await expect(page.locator("#admin-email")).toHaveValue("");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(page.getByRole("alert")).toContainText("請輸入有效的管理員 email");

  await page.locator("#admin-email").fill("not-an-email");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(page.getByRole("alert")).toContainText("請輸入有效的管理員 email");
  expect(optionsCalls).toBe(0);
});
