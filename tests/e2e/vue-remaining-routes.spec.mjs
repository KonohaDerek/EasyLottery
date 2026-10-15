import { expect, test } from "./fixtures.mjs";

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
  return session.token;
}

function pokeTemplate(overrides = {}) {
  return {
    id: 1, publicId: "market-source", marketSourcePublicId: "00000000-0000-0000-0000-000000000000",
    name: "週末戳戳樂", description: "可編輯的市集範本", gridRows: 3, gridColumns: 3, mode: 0,
    allowRePoking: false, maxPokeCount: 0, backgroundImageUrl: "", fontFamily: "",
    congratulationMessage: "恭喜中獎", overlayWidth: 1920, overlayHeight: 1080, animation: 0,
    animationDurationMs: 650, resultDisplayDurationSeconds: 8, pokeSoundUrl: "", openSoundUrl: "",
    isBuiltIn: true, publicationStatus: 0, createdAt: "2026-10-10T00:00:00Z", updatedAt: "2026-10-10T00:00:00Z",
    cells: Array.from({ length: 9 }, (_, index) => ({ id: index + 1, templateId: 1, index, title: `格子 ${index + 1}`, subTitle: "", imageUrl: "", revealedImageUrl: "", revealedColor: "#cccccc", isRevealed: false, revealedAt: null })),
    ...overrides
  };
}

const emptyRoulette = {
  id: 2, publicId: "roulette-source", marketSourcePublicId: "00000000-0000-0000-0000-000000000000",
  name: "幸運轉盤", description: "", segmentCount: 8, spinDurationSec: 5, resultDisplayDurationSeconds: 12,
  easingFunction: "ease-out-cubic", initialAngleDeg: 0, centerImageUrl: "", backgroundImageUrl: "", pointerImageUrl: "",
  spinSoundUrl: "", winSoundUrl: "", isBuiltIn: true, publicationStatus: 1,
  createdAt: "2026-10-10T00:00:00Z", updatedAt: "2026-10-10T00:00:00Z", segments: []
};

const connectionList = [
  { platform: "YouTube", state: "configured", configured: true, channelScope: "channel-1", apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__", accessToken: null },
  { platform: "Twitch", state: "degraded", configured: true, channelScope: "channel-2", apiKey: null, accessToken: "__EASYLOTTERY_SECRET_UNCHANGED__" },
  { platform: "Discord", state: "unconfigured", configured: false, channelScope: "", apiKey: null, accessToken: null }
];

const overtimeSettings = {
  isEnabled: true, title: "加班台", subtitle: "SuperChat / 綠界贊助", themeKey: "festival-stage",
  maxVisibleItems: 8, showAmounts: true, showAvatars: true, demoSuperChatAmount: 1000, demoEcpayAmount: 2000,
  messageTemplateKey: "default", textAnimationKey: "slide-in", streamStartedAtUtc: "2026-10-12T08:00:00Z",
  plannedEndAtUtc: "2026-10-12T12:00:00Z", sessionState: "running", pausedAtUtc: null, completedAtUtc: null,
  enableCompletionFireworks: true, completionFireworksStyle: "classic", completionFireworksDurationSeconds: 6,
  completionFireworksGifUrl: "", supportMessageVisibleSeconds: 8,
  rewardRules: [{ isEnabled: true, amountThreshold: 100, addHours: 0, addMinutes: 10, label: "入門加班" }]
};

test("anonymous direct navigation to all three migrated routes redirects without API reads", async ({ page, baseURL }) => {
  const requests = [];
  page.on("request", request => {
    const path = new URL(request.url()).pathname;
    if (path === "/api/poke-templates" || path === "/api/roulette-templates"
      || path === "/api/interactions/connections" || path === "/api/settings/overtime") requests.push(path);
  });

  for (const path of ["/market", "/system/platform-connections", "/activity/overtime"]) {
    await page.goto(`${baseURL}${path}`, { waitUntil: "networkidle" });
    expect(new URL(page.url()).pathname).toBe("/login");
    expect(new URL(page.url()).searchParams.get("redirect")).toBe(path);
  }
  expect(requests).toEqual([]);
});

test("marketplace survives direct refresh and installs by duplicating then publishing only the new copy", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  const source = pokeTemplate();
  const installed = pokeTemplate({ id: 5, publicId: "previous-copy", marketSourcePublicId: source.publicId, isBuiltIn: false, updatedAt: "2026-10-11T00:00:00Z" });
  const hiddenDraft = pokeTemplate({ id: 4, publicId: "hidden", name: "不應出現", publicationStatus: 1 });
  const calls = [];
  const pokeTemplates = [source, installed, hiddenDraft];
  await page.route("**/api/poke-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    calls.push({ method: request.method(), path, body: request.postData() ? request.postDataJSON() : null });
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    if (request.method() === "GET" && path === "/api/poke-templates") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(pokeTemplates) });
    } else if (request.method() === "POST" && path.endsWith("/duplicate")) {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: 27 }) });
    } else if (request.method() === "PUT" && path === "/api/poke-templates/27/publication-status") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: 27 }) });
    } else {
      await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: `Unexpected ${request.method()} ${path}` }) });
    }
  });
  await page.route("**/api/roulette-templates", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([emptyRoulette]) });
  });
  await page.route("**/api/obs-assets**", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`${baseURL}/market`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "活動市集" })).toBeVisible();
  await expect(page.getByText("不應出現")).toHaveCount(0);
  await expect(page.getByText("週末戳戳樂")).toBeVisible();
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByText("週末戳戳樂")).toBeVisible();
  await page.getByRole("button", { name: "週末戳戳樂：建立最新版本副本" }).click();
  await expect(page).toHaveURL(/\/pokebox\?edit=27$/);
  expect(calls.filter(call => call.method === "PUT" && call.path === "/api/poke-templates/1")).toEqual([]);
  expect(calls.filter(call => call.path.includes("/duplicate") || call.path.includes("/publication-status"))).toEqual([
    { method: "POST", path: "/api/poke-templates/1/duplicate", body: null },
    { method: "PUT", path: "/api/poke-templates/27/publication-status", body: { status: 0 } }
  ]);
});

