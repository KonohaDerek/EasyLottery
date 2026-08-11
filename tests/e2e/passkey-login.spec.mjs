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
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(page.getByText("名單與獎項匯入 / 匯出")).toBeVisible();

    await page.evaluate(() => sessionStorage.clear());
    await page.reload({ waitUntil: "networkidle" });
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(page.getByText("名單與獎項匯入 / 匯出")).toBeVisible();

    await page.goto(`${origin}/system/access`, { waitUntil: "networkidle" });
    await expect(page.getByText("管理員 Passkey 管理", { exact: true })).toBeVisible();
    const rows = page.locator("tbody tr");
    await expect(rows).toHaveCount(1);

    const { authenticatorId: backupAuthenticatorId } = await cdp.send("WebAuthn.addVirtualAuthenticator", {
      options: {
        protocol: "ctap2",
        transport: "usb",
        hasResidentKey: true,
        hasUserVerification: true,
        isUserVerified: true
      }
    });
    await page.locator("#passkey-name").fill("Backup Authenticator");
    await page.getByRole("button", { name: "新增 Passkey" }).click();
    await expect(rows).toHaveCount(2);
    await expect(page.getByText("Backup Authenticator")).toBeVisible();

    page.once("dialog", dialog => dialog.accept());
    await rows.last().getByRole("button", { name: "移除" }).click();
    await expect(rows).toHaveCount(1);
    await cdp.send("WebAuthn.removeVirtualAuthenticator", { authenticatorId: backupAuthenticatorId });
  } finally {
    await context.close();
  }
});
