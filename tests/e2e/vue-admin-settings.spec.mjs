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
