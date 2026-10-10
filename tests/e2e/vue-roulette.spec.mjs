import { expect, test } from "./fixtures.mjs";

const makeTemplate = (overrides = {}) => ({
  id: 7,
  publicId: "f6fa9d7d-96d6-4e86-895f-79f3291b1994",
  marketSourcePublicId: "00000000-0000-0000-0000-000000000000",
  name: "週末轉盤",
  description: "測試用轉盤",
  segmentCount: 8,
  spinDurationSec: 5,
  resultDisplayDurationSeconds: 12,
  easingFunction: "ease-out-cubic",
  initialAngleDeg: 0,
  centerImageUrl: "",
  backgroundImageUrl: "",
  pointerImageUrl: "",
  spinSoundUrl: "",
  winSoundUrl: "",
  isBuiltIn: false,
  publicationStatus: 0,
  createdAt: "2026-10-10T00:00:00Z",
  updatedAt: "2026-10-10T00:00:00Z",
  segments: Array.from({ length: 8 }, (_, index) => ({
    id: index + 1,
    templateId: 7,
    index,
    title: `選項 ${index + 1}`,
    imageUrl: "",
    color: ["#e74c3c", "#e67e22", "#f1c40f", "#2ecc71", "#1abc9c", "#3498db", "#9b59b6", "#e91e63"][index],
    probability: 0
  })),
  ...overrides
});

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
  return session.token;
}

test("unauthenticated Roulette route redirects without requesting templates", async ({ page, baseURL }) => {
  let requests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/roulette-templates") requests++;
  });
  await page.goto(`${baseURL}/roulette`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/roulette");
  expect(requests).toBe(0);
});

test("admin manages Roulette templates through the existing API and can return to Blazor", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  const source = makeTemplate();
  const builtIn = makeTemplate({ id: 9, name: "內建模板", isBuiltIn: true });
  let templates = [structuredClone(source), structuredClone(builtIn)];
  const requests = [];

  await page.route("**/api/roulette-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    requests.push({ method, path, body: request.postData() ? request.postDataJSON() : null });

    if (method === "GET" && path === "/api/roulette-templates") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(templates) });
      return;
    }
    if (method === "POST" && path === "/api/roulette-templates") {
      const body = request.postDataJSON();
      const saved = { ...body, id: 8, publicId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", segments: body.segments.map((segment, index) => ({ ...segment, id: index + 20, templateId: 8 })) };
      templates.push(saved);
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(saved) });
      return;
    }
    if (method === "PUT" && /^\/api\/roulette-templates\/\d+$/.test(path)) {
      const body = request.postDataJSON();
      templates = templates.map(template => template.id === body.id ? body : template);
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
      return;
    }
    if (method === "DELETE" && /^\/api\/roulette-templates\/\d+$/.test(path)) {
      const id = Number(path.split("/").at(-1));
      templates = templates.filter(template => template.id !== id);
      await route.fulfill({ status: 204, body: "" });
      return;
    }
    await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: `Unexpected request ${method} ${path}` }) });
  });
  await page.route("**/api/obs-assets", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([
      { id: "spin-audio", fileName: "spin.mp3", contentType: "audio/mpeg", kind: 1, length: 4096 },
      { id: "wheel-image", fileName: "wheel.png", contentType: "image/png", kind: 0, length: 2048 }
    ]) });
  });

  await page.goto(`${baseURL}/roulette`, { waitUntil: "networkidle" });
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
  await expect(page.getByRole("heading", { name: "轉盤模板", exact: true })).toBeVisible();
  await expect(page.getByRole("row", { name: /週末轉盤/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /內建模板/ }).getByRole("button", { name: "刪除" })).toHaveCount(0);

  await page.getByRole("button", { name: "新增模板" }).click();
  await page.getByLabel("模板名稱").fill("新測試轉盤");
  await page.getByRole("dialog").getByRole("combobox").first().selectOption("6");
  await page.getByRole("button", { name: "套用格數" }).click();
  await expect(page.getByRole("group", { name: "格 6" })).toBeVisible();
  await expect(page.getByRole("group", { name: "格 7" })).toHaveCount(0);
  const spinSoundField = page.locator("label.field").filter({ hasText: "轉動音效 URL" });
  await spinSoundField.getByRole("button", { name: "選擇資產" }).click();
  await expect(page.getByRole("dialog", { name: "選擇 OBS 資產" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "選擇音訊" })).toBeVisible();
  await page.getByRole("button", { name: /spin\.mp3/ }).click();
  await expect(page.getByLabel("轉動音效 URL")).toHaveValue("/api/obs-assets/spin-audio/content");
  await page.getByRole("group", { name: "格 1" }).getByLabel("標題").fill("測試頭獎");
  await page.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByRole("row", { name: /新測試轉盤/ })).toBeVisible();
  expect(requests.find(item => item.method === "POST" && item.path === "/api/roulette-templates").body).toMatchObject({
    name: "新測試轉盤",
    segmentCount: 6,
    publicationStatus: 1,
    spinSoundUrl: "/api/obs-assets/spin-audio/content",
    segments: expect.arrayContaining([expect.objectContaining({ index: 0, title: "測試頭獎" })])
  });

  const createdRow = page.getByRole("row", { name: /新測試轉盤/ });
  await createdRow.getByRole("button", { name: "編輯" }).click();
  await page.getByLabel("模板名稱").fill("取消不應儲存");
  await page.getByRole("button", { name: "取消" }).click();
  await createdRow.getByRole("button", { name: "編輯" }).click();
  await expect(page.getByLabel("模板名稱")).toHaveValue("新測試轉盤");
  await page.getByLabel("模板名稱").fill("更新後轉盤");
  await page.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByRole("row", { name: /更新後轉盤/ })).toBeVisible();
  expect(requests.find(item => item.method === "PUT").path).toBe("/api/roulette-templates/8");

  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("row", { name: /更新後轉盤/ }).getByRole("button", { name: "刪除" }).click();
  await expect(page.getByRole("row", { name: /更新後轉盤/ })).toHaveCount(0);
  expect(requests.find(item => item.method === "DELETE").path).toBe("/api/roulette-templates/8");

  await page.getByRole("link", { name: "開啟舊版管理頁" }).click();
  await expect(page).toHaveURL(/\/legacy\/roulette$/);
  await expect(page.getByText("轉盤模板", { exact: true }).first()).toBeVisible();
  await page.getByRole("button", { name: "活動管理" }).click();
  const vueNavigation = page.waitForRequest(request => request.isNavigationRequest()
    && request.frame() === page.mainFrame()
    && new URL(request.url()).pathname === "/roulette");
  await page.getByRole("link", { name: "管理轉盤活動" }).click();
  await vueNavigation;
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
});

