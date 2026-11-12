import { expect, test } from "./fixtures.mjs";

for (const width of [320, 375, 768, 1280]) {
  test(`public navigation and login title fit at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 740 });
    for (const path of ["/about", "/privacy-policy", "/login"]) {
      await page.goto(path);
      await expect(page.locator(".public-nav")).toBeVisible();
      for (const name of ["關於", "隱私權政策", "管理員登入"]) {
        const link = page.getByRole("link", { name, exact: true });
        await expect(link).toBeVisible();
        expect((await link.boundingBox()).height).toBeLessThan(48);
        expect(await link.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    }
    const heading = page.getByRole("heading", { level: 1, name: "管理員 Passkey 登入" });
    await expect(heading).toBeVisible();
    expect(await heading.evaluate(node => node.scrollWidth <= node.clientWidth && getComputedStyle(node).whiteSpace !== "nowrap")).toBe(true);
    await page.getByRole("link", { name: "關於", exact: true }).focus();
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(/\/about$/);
  });
}

test("login validation links and clears only field errors", async ({ page }) => {
  await page.goto("/login");
  await expect(page.locator("h1")).toHaveCount(1);
  const email = page.getByLabel("管理員 email");
  await expect(email).not.toHaveAttribute("aria-invalid", "true");
  for (const value of ["", "invalid"]) {
    await email.fill(value);
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(email).toHaveAttribute("aria-invalid", "true");
    await expect(email).toHaveAttribute("aria-describedby", "admin-email-error");
    await expect(page.locator("#admin-email-error")).toHaveText("請輸入有效的管理員 email。");
    await email.fill("admin@example.test");
    await expect(email).not.toHaveAttribute("aria-invalid", "true");
    await expect(email).not.toHaveAttribute("aria-describedby");
    await expect(page.getByRole("alert")).toHaveCount(0);
  }
  await page.route("**/api/auth/passkey/options", route => route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "登入服務暫時無法使用。" }) }));
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(page.getByRole("alert")).toBeVisible();
  await expect(email).not.toHaveAttribute("aria-invalid", "true");
});

test("About distinguishes supported record categories", async ({ page }) => {
  await page.goto("/about");
  await expect(page.getByText("依功能管理紀錄", { exact: true })).toBeVisible();
  await expect(page.locator("main")).toContainText("依各功能的記錄操作與查詢頁面");
  await expect(page.locator("main")).not.toContainText("抽獎結果與稽核紀錄分離保存");
});

test("public favicon is an actual PNG", async ({ page, request }) => {
  await page.goto("/about");
  const href = await page.locator('link[rel="icon"]').getAttribute("href");
  const response = await request.get(new URL(href, page.url()).href);
  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toContain("image/png");
  expect((await response.body()).subarray(0, 8).toString("hex")).toBe("89504e470d0a1a0a");
});

test("root robots only allows exact public information pages", async ({ request }) => {
  const response = await request.get("/robots.txt");
  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toContain("text/plain");
  const body = await response.text();
  for (const line of ["User-agent: *", "Allow: /about$", "Allow: /privacy-policy$", "Disallow: /obs/", "Disallow: /system/", "Disallow: /api/", "Disallow: /hubs/", "Disallow: /legacy", "Disallow: /"]) {
    expect(body.split(/\r?\n/)).toContain(line);
  }
});
