import { expect, test } from "./fixtures.mjs";

test("unknown page is an HTTP 404 with the public Vue not-found view", async ({ page }) => {
  const response = await page.goto("/this-does-not-exist", { waitUntil: "networkidle" });
  expect(response.status()).toBe(404);
  await expect(page.getByRole("heading", { level: 1, name: "找不到頁面" })).toBeVisible();
  await expect(page).toHaveTitle("找不到頁面｜EasyLottery");
  await expect(page.locator(".sidebar")).toHaveCount(0);
  await page.getByRole("link", { name: "關於 EasyLottery", exact: true }).last().click();
  await expect(page).toHaveURL(/\/about$/);
});

test("unknown API and Hub paths return problem JSON instead of a shell", async ({ request }) => {
  for (const path of ["/api/does-not-exist", "/hubs/does-not-exist"]) {
    for (const method of ["GET", "POST", "PUT", "DELETE", "OPTIONS"]) {
      const response = await request.fetch(path, { method });
      expect(response.status()).toBe(404);
      expect(response.headers()["content-type"]).toContain("application/problem+json");
      expect((await response.json()).status).toBe(404);
    }
  }
});

test("settings browser navigation enters login while legacy requests stay protected", async ({ page, request }) => {
  const documents = [];
  page.on("response", response => {
    if (response.request().isNavigationRequest()) documents.push(response.status());
  });
  await page.goto("/settings", { waitUntil: "networkidle" });
  await expect(page).toHaveURL(/\/login\?redirect=/);
  expect(documents).not.toContain(401);
  expect((await request.get("/settings")).status()).toBe(401);
  expect((await request.head("/settings")).status()).toBe(401);
});
