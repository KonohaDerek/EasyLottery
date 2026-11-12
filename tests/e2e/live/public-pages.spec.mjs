import { expect, test } from "../fixtures.mjs";

const pages = [
  { path: "/about", heading: "關於 EasyLottery" },
  { path: "/privacy-policy", heading: "隱私權政策" }
];

for (const { path, heading } of pages) {
  test(`${path} renders the public shell`, async ({ page }) => {
    await page.goto(path);
    await expect(page.locator("html")).toHaveAttribute("lang", "zh-Hant");
    await expect(page).toHaveTitle(/EasyLottery/);
    await expect(page.getByRole("heading", { level: 1, name: heading })).toHaveCount(1);
    await expect(page.getByRole("link", { name: "管理員登入" })).toBeVisible();
  });
}

test("public nav links work", async ({ page }) => {
  await page.goto("/about");
  await page.getByRole("link", { name: "隱私權政策" }).click();
  await expect(page).toHaveURL(/\/privacy-policy$/);
  await page.getByRole("link", { name: "管理員登入" }).click();
  await expect(page).toHaveURL(/\/login/);
});

test("public pages have no horizontal overflow on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 740 });
  for (const path of ["/about", "/privacy-policy", "/login"]) {
    await page.goto(path);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), path).toBe(true);
  }
});

test("mobile nav labels and login title are not wrapped or truncated", async ({ page }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/320");
  await page.setViewportSize({ width: 375, height: 740 });
  await page.goto("/login");
  for (const name of ["關於", "隱私權政策", "管理員登入"]) {
    const box = await page.getByRole("link", { name, exact: true }).boundingBox();
    expect(box.height, name).toBeLessThan(48);
  }
  const title = page.getByText("管理員 Passkey 登入", { exact: true });
  expect(await title.evaluate(node => node.scrollWidth <= node.clientWidth)).toBe(true);
});

test("Vue shell declares a reachable favicon", async ({ page, request }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/323");
  await page.goto("/about");
  const href = await page.locator("link[rel~='icon']").first().getAttribute("href", { timeout: 2_000 });
  expect((await request.get(href)).ok()).toBe(true);
});

test("robots.txt keeps admin and OBS routes out of indexes", async ({ request }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/323");
  const response = await request.get("/robots.txt");
  expect(response.status()).toBe(200);
  expect(await response.text()).toContain("Disallow: /obs/");
});
