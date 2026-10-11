import { expect, test } from "./fixtures.mjs";

async function login(request) {
  const response = await request.get("/api/test/session-token");
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function issueObsToken(request, adminToken, kind, publicId, scopes) {
  const response = await request.post("/api/obs-sessions", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: { resourceKind: kind, resourceId: publicId, scopes }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

function obsUrl(baseUrl, kind, publicId, token, controls = false) {
  return `${baseUrl}/obs/${kind}/${publicId}${controls ? "?controls=1" : ""}#sessionToken=${encodeURIComponent(token)}`;
}

test("戳戳樂 supports direct cell interaction, shared OBS state, and reset", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const create = await request.post("/api/poke-templates", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: {
      name: "CI 戳戳樂",
      gridRows: 1,
      gridColumns: 2,
      mode: 1,
      animationDurationMs: 300,
      resultDisplayDurationSeconds: 1,
      cells: [
        { index: 0, title: "CI 戳戳獎品 A", revealedColor: "#55aaee" },
        { index: 1, title: "CI 戳戳獎品 B", revealedColor: "#eeaa55" }
      ]
    }
  });
  expect(create.status()).toBe(201);
  const template = await create.json();
  const readToken = await issueObsToken(request, adminToken, "pokebox", template.publicId, ["read"]);
  const controlToken = await issueObsToken(request, adminToken, "pokebox", template.publicId, ["read", "control"]);
  const formalContext = await browser.newContext();
  const controlContext = await browser.newContext();
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();
  await formal.setViewportSize({ width: 1280, height: 720 });
  await control.setViewportSize({ width: 1920, height: 1080 });
  const frameworkRequests = [];
  formal.on("request", request => {
    if (new URL(request.url()).pathname.startsWith("/_framework/")) frameworkRequests.push(request.url());
  });
  let liveDrawSocket;
  await control.routeWebSocket(/\/hubs\/live-draw/, socket => {
    liveDrawSocket = socket;
    socket.connectToServer();
  });

  try {
    const deniedReset = await request.post(`/api/live-draw/pokebox/${template.publicId}/reset`, {
      headers: { "X-EasyLottery-Session-Token": readToken }
    });
    expect(deniedReset.status()).toBe(403);

    await formal.goto(obsUrl(baseURL, "pokebox", template.publicId, readToken));
    await expect(formal.locator(".poke-cell")).toHaveCount(2);
    await expect(formal.locator(".cell-title")).toHaveCount(0);
    expect(await formal.evaluate(() => location.href)).not.toContain(readToken);
    expect(await formal.evaluate(() => getComputedStyle(document.body).backgroundImage)).toBe("none");
    expect(frameworkRequests).toEqual([]);

    await control.goto(obsUrl(baseURL, "pokebox", template.publicId, controlToken, true));
    await expect(control.getByRole("button", { name: "重設" })).toBeVisible();
    await expect(control.locator(".obs-controls [role=status]").first()).toHaveText("即時連線正常");
    expect(await control.evaluate(() => location.href)).not.toContain(controlToken);
    expect(await control.evaluate(() => sessionStorage.getItem("easy-lottery.session-token"))).toBe(controlToken);
    expect(liveDrawSocket).toBeTruthy();
    await liveDrawSocket.close({ code: 1012, reason: "E2E reconnect" });
    await expect(control.locator(".obs-controls [role=status]").first()).toContainText("重新連線");
    await expect(control.locator(".obs-controls [role=status]").first()).toContainText("已恢復", { timeout: 30_000 });

    const recordButton = control.getByRole("button", { name: "結束本次活動並記錄" });
    await formalContext.setOffline(true);
    await control.locator(".poke-cell").first().focus();
    await control.locator(".poke-cell").first().press("Enter");
    await expect(recordButton).toBeEnabled({ timeout: 10_000 });
    await formalContext.setOffline(false);
    await expect(formal.locator(".cell-title").first()).toContainText("CI 戳戳獎品", { timeout: 10_000 });

    await expect(recordButton).toBeEnabled({ timeout: 10_000 });
    await recordButton.click();
    await expect(control.locator(".obs-controls p[role=status]").nth(1)).toContainText("已記錄活動");
    const records = await request.get("/api/activity-results", { headers: { "X-EasyLottery-Session-Token": adminToken } });
    expect(records.ok()).toBeTruthy();
    expect((await records.json()).some(record => record.templatePublicId === template.publicId)).toBe(true);

    await control.getByRole("button", { name: "重設" }).click();
    await expect(formal.locator(".cell-title")).toHaveCount(0, { timeout: 10_000 });

    await formal.goto(`${baseURL}/legacy/obs/pokebox/${template.publicId}#sessionToken=${encodeURIComponent(readToken)}`);
    await expect(formal.locator(".poke-cell")).toHaveCount(2, { timeout: 20_000 });
    expect(await formal.evaluate(() => location.href)).not.toContain(readToken);
    const bounds = await formal.locator(".obs-stage").boundingBox();
    expect(bounds.y + bounds.height).toBeLessThanOrEqual(720);
  } finally {
    await formalContext.close();
    await controlContext.close();
    const deleted = await request.delete(`/api/poke-templates/${template.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});

test("戳戳樂隨機模式由伺服器選格並同步到獨立 OBS source", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const create = await request.post("/api/poke-templates", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: {
      name: "CI 隨機戳戳樂",
      gridRows: 1,
      gridColumns: 2,
      mode: 0,
      animationDurationMs: 300,
      resultDisplayDurationSeconds: 1,
      cells: [
        { index: 0, title: "CI 隨機獎品 A" },
        { index: 1, title: "CI 隨機獎品 B" }
      ]
    }
  });
  expect(create.status()).toBe(201);
  const template = await create.json();
  const readToken = await issueObsToken(request, adminToken, "pokebox", template.publicId, ["read"]);
  const controlToken = await issueObsToken(request, adminToken, "pokebox", template.publicId, ["read", "control"]);
  const formalContext = await browser.newContext();
  const controlContext = await browser.newContext();
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();

  try {
    await formal.goto(obsUrl(baseURL, "pokebox", template.publicId, readToken));
    await control.goto(obsUrl(baseURL, "pokebox", template.publicId, controlToken, true));
    let commandBody;
    await control.route(`**/api/live-draw/pokebox/${template.publicId}/poke`, async route => {
      commandBody = route.request().postDataJSON();
      await route.continue();
    });
    await control.getByRole("button", { name: "隨機戳一格" }).click();
    await expect(formal.locator(".cell-title").first()).toContainText(/CI 隨機獎品 [AB]/, { timeout: 10_000 });
    expect(commandBody).toEqual({ cellIndex: null, countdownSeconds: 2 });
  } finally {
    await formalContext.close();
    await controlContext.close();
    const deleted = await request.delete(`/api/poke-templates/${template.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});

test("轉盤 supports keyboard activation and shared result rendering", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const create = await request.post("/api/roulette-templates", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: {
      name: "CI 轉盤",
      segmentCount: 2,
      spinDurationSec: 0.3,
      resultDisplayDurationSeconds: 30,
      segments: [
        { index: 0, title: "CI 轉盤獎品", color: "#55aaee", probability: 100 },
        { index: 1, title: "CI 備用獎品", color: "#eeaa55", probability: 0 }
      ]
    }
  });
  expect(create.status()).toBe(201);
  const template = await create.json();
  const readToken = await issueObsToken(request, adminToken, "roulette", template.publicId, ["read"]);
  const controlToken = await issueObsToken(request, adminToken, "roulette", template.publicId, ["read", "control"]);
  const formalContext = await browser.newContext();
  const controlContext = await browser.newContext();
  const audioBlockedContext = await browser.newContext();
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();
  const audioBlocked = await audioBlockedContext.newPage();
  await formal.setViewportSize({ width: 1280, height: 720 });
  await control.setViewportSize({ width: 1920, height: 1080 });
  let liveDrawSocket;
  await control.routeWebSocket(/\/hubs\/live-draw/, socket => {
    liveDrawSocket = socket;
    socket.connectToServer();
  });

  try {
    await formal.goto(obsUrl(baseURL, "roulette", template.publicId, readToken));
    await expect(formal.locator(".roulette-container")).toHaveCount(1);
    await expect(formal.locator(".obs-roulette-wrapper")).toHaveClass(/obs-animation-idle/);
    await expect(formal.locator(".obs-win-result")).toHaveCount(0);
    expect(await formal.evaluate(() => getComputedStyle(document.body).backgroundImage)).toBe("none");

    await control.goto(obsUrl(baseURL, "roulette", template.publicId, controlToken, true));
    await expect(control.locator(".obs-controls [role=status]")).toHaveText("即時連線正常");
    expect(await control.evaluate(() => location.href)).not.toContain(controlToken);
    expect(await control.evaluate(() => sessionStorage.getItem("easy-lottery.session-token"))).toBe(controlToken);

    expect(liveDrawSocket).toBeTruthy();
    await liveDrawSocket.close({ code: 1012, reason: "E2E reconnect" });
    await expect(control.locator(".obs-controls [role=status]")).toContainText("重新連線");
    await expect(control.locator(".obs-controls [role=status]")).toContainText("已恢復", { timeout: 30_000 });

    const wheel = control.locator(".roulette-container");
    await wheel.focus();
    await wheel.press("Enter");
    await expect(formal.locator(".obs-win-result")).toBeVisible({ timeout: 10_000 });
    await expect(formal.locator(".win-title")).toContainText("CI 轉盤獎品");
    await audioBlocked.addInitScript(() => {
      const NativeAudioContext = window.AudioContext;
      if (NativeAudioContext) {
        window.AudioContext = class extends NativeAudioContext {
          resume() { return new Promise(() => {}); }
        };
      }
    });
    await audioBlocked.goto(obsUrl(baseURL, "roulette", template.publicId, readToken));
    await expect(audioBlocked.locator(".roulette-container")).toBeVisible({ timeout: 10_000 });
    await expect(audioBlocked.locator(".win-title")).toContainText("CI 轉盤獎品");
    const wheelBounds = await formal.locator(".roulette-wheel-wrapper").boundingBox();
    expect(wheelBounds.y + wheelBounds.height).toBeLessThanOrEqual(720);

    await control.getByRole("button", { name: "結束本次活動並記錄" }).click();
    await expect(control.locator(".obs-controls [role=alert]")).toContainText("已記錄活動");
    const records = await request.get("/api/activity-results", { headers: { "X-EasyLottery-Session-Token": adminToken } });
    expect(records.ok()).toBeTruthy();
    expect((await records.json()).some(record => record.templatePublicId === template.publicId)).toBe(true);

    await formal.goto(`${baseURL}/legacy/obs/roulette/${template.publicId}#sessionToken=${encodeURIComponent(readToken)}`);
    await expect(formal.locator(".roulette-container")).toHaveCount(1, { timeout: 20_000 });
    expect(await formal.evaluate(() => location.href)).not.toContain(readToken);
  } finally {
    await audioBlockedContext.close();
    await formalContext.close();
    await controlContext.close();
    const deleted = await request.delete(`/api/roulette-templates/${template.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});

test("轉盤 OBS displays a recoverable message for an expired session", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const create = await request.post("/api/roulette-templates", {
    headers: { "X-EasyLottery-Session-Token": adminToken },
    data: { name: "CI 過期 OBS 轉盤", segmentCount: 2, segments: [{ index: 0, title: "獎品 A" }, { index: 1, title: "獎品 B" }] }
  });
  expect(create.status()).toBe(201);
  const template = await create.json();
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    await page.goto(obsUrl(baseURL, "roulette", template.publicId, "expired-obs-session", true));
    await expect(page.getByRole("alert")).toContainText("OBS 工作階段無效或已過期");
    expect(await page.evaluate(() => location.href)).not.toContain("expired-obs-session");
  } finally {
    await context.close();
    const deleted = await request.delete(`/api/roulette-templates/${template.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});