test("marketplace presents a safe empty state when no published built-ins exist", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  await page.route("**/api/poke-templates", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });
  await page.route("**/api/roulette-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.goto(`${baseURL}/market`, { waitUntil: "networkidle" });
  await expect(page.getByRole("status")).toContainText("目前沒有可安裝的已發布內建範本");
});

test("marketplace retries publishing the retained copy without duplicating it", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let duplicates = 0;
  let publications = 0;
  await page.route("**/api/poke-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET" && path === "/api/poke-templates") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([pokeTemplate()]) });
    } else if (request.method() === "POST" && path.endsWith("/duplicate")) {
      duplicates++;
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ id: 37 }) });
    } else if (request.method() === "PUT" && path === "/api/poke-templates/37/publication-status") {
      publications++;
      await route.fulfill(publications === 1
        ? { status: 503, contentType: "application/json", body: JSON.stringify({ error: "暫時無法發布" }) }
        : { status: 204, body: "" });
    } else {
      await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: `Unexpected ${request.method()} ${path}` }) });
    }
  });
  await page.route("**/api/roulette-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`${baseURL}/market`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "週末戳戳樂：建立可編輯副本" }).click();
  await expect(page.locator(".v-alert[role=alert]")).toContainText("副本 #37 已建立，但發布未完成");
  await expect(page.getByRole("button", { name: "週末戳戳樂：建立可編輯副本" })).toBeDisabled();
  await page.getByRole("button", { name: "重試發布副本" }).click();
  await expect(page).toHaveURL(/\/pokebox\?edit=37$/);
  expect(duplicates).toBe(1);
  expect(publications).toBe(2);
});

test("marketplace shows a recoverable error and reloads successfully", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  let pokeReads = 0;
  await page.route("**/api/poke-templates", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    pokeReads++;
    if (pokeReads === 1) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary" }) });
    } else {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([pokeTemplate()]) });
    }
  });
  await page.route("**/api/roulette-templates", async route => {
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([emptyRoulette]) });
  });

  await page.goto(`${baseURL}/market`, { waitUntil: "networkidle" });
  await expect(page.locator(".v-alert[role=alert]")).toBeVisible();
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByText("週末戳戳樂")).toBeVisible();
  expect(pokeReads).toBe(2);
});

