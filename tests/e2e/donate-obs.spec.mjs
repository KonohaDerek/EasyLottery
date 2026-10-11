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

// Short VP8/WebM fixture for exercising the browser's natural playback-ended path.
const SHORT_WEBM_BASE64 = [
  "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",
  "wIvZkAAcU7trkbuPs4EAt4r3gQHxggGf8IED"
].join("");

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
      useWebmAnimation: true,
      webmAnimationUrl: "/missing-donate-animation.webm",
      webmAnimationLoop: false,
      showDonateInformation: true,
      isEnabled: true,
      prizes: [{ name: "CI 測試獎品", imageUrl: "/missing-donate-prize.png", quantity: 10, remainingQuantity: 10, probability: 100, isGrandPrize: false }]
    }
  });
  expect(create.status()).toBe(201);
  const activity = await create.json();
  const readToken = await issueObsToken(request, adminToken, activity.publicId, ["read"]);
  const controlToken = await issueObsToken(request, adminToken, activity.publicId, ["read", "control"]);
  const seeded = await request.post(`/api/obs/donate/${activity.publicId}/test`, {
    headers: { "X-EasyLottery-Session-Token": controlToken },
    data: { donorName: "既有紀錄", amount: 100, message: "不應重播", paymentMethod: "測試付款" }
  });
  expect(seeded.ok()).toBeTruthy();

  const formalContext = await browser.newContext();
  const controlContext = await browser.newContext();
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();
  const legacy = await browser.newPage();
  let readOnlyControl;
  let reducedMotion;

  try {
    await formal.route("**/missing-donate-animation.webm", route => route.abort());
    await formal.route("**/missing-donate-prize.png", route => route.abort());
    await formal.goto(obsUrl(baseURL, activity.publicId, readToken));
    await expect(formal.locator(".donate-obs-shell")).toBeVisible();
    await expect(formal.locator(".donate-reveal")).toHaveCount(0);
    await expect(formal.locator(".donate-test-launch")).toHaveCount(0);
    expect(await formal.evaluate(() => `${location.search}${location.hash}`)).not.toContain("sessionToken=");
    const background = await formal.evaluate(() => getComputedStyle(document.documentElement).backgroundColor);
    expect(background).toMatch(/rgba?\(0, 0, 0, 0\)|transparent/);

    await legacy.goto(`${baseURL}/legacy/obs/donate/${activity.publicId}#sessionToken=${encodeURIComponent(readToken)}`);
    await expect(legacy.locator(".donate-obs-shell")).toBeVisible();

    await control.goto(obsUrl(baseURL, activity.publicId, controlToken, { controls: true }));
    readOnlyControl = await browser.newPage();
    await readOnlyControl.goto(obsUrl(baseURL, activity.publicId, readToken, { controls: true }));
    await expect(readOnlyControl.locator(".donate-obs-state")).toHaveCount(0);
    await readOnlyControl.getByRole("button", { name: "測試 Donate" }).click();
    await readOnlyControl.getByRole("button", { name: "送出測試 Donate" }).click();
    await expect(readOnlyControl.getByRole("status")).toContainText("權限不足");

    reducedMotion = await browser.newPage();
    await reducedMotion.goto(obsUrl(baseURL, activity.publicId, readToken, { motion: "reduce" }));
    await expect(reducedMotion.locator(".obs-layout")).toHaveClass(/obs-motion-reduced/);

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
    await expect(formal.locator(".donate-result-content")).toBeVisible({ timeout: 15_000 });
    await expect(formal.locator(".donate-reveal-fallback")).toHaveText("CI 測試獎品");
    await expect(reducedMotion.locator(".donate-result-content")).toBeVisible({ timeout: 15_000 });
    await expect(reducedMotion.locator(".donate-animation-video")).toHaveCount(0);

    await reducedMotion.close();
    await readOnlyControl.close();
    await legacy.close();
  } finally {
    await formalContext.close();
    await controlContext.close();
    if (!legacy.isClosed()) await legacy.close();
    if (readOnlyControl && !readOnlyControl.isClosed()) await readOnlyControl.close();
    if (reducedMotion && !reducedMotion.isClosed()) await reducedMotion.close();
    const deleted = await request.delete(`/api/donate-activities/${activity.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});

test("plays a WebM animation to completion and continues the queued Donate result", async ({ browser, request, baseURL }) => {
  const videoPage = await browser.newPage();
  let activity;
  let adminToken;

  try {
    adminToken = await login(request);
    const create = await request.post("/api/donate-activities", {
      headers: { "X-EasyLottery-Session-Token": adminToken },
      data: {
        name: "CI OBS WebM lifecycle",
        minimumDonationAmount: 100,
        startsAtUtc: new Date(Date.now() - 60_000).toISOString(),
        endsAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
        animation: 0,
        resultDisplayDurationSeconds: 3,
        animationDurationSeconds: 3,
        useWebmAnimation: true,
        webmAnimationUrl: "/short-donate-animation.webm",
        webmAnimationLoop: false,
        showDonateInformation: false,
        isEnabled: true,
        prizes: [{ name: "WebM fixture prize", imageUrl: "", quantity: 10, remainingQuantity: 10, probability: 100, isGrandPrize: false }]
      }
    });
    expect(create.status()).toBe(201);
    activity = await create.json();
    const controlToken = await issueObsToken(request, adminToken, activity.publicId, ["read", "control"]);

    await videoPage.addInitScript(() => {
      window.__donateWebmEvents = [];
      document.addEventListener("canplay", event => {
        if (event.target instanceof HTMLVideoElement && event.target.classList.contains("donate-animation-video")) {
          event.target.playbackRate = 8;
        }
      }, true);
      document.addEventListener("ended", event => {
        if (event.target instanceof HTMLVideoElement && event.target.classList.contains("donate-animation-video")) {
          window.__donateWebmEvents.push({
            donor: document.querySelector(".donate-notice-name")?.textContent,
            phase: document.querySelector(".donate-reveal")?.className
          });
        }
      }, true);
    });
    await videoPage.route("**/short-donate-animation.webm", route => route.fulfill({
      status: 200,
      contentType: "video/webm",
      body: Buffer.from(SHORT_WEBM_BASE64, "base64")
    }));
    await videoPage.bringToFront();
    await videoPage.goto(obsUrl(baseURL, activity.publicId, controlToken));
    await expect(videoPage.locator(".donate-obs-shell")).toBeVisible();

    const first = await request.post(`/api/obs/donate/${activity.publicId}/test`, {
      headers: { "X-EasyLottery-Session-Token": controlToken },
      data: { donorName: "WebM first", amount: 100, message: "WebM playback E2E", paymentMethod: "測試付款" }
    });
    expect(first.ok()).toBeTruthy();
    expect((await first.json()).records).toHaveLength(1);
    await expect.poll(
      () => videoPage.evaluate(() => window.__donateWebmEvents.some(event => event.donor?.includes("WebM first"))),
      { timeout: 10_000 }
    ).toBe(true);
    await expect(videoPage.locator(".donate-reveal-congratulation")).toContainText("WebM first");

    const second = await request.post(`/api/obs/donate/${activity.publicId}/test`, {
      headers: { "X-EasyLottery-Session-Token": controlToken },
      data: { donorName: "WebM second", amount: 100, message: "WebM playback E2E", paymentMethod: "測試付款" }
    });
    expect(second.ok()).toBeTruthy();
    expect((await second.json()).records).toHaveLength(1);
    await expect.poll(
      () => videoPage.evaluate(() => window.__donateWebmEvents.some(event => event.donor?.includes("WebM second"))),
      { timeout: 20_000 }
    ).toBe(true);
    await expect(videoPage.locator(".donate-reveal-congratulation")).toContainText("WebM second");
  } finally {
    await videoPage.close();
    if (activity && adminToken) {
      const deleted = await request.delete(`/api/donate-activities/${activity.id}`, {
        headers: { "X-EasyLottery-Session-Token": adminToken }
      });
      expect(deleted.status()).toBe(204);
    }
  }
});
