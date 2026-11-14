import { expect, test } from "../fixtures.mjs";

test.beforeEach(async ({ page }) => {
  await page.goto("/login");
});

test("login email starts blank and uses email autocomplete", async ({ page }) => {
  const email = page.getByLabel("管理員 email");
  await expect(email).toHaveValue("");
  await expect(email).toHaveAttribute("type", "email");
  await expect(email).toHaveAttribute("autocomplete", "username");
});

for (const value of ["", "not-an-email"]) {
  test(`invalid email "${value}" is rejected client-side`, async ({ page }) => {
    const authRequests = [];
    page.on("request", request => {
      if (new URL(request.url()).pathname.startsWith("/api/auth/")) authRequests.push(request.url());
    });
    await page.getByLabel("管理員 email").fill(value);
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(page.getByRole("alert")).toHaveText("請輸入有效的管理員 email。");
    expect(authRequests).toEqual([]);
  });
}

test("login page has a single h1", async ({ page }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/321");
  await expect(page.getByRole("heading", { level: 1, name: "管理員 Passkey 登入" })).toHaveCount(1, { timeout: 2_000 });
});

test("invalid email marks the field invalid and links the error", async ({ page }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/321");
  const email = page.getByLabel("管理員 email");
  await email.fill("not-an-email");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(email).toHaveAttribute("aria-invalid", "true", { timeout: 2_000 });
  const describedBy = await email.getAttribute("aria-describedby");
  await expect(page.locator(`#${describedBy}`)).toContainText("請輸入有效的管理員 email");
});

test("non-admin email does not reveal the admin allowlist", async ({ request }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/319");
  const response = await request.post("/api/auth/passkey/options", {
    data: { email: "not-the-admin@example.com" }
  });
  const body = await response.text();
  expect(response.status()).not.toBe(403);
  expect(body).not.toContain("admin_email_required");
});
