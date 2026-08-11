import { expect, test } from "./fixtures.mjs";

test("admin can register and use a Passkey", async ({ browser, baseURL }) => {
  const origin = new URL(baseURL ?? "http://127.0.0.1:18930").origin.replace("127.0.0.1", "localhost");
  const context = await browser.newContext();
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  await cdp.send("WebAuthn.enable");
  await cdp.send("WebAuthn.addVirtualAuthenticator", {
    options: {
      protocol: "ctap2",
      transport: "internal",
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true
    }
  });

  try {
    await page.goto(`${origin}/`, { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "註冊第一個 Passkey" }).click();
    await expect(page.getByText("名單與獎項匯入 / 匯出")).toBeVisible();

    await page.evaluate(() => sessionStorage.clear());
    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(page.getByText("名單與獎項匯入 / 匯出")).toBeVisible();
  } finally {
    await context.close();
  }
});
