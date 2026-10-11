import { expect, test } from "./fixtures.mjs";

async function adminToken(request) {
  const response = await request.get("/api/test/session-token");
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function issueObsToken(request, token, roundId) {
  const response = await request.post("/api/obs-sessions", {
    headers: { "X-EasyLottery-Session-Token": token },
    data: { resourceKind: "interaction-round", resourceId: roundId, scopes: ["read"] }
  });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

test("主持人可建立、暫停並結算互動回合", async ({ request }) => {
  const tokenResponse = await request.get("/api/test/session-token");
  expect(tokenResponse.ok()).toBeTruthy();
  const token = (await tokenResponse.json()).token;
  const headers = { "X-EasyLottery-Session-Token": token };
  const created = await request.post("/api/interactions/rounds", { headers, data: { name: "E2E 投票", type: "Vote", commandPrefix: "!vote", voteOptions: ["a", "b"] } });
  expect(created.ok()).toBeTruthy();
  const round = await created.json();
  expect((await request.post(`/api/interactions/rounds/${round.id}/start`, { headers })).ok()).toBeTruthy();
  expect((await request.post(`/api/interactions/rounds/${round.id}/pause`, { headers })).ok()).toBeTruthy();
  const event = await request.post(`/api/interactions/rounds/${round.id}/events`, { headers, data: { audienceProfileId: "b8dd986b-b3d3-4b7d-ae8a-0253bb6a6240", platform: "YouTube", channelScope: "test", externalUserId: "viewer", externalEventId: "event", content: "!vote a" } });
  expect(event.ok()).toBeTruthy();
  expect((await event.json()).reason).toBe("round-not-live");
});

test("Vue interaction management routes preserve admin REST behavior and show empty state", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);

  const frameworkRequests = [];
  page.on("request", outgoing => {
    if (new URL(outgoing.url()).pathname.startsWith("/_framework/")) frameworkRequests.push(outgoing.url());
  });
  let rounds = [];
  const calls = [];
  await page.route("**/api/interactions/rounds**", async route => {
    const request = route.request();
    const url = new URL(request.url());
    const headers = request.headers();
    calls.push({ method: request.method(), path: url.pathname, token: headers["x-easylottery-session-token"] });

    if (request.method() === "GET" && url.pathname === "/api/interactions/rounds") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(rounds) });
      return;
    }
    if (request.method() === "POST" && url.pathname === "/api/interactions/rounds") {
      const body = request.postDataJSON();
      const created = {
        id: "e2e-interaction-round",
        name: body.name,
        status: "Draft",
        type: body.type,
        commandPrefix: body.commandPrefix,
        voteOptions: body.voteOptions,
        correctAnswerPoints: body.correctAnswerPoints,
        eligibilityTickets: body.eligibilityTickets
      };
      rounds = [created];
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(created) });
      return;
    }
    const action = url.pathname.split("/").at(-1);
    const status = { start: "Live", pause: "Paused", settle: "Settled", cancel: "Cancelled" }[action];
    rounds = rounds.map(round => round.id === "e2e-interaction-round" ? { ...round, status } : round);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(rounds[0]) });
  });

  await page.goto(`${baseURL}/interactions/rounds`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "直播互動回合", exact: true })).toBeVisible();
  await expect(page.getByText("目前沒有互動回合", { exact: false })).toBeVisible();
  await page.getByLabel("回合名稱").fill("E2E Vue 投票");
  await page.getByRole("button", { name: "建立草稿" }).click();
  await expect(page.getByText("E2E Vue 投票", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "開始" }).click();
  await expect(page.getByText("進行中", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "暫停" })).toBeVisible();
  const callSummary = calls.map(({ method, path, token: receivedToken }) => ({ method, path, hasToken: Boolean(receivedToken), tokenMatches: receivedToken === token }));
  expect(calls.some(call => call.method === "POST" && call.path === "/api/interactions/rounds" && call.token === token), JSON.stringify(callSummary)).toBe(true);
  expect(calls.some(call => call.method === "POST" && call.path.endsWith("/start") && call.token === token), JSON.stringify(callSummary)).toBe(true);

  await page.goto(`${baseURL}/interactions/identity-link`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "觀眾身份綁定", exact: true })).toBeVisible();
  const invalidLinkResponse = route => route.fulfill({
    status: 400,
    contentType: "application/json",
    body: JSON.stringify({ error: "invalid, expired, or consumed" })
  });
  await page.route("**/api/interactions/identity-links/complete", invalidLinkResponse);
  await page.getByLabel("一次性綁定碼").fill("private-one-time-code");
  await page.getByRole("button", { name: "完成綁定" }).click();
  const safeError = page.locator(".identity-link-form .v-alert[role='alert']");
  await expect(safeError).toContainText("無效、已過期或已使用");
  await expect(safeError).not.toContainText("private-one-time-code");
  await page.unroute("**/api/interactions/identity-links/complete", invalidLinkResponse);
  await page.route("**/api/interactions/identity-links/complete", route => route.fulfill({ status: 204, body: "" }));
  await page.getByLabel("一次性綁定碼").fill("valid-one-time-code");
  await page.getByRole("button", { name: "完成綁定" }).click();
  await expect(page.getByRole("status")).toContainText("身份綁定完成");
  await expect(page.getByLabel("一次性綁定碼")).toHaveValue("");
  expect(new URL(page.url()).search).toBe("");
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "觀眾身份綁定", exact: true })).toBeVisible();
  expect(frameworkRequests).toEqual([]);

  await page.unroute("**/api/interactions/rounds**");
  await page.goto(`${baseURL}/legacy/interactions/rounds`, { waitUntil: "domcontentloaded" });
  await expect(page.getByRole("heading", { name: "直播互動回合", exact: true })).toBeVisible();
  expect(frameworkRequests.some(url => new URL(url).pathname.startsWith("/_framework/"))).toBe(true);
});