test("platform credentials remain masked; unchanged and explicit-clear saves use the current ETag", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  const writes = [];
  let revision = 0;
  await page.route("**/api/interactions/connections**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    if (request.method() === "GET") {
      await route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: `"v${revision}"` }, body: JSON.stringify(connectionList) });
      return;
    }
    writes.push({ path, etag: request.headers()["if-match"], body: request.postDataJSON() });
    revision++;
    const platform = path.split("/").at(-1);
    const saved = connectionList.find(connection => connection.platform === platform);
    await route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: `"v${revision}"` }, body: JSON.stringify(saved) });
  });

  await page.goto(`${baseURL}/system/platform-connections`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "直播平台串接" })).toBeVisible();
  const youtubeCard = page.locator(".connection-card").filter({ has: page.getByText("YouTube", { exact: true }) });
  await expect(youtubeCard.getByLabel("互動平台 YouTube API Key")).toBeVisible();
  await expect(youtubeCard.getByText(/獨立的互動平台設定/)).toBeVisible();
  await expect(youtubeCard.getByRole("link", { name: "YouTube 直播設定" })).toHaveAttribute("href", "/system/youtube-login");
  await expect(page.locator("body")).not.toContainText("__EASYLOTTERY_SECRET_UNCHANGED__");
  await page.reload({ waitUntil: "networkidle" });
  await page.getByRole("button", { name: "儲存 YouTube" }).click();
  await expect(page.getByRole("status")).toContainText("YouTube 設定已儲存");
  await page.getByRole("button", { name: "Twitch 清除憑證" }).click();
  await page.getByRole("button", { name: "儲存 Twitch" }).click();

  expect(writes).toEqual([
    { path: "/api/interactions/connections/YouTube", etag: '"v0"', body: { apiKey: null, accessToken: null, channelScope: "channel-1", degrade: false } },
    { path: "/api/interactions/connections/Twitch", etag: '"v1"', body: { apiKey: null, accessToken: "", channelScope: "channel-2", degrade: false } }
  ]);
});

test("stale platform connection ETag requires reload before another save", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let gets = 0;
  await page.route("**/api/interactions/connections**", async route => {
    const request = route.request();
    if (request.method() === "GET") {
      gets++;
      await route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: `"v${gets}"` }, body: JSON.stringify(connectionList) });
    } else {
      await route.fulfill({ status: 409, contentType: "application/json", body: JSON.stringify({ error: "stale" }) });
    }
  });
  await page.goto(`${baseURL}/system/platform-connections`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "儲存 YouTube" }).click();
  await expect(page.locator(".v-alert[role=alert]")).toContainText("設定已由其他管理員更新");
  await expect(page.getByRole("button", { name: "儲存 YouTube" })).toBeDisabled();
  await page.getByRole("button", { name: "重新載入最新設定" }).click();
  await expect(page.getByRole("button", { name: "儲存 YouTube" })).toBeEnabled();
  expect(gets).toBe(2);
});

