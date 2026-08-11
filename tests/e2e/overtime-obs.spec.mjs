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

function obsUrl(baseUrl, token, controls = false) {
  return `${baseUrl}/obs/overtime${controls ? "?controls=1" : ""}#sessionToken=${encodeURIComponent(token)}`;
}

test("加班台 OBS shares controls, feed notifications, and transparent rendering", async ({ browser, request, baseURL }) => {
  const adminToken = await login(request);
  const original = await readSettings(request, adminToken);
  await request.delete("/api/overtime-feed", {
    headers: { "X-EasyLottery-Session-Token": adminToken }
  });
  await saveSettings(request, adminToken, {
    ...original.value,
    isEnabled: true,
    plannedEndAtUtc: new Date(Date.now() + 3_600_000).toISOString(),
    streamStartedAtUtc: null,
    pausedAtUtc: null,
    completedAtUtc: null,
    sessionState: "idle"
  }, original.etag);

  const readToken = await issueObsToken(request, adminToken, ["read"]);
  const controlToken = await issueObsToken(request, adminToken, ["read", "control"]);
  const formalContext = await browser.newContext();
  const controlContext = await browser.newContext();
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();

  try {
    await formal.goto(obsUrl(baseURL, readToken));
    await control.goto(obsUrl(baseURL, controlToken, true));
    await expect(control.getByRole("button", { name: "開始加班" })).toBeVisible();
    await expect(control.getByRole("button", { name: "測試贊助通知" })).toBeVisible();

    await control.getByRole("button", { name: "開始加班" }).click();
    await expect(formal.locator(".overtime-overlay")).toBeVisible({ timeout: 10_000 });
    await expect(formal.locator(".overtime-progress-panel")).toBeVisible();
    const background = await formal.evaluate(() => getComputedStyle(document.documentElement).backgroundColor);
    expect(background).toMatch(/rgba?\(0, 0, 0, 0\)|transparent/);

    await control.getByRole("button", { name: "測試贊助通知" }).click();
    await expect(formal.locator(".overtime-live-name")).toContainText("測試贊助者", { timeout: 10_000 });

    await control.getByRole("button", { name: "結束加班" }).click();
    await expect(formal.locator(".overtime-overlay")).toHaveCount(0, { timeout: 10_000 });
  } finally {
    await formalContext.close();
    await controlContext.close();
    const latest = await readSettings(request, adminToken);
    await saveSettings(request, adminToken, original.value, latest.etag);
    const cleared = await request.delete("/api/overtime-feed", {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(cleared.status()).toBe(204);
  }
});