test("interaction admin routes are gated and OBS routes load Vue with scoped realtime states", async ({ browser, request, baseURL }) => {
  const anonymous = await browser.newPage();
  await anonymous.goto(`${baseURL}/interactions/rounds`, { waitUntil: "networkidle" });
  expect(new URL(anonymous.url()).pathname).toBe("/login");
  expect(new URL(anonymous.url()).searchParams.get("redirect")).toBe("/interactions/rounds");
  await anonymous.close();

  const admin = await adminToken(request);
  const headers = { "X-EasyLottery-Session-Token": admin };
  const created = await request.post("/api/interactions/rounds", {
    headers,
    data: { name: "E2E 即時投票", type: "Vote", commandPrefix: "!vote", voteOptions: ["A", "B"] }
  });
  expect(created.status()).toBe(201);
  const round = await created.json();
  const readToken = await issueObsToken(request, admin, round.id);
  const context = await browser.newContext();
  const page = await context.newPage();
  const interactionSockets = [];
  const frameworkRequests = [];
  page.on("request", outgoing => {
    if (new URL(outgoing.url()).pathname.startsWith("/_framework/")) frameworkRequests.push(outgoing.url());
  });
  await page.routeWebSocket(/\/hubs\/interactions/, socket => {
    interactionSockets.push(socket);
    socket.connectToServer();
  });

  try {
    await request.post(`/api/interactions/rounds/${round.id}/start`, { headers });
    await page.goto(`${baseURL}/obs/interaction-vote/${round.id}#sessionToken=${encodeURIComponent(readToken)}`, { waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: "E2E 即時投票", exact: true })).toBeVisible();
    await expect(page.getByText("投票開始後，結果會即時顯示在這裡。", { exact: true })).toBeVisible();
    expect(await page.evaluate(() => location.href)).not.toContain(readToken);
    expect(await page.evaluate(() => document.body.classList.contains("obs-overlay"))).toBe(true);
    await page.emulateMedia({ reducedMotion: "reduce" });
    expect(await page.locator(".interaction-panel").evaluate(element => getComputedStyle(element).animationName)).toBe("none");
    await page.emulateMedia({ reducedMotion: "no-preference" });

    await page.reload({ waitUntil: "networkidle" });
    await expect(page.getByText("投票開始後，結果會即時顯示在這裡。", { exact: true })).toBeVisible();

    const event = await request.post(`/api/interactions/rounds/${round.id}/events`, {
      headers,
      data: {
        audienceProfileId: "99a5b322-a84a-463a-a270-a4f7330b2aa9",
        platform: "YouTube",
        channelScope: "e2e",
        externalUserId: "viewer-e2e",
        externalEventId: `vote-${Date.now()}`,
        content: "!vote a"
      }
    });
    expect(event.ok()).toBeTruthy();
    await expect(page.getByRole("progressbar", { name: "A 得票數" })).toHaveAttribute("aria-valuenow", "1");
    expect(interactionSockets.length).toBeGreaterThan(0);

    const connectionsBeforeReconnect = interactionSockets.length;
    await interactionSockets.at(-1).close({ code: 1012, reason: "E2E reconnect" });
    await expect(page.getByText("重新連線中", { exact: true })).toBeVisible();
    await expect.poll(() => interactionSockets.length).toBeGreaterThan(connectionsBeforeReconnect);
    await expect(page.getByText("即時同步", { exact: true })).toBeVisible({ timeout: 30_000 });
    const afterReconnect = await request.post(`/api/interactions/rounds/${round.id}/events`, {
      headers,
      data: {
        audienceProfileId: "f46c4479-cc8b-4f30-bd05-169ec46257da",
        platform: "YouTube",
        channelScope: "e2e",
        externalUserId: "viewer-reconnected",
        externalEventId: `vote-after-reconnect-${Date.now()}`,
        content: "!vote b"
      }
    });
    expect(afterReconnect.ok()).toBeTruthy();
    await expect(page.getByRole("progressbar", { name: "B 得票數" })).toHaveAttribute("aria-valuenow", "1");
    expect(frameworkRequests).toEqual([]);

    await page.goto(`${baseURL}/obs/interaction-notice/${round.id}#sessionToken=${encodeURIComponent(readToken)}`, { waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: "E2E 即時投票", exact: true })).toBeVisible();
    await expect(page.getByText("現在開放互動", { exact: true })).toBeVisible();
    const paused = await request.post(`/api/interactions/rounds/${round.id}/pause`, { headers });
    expect(paused.ok()).toBeTruthy();
    await page.goto(`${baseURL}/obs/interaction-notice/${round.id}#sessionToken=${encodeURIComponent(readToken)}`, { waitUntil: "networkidle" });
    await expect(page.locator(".interaction-panel")).toHaveCount(0);
    await expect(page.locator(".interaction-connection-alert")).toHaveCount(0);

    const invalidToken = await issueObsToken(request, admin, crypto.randomUUID());
    await page.goto(`${baseURL}/obs/interaction-status/${round.id}#sessionToken=${encodeURIComponent(invalidToken)}`, { waitUntil: "networkidle" });
    await expect(page.getByRole("alert")).toContainText("讀取憑證已失效");

    const missingRoundId = crypto.randomUUID();
    const missingRoundToken = await issueObsToken(request, admin, missingRoundId);
    await page.goto(`${baseURL}/obs/interaction-leaderboard/${missingRoundId}#sessionToken=${encodeURIComponent(missingRoundToken)}`, { waitUntil: "networkidle" });
    await expect(page.getByRole("alert")).toContainText("找不到這個互動回合");
  } finally {
    await context.close();
    const cancelled = await request.post(`/api/interactions/rounds/${round.id}/cancel`, { headers });
    expect(cancelled.ok()).toBeTruthy();
  }
});
