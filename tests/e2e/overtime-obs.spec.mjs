import { expect, test } from "./fixtures.mjs";

async function login(request) {
  const response = await request.get("/api/test/session-token");
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function issueObsToken(request, adminToken, scopes) {
  const response = await request.post("/api/obs-sessions", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: { resourceKind: "overtime", resourceId: "default", scopes }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function readSettings(request, token) {
  const response = await request.get("/api/settings/overtime", {
    headers: { "X-EasyLottery-Session-Token": token }
  });
  expect(response.ok()).toBeTruthy();
  return { value: await response.json(), etag: response.headers()["etag"] };
}

async function saveSettings(request, token, value, etag) {
  const response = await request.put("/api/settings/overtime", {
    headers: { "X-EasyLottery-Session-Token": token, "If-Match": etag },
    data: value
  });
  expect(response.ok()).toBeTruthy();
  return { value: await response.json(), etag: response.headers()["etag"] };
}

async function readFeed(request, token) {
  const response = await request.get("/api/overtime-feed", {
    headers: { "X-EasyLottery-Session-Token": token }
  });
  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function saveFeed(request, token, events) {
  const response = await request.put("/api/overtime-feed", {
    headers: { "X-EasyLottery-Session-Token": token },
    data: events
  });
  expect(response.ok()).toBeTruthy();
}

function obsUrl(baseUrl, token, controls = false) {
  return `${baseUrl}/obs/overtime${controls ? "?controls=1" : ""}#sessionToken=${encodeURIComponent(token)}`;
}

test("加班台 OBS shares controls, feed notifications, and transparent rendering", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const original = await readSettings(request, adminToken);
  const originalFeed = await readFeed(request, adminToken);

  try {
    await saveSettings(request, adminToken, {
      ...original.value,
      isEnabled: true,
      plannedEndAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
      supportMessageVisibleSeconds: 60,
      streamStartedAtUtc: null,
      pausedAtUtc: null,
      completedAtUtc: null,
      sessionState: "idle"
    }, original.etag);
    await saveFeed(request, adminToken, []);

    const readToken = await issueObsToken(request, adminToken, ["read"]);
    const controlToken = await issueObsToken(request, adminToken, ["read", "control"]);
    const formalContext = await browser.newContext();
    const readonlyControlContext = await browser.newContext();
    const controlContext = await browser.newContext();
    const formal = await formalContext.newPage();
    const readonlyControl = await readonlyControlContext.newPage();
    const control = await controlContext.newPage();

    try {
      const legacyResponse = await request.get("/legacy/obs/overtime");
      expect(legacyResponse.ok()).toBeTruthy();
      expect(await legacyResponse.text()).toContain("_framework/blazor.webassembly.js");

      await formal.goto(obsUrl(baseURL, readToken));
      await expect(formal).toHaveURL(`${baseURL}/obs/overtime`);
      await expect(formal.getByRole("button", { name: "開始加班" })).toHaveCount(0);

      await readonlyControl.goto(obsUrl(baseURL, readToken, true));
      await expect(readonlyControl).toHaveURL(`${baseURL}/obs/overtime?controls=1`);
      await readonlyControl.getByRole("button", { name: "開始加班" }).click();
      await expect(readonlyControl.getByRole("alert")).toContainText("403");
      expect((await readSettings(request, adminToken)).value.sessionState).toBe("idle");

      const idleSettings = await readSettings(request, adminToken);
      const feedEndAtUtc = new Date(Date.now() - 1_000).toISOString();
      await saveSettings(request, adminToken, {
        ...idleSettings.value,
        streamStartedAtUtc: new Date(Date.now() - 60_000).toISOString(),
        plannedEndAtUtc: null,
        pausedAtUtc: null,
        completedAtUtc: null,
        sessionState: "running"
      }, idleSettings.etag);
      await saveFeed(request, adminToken, [{
        source: 3,
        sourceLabel: "預計關台時間",
        displayName: "預計關台時間",
        occurredAtUtc: new Date().toISOString(),
        sessionEndAtUtc: feedEndAtUtc
      }]);

      let autoCompletionFailedOnce = false;
      await control.route("**/settings", async route => {
        if (route.request().method() === "PUT" && !autoCompletionFailedOnce) {
          autoCompletionFailedOnce = true;
          await route.fulfill({ status: 503, contentType: "text/plain", body: "temporary test failure" });
          return;
        }
        await route.continue();
      });
      await control.goto(obsUrl(baseURL, controlToken, true));
      await expect(control).toHaveURL(`${baseURL}/obs/overtime?controls=1`);
      await expect(control.getByRole("button", { name: "開始加班" })).toBeVisible();
      await expect(control.getByRole("button", { name: "測試贊助通知" })).toBeVisible();
      await expect.poll(() => autoCompletionFailedOnce).toBeTruthy();
      await expect.poll(async () => (await readSettings(request, adminToken)).value.sessionState).toBe("completed");
      await control.unroute("**/settings");

      const completedSettings = await readSettings(request, adminToken);
      await saveSettings(request, adminToken, {
        ...completedSettings.value,
        streamStartedAtUtc: null,
        plannedEndAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
        pausedAtUtc: null,
        completedAtUtc: null,
        sessionState: "idle"
      }, completedSettings.etag);
      await saveFeed(request, adminToken, []);
      await control.reload();

      await control.getByRole("button", { name: "開始加班" }).click();
      await expect(formal.locator(".overtime-overlay")).toBeVisible({ timeout: 10_000 });
      await expect(formal.locator(".overtime-progress-panel")).toBeVisible();
      const background = await formal.evaluate(() => getComputedStyle(document.documentElement).backgroundColor);
      expect(background).toMatch(/rgba?\(0, 0, 0, 0\)|transparent/);

      const startedSettings = await readSettings(request, adminToken);
      const administratorEndAtUtc = new Date(Date.now() + 2 * 3_600_000).toISOString();
      await saveSettings(request, adminToken, { ...startedSettings.value, plannedEndAtUtc: administratorEndAtUtc }, startedSettings.etag);
      await expect.poll(async () => {
        const [hours, minutes, seconds] = (await formal.locator(".overtime-remaining-time").textContent()).split(":").map(Number);
        return hours * 3600 + minutes * 60 + seconds;
      }, { timeout: 12_000 }).toBeGreaterThan(7_000);

      await control.getByRole("button", { name: "暫停加班" }).click();
      await expect(control.getByRole("button", { name: "繼續加班" })).toBeVisible();
      expect(Date.parse((await readSettings(request, adminToken)).value.plannedEndAtUtc)).toBeGreaterThan(Date.now() + 7_000_000);
      await control.getByRole("button", { name: "繼續加班" }).click();
      await expect(control.getByRole("button", { name: "暫停加班" })).toBeVisible();

      await control.getByRole("button", { name: "測試贊助通知" }).click();
      const testAmount = original.value.demoEcpayAmount > 0 ? original.value.demoEcpayAmount : 100;
      const formattedTestAmount = `NT$${testAmount.toLocaleString("zh-TW", { maximumFractionDigits: 2 })}`;
      await expect.poll(async () => {
        const event = (await readFeed(request, adminToken)).find(item => (item.displayName ?? item.DisplayName) === "測試贊助者");
        return event && {
          amount: event.amount ?? event.Amount,
          amountDisplay: event.amountDisplay ?? event.AmountDisplay
        };
      }).toEqual({ amount: testAmount, amountDisplay: formattedTestAmount });
      await expect(formal.locator(".overtime-live-name")).toContainText("測試贊助者", { timeout: 10_000 });
      await expect(formal.locator(".overtime-live-amount")).toContainText(formattedTestAmount);

      await control.getByRole("button", { name: "結束加班" }).click();
      await expect(formal.locator(".overtime-overlay")).toHaveCount(0, { timeout: 10_000 });

      await readonlyControl.goto(`${baseURL}/obs/overtime?controls=1&motion=reduce#sessionToken=${encodeURIComponent(readToken)}`);
      await expect(readonlyControl).toHaveURL(`${baseURL}/obs/overtime?controls=1&motion=reduce`);
      const reducedMotionFill = readonlyControl.locator(".overtime-overlay.obs-motion-reduced .overtime-progress-fill");
      await expect(reducedMotionFill).toBeVisible();
      expect(parseFloat(await reducedMotionFill.evaluate(element => getComputedStyle(element).transitionDuration))).toBeLessThan(0.001);
    } finally {
      await formalContext.close();
      await readonlyControlContext.close();
      await controlContext.close();
    }
  } finally {
    try {
      const latest = await readSettings(request, adminToken);
      await saveSettings(request, adminToken, original.value, latest.etag);
    } finally {
      await saveFeed(request, adminToken, originalFeed);
    }
  }
});
