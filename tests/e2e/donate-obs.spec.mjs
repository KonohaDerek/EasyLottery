import { expect, test } from "./fixtures.mjs";

async function login(request) {
  const response = await request.get("/api/test/session-token");
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function issueObsToken(request, adminToken, publicId, scopes) {
  const response = await request.post("/api/obs-sessions", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: { resourceKind: "donate", resourceId: publicId, scopes }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

function obsUrl(baseUrl, publicId, token, { controls = false, motion = "" } = {}) {
  const params = new URLSearchParams();
  if (controls) params.set("controls", "1");
  if (motion) params.set("motion", motion);
  const query = params.toString() ? `?${params}` : "";
  return `${baseUrl}/obs/donate/${publicId}${query}#sessionToken=${encodeURIComponent(token)}`;
}

test("control and formal OBS contexts share a transparent Donate lifecycle", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const startsAtUtc = new Date(Date.now() - 60_000).toISOString();
  const endsAtUtc = new Date(Date.now() + 3_600_000).toISOString();
  const create = await request.post("/api/donate-activities", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: {
      name: "CI OBS Donate",
      minimumDonationAmount: 1,
      startsAtUtc,
      endsAtUtc,
      animation: 4,
      resultDisplayDurationSeconds: 3,
      animationDurationSeconds: 3,
      showDonateInformation: true,
      isEnabled: true,
      prizes: [{ name: "CI 測試獎品", imageUrl: "", quantity: 10, remainingQuantity: 10, probability: 100, isGrandPrize: false }]
    }
  });
  expect(create.status()).toBe(201);
  const activity = await create.json();
  const readToken = await issueObsToken(request, adminToken, activity.publicId, ["read"]);
  const controlToken = await issueObsToken(request, adminToken, activity.publicId, ["read", "control"]);

  const formalContext = await browser.newContext();
  const controlContext = await browser.newContext();
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();

  try {
    await formal.goto(obsUrl(baseURL, activity.publicId, readToken));
    await expect(formal.locator(".donate-obs-shell")).toBeVisible();
    await expect(formal.locator(".donate-reveal")).toHaveCount(0);
    await expect(formal.locator(".donate-test-launch")).toBeHidden();
    const background = await formal.evaluate(() => getComputedStyle(document.documentElement).backgroundColor);
    expect(background).toMatch(/rgba?\(0, 0, 0, 0\)|transparent/);

    await control.goto(obsUrl(baseURL, activity.publicId, controlToken, { controls: true }));
    const testButton = control.getByRole("button", { name: "測試 Donate" });
    await expect(testButton).toBeVisible();
    await testButton.click();
    await control.getByLabel("測試贊助者名稱").fill("CI 測試贊助者");
    await control.getByLabel("測試贊助金額").fill("100");
    await control.getByLabel("留言").fill("CI OBS lifecycle message");
    await control.getByLabel("支付方式").fill("CI payment");
    await control.getByRole("button", { name: "送出測試 Donate" }).click();
    await expect(control.getByRole("status")).toContainText("測試通知已送出");

    await expect(formal.locator(".donate-notice-name")).toHaveText("CI 測試贊助者", { timeout: 10_000 });
    await expect(formal.locator(".donate-notice-amount")).toContainText("NT$100");
    await expect(formal.locator(".donate-notice-message")).toContainText("CI OBS lifecycle message");
    await expect(formal.locator(".donate-notice-payment")).toContainText("CI payment");
    await expect(formal.locator(".donate-reveal")).toBeVisible();

    const reducedMotion = await browser.newPage();
    await reducedMotion.goto(obsUrl(baseURL, activity.publicId, readToken, { motion: "reduce" }));
    await expect(reducedMotion.locator(".obs-layout")).toHaveClass(/obs-motion-reduced/);
    await reducedMotion.close();
  } finally {
    await formalContext.close();
    await controlContext.close();
    const deleted = await request.delete(`/api/donate-activities/${activity.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});
