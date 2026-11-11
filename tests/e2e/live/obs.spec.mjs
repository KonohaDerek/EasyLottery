import { expect, test } from "../fixtures.mjs";

test("overtime OBS without a session explains how to recover", async ({ page }) => {
  await page.goto("/obs/overtime");
  await expect(page.getByText(/工作階段無效或已過期/)).toBeVisible();
  await expect(page.locator(".sidebar")).toHaveCount(0);
});

test("roulette OBS with an unknown id shows an actionable message", async ({ page }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/322");
  await page.goto("/obs/roulette/00000000-0000-0000-0000-000000000000");
  await expect(page.locator("main")).not.toHaveText(/^\s*API 回傳 HTTP \d+。?\s*$/, { timeout: 5_000 });
  await expect(page.getByText(/找不到|不存在|重新複製/)).toBeVisible({ timeout: 2_000 });
});

test("donate OBS with an unknown activity is not silently blank", async ({ page }) => {
  test.fail(true, "https://github.com/KonohaDerek/EasyLottery/issues/322");
  await page.goto("/obs/donate/unknown-activity-key");
  await expect(page.locator("main")).not.toHaveText(/^\s*$/, { timeout: 5_000 });
});