test("overtime settings preserve the latest OBS session, and preview/demo/clear require explicit actions", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.context().grantPermissions(["clipboard-read", "clipboard-write"], { origin: baseURL });
  await page.emulateMedia({ reducedMotion: "reduce" });
  let feedWrites = 0;
  let settingsRevision = 0;
  let settingsGets = 0;
  let latestSettings = structuredClone(overtimeSettings);
  const concurrentPlannedEnd = "2026-10-12T13:00:00Z";
  const sessionToken = "overtime-scoped-secret";
  await page.route("**/api/settings/overtime", async route => {
    const request = route.request();
    if (request.method() === "GET") {
      settingsGets++;
      await route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: `"o${settingsRevision}"` }, body: JSON.stringify(latestSettings) });
      return;
    }
    expect(request.headers()["if-match"]).toBe(`"o${settingsRevision}"`);
    const body = request.postDataJSON();
    expect(body.sessionState).toBe("running");
    expect(body.streamStartedAtUtc).toBe(overtimeSettings.streamStartedAtUtc);
    if (settingsRevision === 0 && body.title === "週末加班台") expect(body.plannedEndAtUtc).toBe(concurrentPlannedEnd);
    latestSettings = body;
    settingsRevision++;
    await route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: `"o${settingsRevision}"` }, body: JSON.stringify(latestSettings) });
  });
  await page.route("**/api/obs-sessions", async route => {
    expect(route.request().postDataJSON()).toEqual({ resourceKind: "overtime", resourceId: "default", scopes: ["read", "control"] });
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: sessionToken, expiresAtUtc: "2026-10-12T13:00:00Z" }) });
  });
  await page.route("**/api/overtime-feed", async route => {
    const request = route.request();
    if (request.method() === "GET") {
      await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
    } else {
      feedWrites++;
      if (request.method() === "PUT") {
        expect(request.postDataJSON()[0]).toMatchObject(feedWrites === 1
          ? { source: 1, displayName: "示範觀眾" }
          : { source: 3, sourceLabel: "預計關台時間" });
      }
      await route.fulfill({ status: 204, body: "" });
    }
  });

  await page.goto(`${baseURL}/activity/overtime`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "加班台設定" })).toBeVisible();
  const previewMotion = await page.locator(".preview-card").evaluate(element => ({
    glow: getComputedStyle(element, "::before").animationName,
    fireworks: getComputedStyle(element.querySelector(".firework-glow span")).animationName,
    sample: getComputedStyle(element.querySelector(".preview-event")).animationName
  }));
  expect(previewMotion).toEqual({ glow: "none", fireworks: "none", sample: "none" });
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("textbox", { name: "標題", exact: true })).toHaveValue("加班台");
  expect(feedWrites).toBe(0);

  await page.getByRole("textbox", { name: "標題", exact: true }).fill("週末加班台");
  latestSettings.plannedEndAtUtc = concurrentPlannedEnd;
  await page.getByRole("button", { name: "儲存設定" }).click();
  await expect(page.getByRole("status")).toContainText("已儲存加班台設定");
  expect(latestSettings.title).toBe("週末加班台");

  await page.getByRole("button", { name: "建立預覽連結" }).click();
  const preview = page.getByRole("link", { name: "開啟預覽" });
  await expect(preview).toBeVisible();
  const previewUrl = new URL(await preview.getAttribute("href"));
  expect(previewUrl.pathname).toBe("/obs/overtime");
  expect(new URLSearchParams(previewUrl.hash.slice(1)).get("sessionToken")).toBe(sessionToken);
  await expect(page.locator("body")).not.toContainText(sessionToken);
  await page.getByRole("button", { name: "複製連結" }).click();
  await expect(page.getByRole("status")).toContainText("已複製 OBS 預覽連結");
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(previewUrl.toString());

  await page.getByRole("button", { name: "送出示範 SuperChat" }).click();
  await expect(page.getByRole("status")).toContainText("已送出示範 SuperChat");
  expect(feedWrites).toBe(2);
  page.on("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "清除加班台訊息" }).click();
  await expect(page.getByRole("status")).toContainText("已清除加班台訊息");
  expect(feedWrites).toBe(3);
  expect(settingsGets).toBeGreaterThanOrEqual(4);
});

test("overtime settings show a safe error on stale ETag and do not claim the draft was saved", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  await page.route("**/api/settings/overtime", async route => {
    if (route.request().method() === "GET") {
      await route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: '"stale"' }, body: JSON.stringify(overtimeSettings) });
    } else {
      await route.fulfill({ status: 409, contentType: "application/json", body: JSON.stringify({ error: "conflict" }) });
    }
  });
  await page.goto(`${baseURL}/activity/overtime`, { waitUntil: "networkidle" });
  await page.getByRole("textbox", { name: "標題", exact: true }).fill("未儲存變更");
  await page.getByRole("button", { name: "儲存設定" }).click();
  await expect(page.locator(".v-alert[role=alert]")).toContainText("設定已由 OBS 或其他管理員更新");
  await expect(page.getByRole("status")).toHaveCount(0);
});

test("legacy Blazor routes remain reachable after Vue takes ownership of the original paths", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  await page.route("**/api/poke-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route("**/api/roulette-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route("**/api/interactions/connections", route => route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: '"legacy"' }, body: JSON.stringify(connectionList) }));
  await page.route("**/api/settings/overtime", route => route.fulfill({ status: 200, contentType: "application/json", headers: { ETag: '"legacy"' }, body: JSON.stringify(overtimeSettings) }));
  await page.route("**/api/overtime-feed", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  for (const [path, heading] of [
    ["/legacy/market", "活動市集"],
    ["/legacy/system/platform-connections", "直播平台串接"],
    ["/legacy/activity/overtime", "加班台設定"]
  ]) {
    await page.goto(`${baseURL}${path}`, { waitUntil: "networkidle" });
    await expect(page.getByRole("heading", { name: heading }).first()).toBeVisible();
    await expect(page.locator("#app[data-v-app]")).toHaveCount(0);
  }
});
