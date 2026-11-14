import { expect, test } from "../fixtures.mjs";

const protectedRoutes = ["/", "/system/access", "/activity-results", "/roulette/preview/1", "/market", "/interactions/identity-link"];

for (const route of protectedRoutes) {
  test(`anonymous ${route} redirects to login with a return path`, async ({ page }) => {
    await page.goto(route);
    await expect(page).toHaveURL(url => url.pathname === "/login" && url.searchParams.get("redirect") === route);
    await expect(page.getByRole("button", { name: "使用 Passkey 登入" })).toBeVisible();
  });
}

test("health endpoints respond", async ({ request }) => {
  for (const path of ["/health/live", "/health/ready"]) {
    const response = await request.get(path);
    expect(response.status(), path).toBe(200);
  }
});

test("HTTPS responses carry security headers", async ({ request }) => {
  const headers = (await request.get("/about")).headers();
  expect(headers["strict-transport-security"]).toContain("max-age=");
  expect(headers["content-security-policy"]).toContain("frame-ancestors 'none'");
  expect(headers["x-content-type-options"]).toBe("nosniff");
  expect(headers["referrer-policy"]).toBeTruthy();
  expect(headers["permissions-policy"]).toBeTruthy();
});

test("plain HTTP redirects to HTTPS", async ({ request, baseURL }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/253");
  const httpUrl = baseURL.replace(/^https:/, "http:");
  const response = await request.get(`${httpUrl}/`, { maxRedirects: 0 });
  expect([301, 308]).toContain(response.status());
  expect(response.headers().location).toMatch(/^https:/);
});

test("unknown page returns 404 instead of the legacy Blazor shell", async ({ request }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/318");
  const response = await request.get("/this-route-does-not-exist");
  expect(response.status()).toBe(404);
});

test("unknown API path returns a non-HTML 404", async ({ request }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/318");
  const response = await request.get("/api/this-endpoint-does-not-exist");
  expect(response.status()).toBe(404);
  expect(response.headers()["content-type"] ?? "").not.toContain("text/html");
});

test("navigating to /settings in a browser does not show a bare 401", async ({ page }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/318");
  const response = await page.goto("/settings");
  expect(response?.status()).not.toBe(401);
});