test("template load failure can be retried and test OBS token stays in fragment", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  let attempts = 0;
  await page.route("**/api/roulette-templates", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    attempts++;
    await route.fulfill(attempts === 1
      ? { status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary failure" }) }
      : { status: 200, contentType: "application/json", body: JSON.stringify([makeTemplate()]) });
  });
  let obsTokenRequests = 0;
  await page.route("**/api/obs-sessions", async route => {
    const request = route.request();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    expect(request.postDataJSON()).toEqual({ resourceKind: "roulette", resourceId: "f6fa9d7d-96d6-4e86-895f-79f3291b1994", scopes: ["read", "control"] });
    obsTokenRequests++;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: "mock-obs-token" }) });
  });

  await page.goto(`${baseURL}/roulette`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("temporary failure");
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("row", { name: /週末轉盤/ })).toBeVisible();
  expect(attempts).toBe(2);

  const popupPromise = page.waitForEvent("popup");
  await page.getByRole("button", { name: "開啟測試 OBS" }).click();
  const popup = await popupPromise;
  await expect(popup).toHaveURL(new RegExp(`/obs/roulette/${makeTemplate().publicId}\\?controls=1`));
  expect(new URL(popup.url()).search).toBe("?controls=1");
  expect(new URL(popup.url()).searchParams.has("sessionToken")).toBe(false);
  expect(obsTokenRequests).toBe(1);
  await popup.close();
});

test("seed, import, export, and duplicate retain their existing endpoints", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  const source = makeTemplate();
  let templates = [structuredClone(source)];
  const calls = [];
  await page.route("**/api/roulette-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    calls.push({ method, path, body: request.postData() });

    if (method === "GET" && path === "/api/roulette-templates") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(templates) });
    } else if (method === "POST" && path === "/api/roulette-templates/seed-defaults") {
      await route.fulfill({ status: 204, body: "" });
    } else if (method === "GET" && path === "/api/roulette-templates/7/export") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(source) });
    } else if (method === "POST" && path === "/api/roulette-templates/import") {
      const imported = { ...JSON.parse(request.postData()), id: 10, name: "匯入模板" };
      templates.push(imported);
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(imported) });
    } else if (method === "POST" && path === "/api/roulette-templates/7/duplicate") {
      const duplicate = { ...structuredClone(source), id: 11, name: "週末轉盤 - 複製" };
      templates.push(duplicate);
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(duplicate) });
    } else {
      await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: `Unexpected request ${method} ${path}` }) });
    }
  });

  await page.goto(`${baseURL}/roulette`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "載入預設模板" }).click();
  await expect(page.getByRole("status")).toContainText("已載入預設模板");
  expect(calls.some(call => call.method === "POST" && call.path === "/api/roulette-templates/seed-defaults")).toBe(true);

  await page.getByText("進階：匯入／匯出模板").click();
  await page.getByLabel("匯入 JSON").fill(JSON.stringify(source));
  await page.getByRole("button", { name: "匯入" }).click();
  await expect(page.getByRole("row", { name: /匯入模板/ })).toBeVisible();
  expect(calls.find(call => call.path === "/api/roulette-templates/import").body).toBe(JSON.stringify(source));

  await page.getByRole("row", { name: /週末轉盤/ }).getByRole("button", { name: "匯出" }).click();
  await expect(page.getByLabel("匯出結果")).toHaveValue(JSON.stringify(source));
  expect(calls.some(call => call.method === "GET" && call.path === "/api/roulette-templates/7/export")).toBe(true);

  await page.getByRole("row", { name: /週末轉盤/ }).getByRole("button", { name: "複製" }).click();
  await expect(page.getByRole("dialog", { name: "編輯轉盤模板：週末轉盤 - 複製" })).toBeVisible();
  await page.getByRole("button", { name: "取消" }).click();
  await expect(page.getByRole("row", { name: /週末轉盤 - 複製/ })).toBeVisible();
  expect(calls.some(call => call.method === "POST" && call.path === "/api/roulette-templates/7/duplicate")).toBe(true);
});
