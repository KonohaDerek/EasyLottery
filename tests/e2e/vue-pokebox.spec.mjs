import { expect, test } from "./fixtures.mjs";

const makeTemplate = (overrides = {}) => ({
  id: 12,
  publicId: "12fa9d7d-96d6-4e86-895f-79f3291b1994",
  marketSourcePublicId: "00000000-0000-0000-0000-000000000000",
  name: "週末戳戳樂",
  description: "測試用模板",
  gridRows: 3,
  gridColumns: 3,
  mode: 0,
  allowRePoking: false,
  maxPokeCount: 0,
  backgroundImageUrl: "",
  fontFamily: "",
  congratulationMessage: "恭喜中獎",
  overlayWidth: 1920,
  overlayHeight: 1080,
  animation: 0,
  animationDurationMs: 650,
  resultDisplayDurationSeconds: 8,
  pokeSoundUrl: "",
  openSoundUrl: "",
  isBuiltIn: false,
  publicationStatus: 0,
  createdAt: "2026-10-10T00:00:00Z",
  updatedAt: "2026-10-10T00:00:00Z",
  cells: Array.from({ length: 9 }, (_, index) => ({
    id: index + 1,
    templateId: 12,
    index,
    title: `格子 ${index + 1}`,
    subTitle: "",
    imageUrl: "",
    revealedImageUrl: "",
    revealedColor: "#cccccc",
    isRevealed: false,
    revealedAt: null
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

test("unauthenticated PokeBox route redirects without requesting templates", async ({ page, baseURL }) => {
  let requests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/poke-templates") requests++;
  });

  await page.goto(`${baseURL}/pokebox?edit=12`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/pokebox?edit=12");
  expect(requests).toBe(0);
});

test("admin edits PokeBox templates through the existing API and can return to Blazor", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  const source = makeTemplate();
  const builtIn = makeTemplate({ id: 4, name: "內建模板", isBuiltIn: true, cells: makeTemplate().cells.map(cell => ({ ...cell, templateId: 4 })) });
  let templates = [structuredClone(source), structuredClone(builtIn)];
  const requests = [];

  await page.route("**/api/poke-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    requests.push({ method, path, body: request.postData() ? request.postDataJSON() : null });

    if (method === "GET" && path === "/api/poke-templates") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(templates) });
      return;
    }
    if (method === "POST" && path === "/api/poke-templates") {
      const body = request.postDataJSON();
      const saved = {
        ...body,
        id: 18,
        publicId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        cells: body.cells.map((cell, index) => ({ ...cell, id: index + 20, templateId: 18 }))
      };
      templates.push(saved);
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(saved) });
      return;
    }
    if (method === "PUT" && /^\/api\/poke-templates\/\d+$/.test(path)) {
      const body = request.postDataJSON();
      templates = templates.map(template => template.id === body.id ? body : template);
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
      return;
    }
    if (method === "DELETE" && /^\/api\/poke-templates\/\d+$/.test(path)) {
      const id = Number(path.split("/").at(-1));
      templates = templates.filter(template => template.id !== id);
      await route.fulfill({ status: 204, body: "" });
      return;
    }
    await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: `Unexpected request ${method} ${path}` }) });
  });
  await page.route("**/api/obs-assets**", async route => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith("/content")) {
      await route.fulfill({ status: 200, contentType: path.endsWith(".png") ? "image/png" : "audio/mpeg", body: "" });
      return;
    }
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([
      { id: "poke-audio", fileName: "poke.mp3", contentType: "audio/mpeg", kind: 1, length: 4096 },
      { id: "box-image", fileName: "box.png", contentType: "image/png", kind: 0, length: 2048 }
    ]) });
  });

  await page.goto(`${baseURL}/pokebox`, { waitUntil: "networkidle" });
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
  await expect(page.getByRole("heading", { name: "戳戳樂模板", exact: true })).toBeVisible();
  await expect(page.getByRole("row", { name: /週末戳戳樂/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /內建模板/ }).getByRole("button", { name: "刪除" })).toHaveCount(0);

  await page.getByRole("button", { name: "新增模板" }).click();
  await page.getByLabel("模板名稱").fill("新測試戳戳樂");
  await page.getByLabel("行數").fill("2");
  await page.getByLabel("欄數").fill("2");
  await page.getByRole("button", { name: "套用格數" }).click();
  await expect(page.getByRole("group", { name: "格 4" })).toBeVisible();
  await expect(page.getByRole("group", { name: "格 5" })).toHaveCount(0);
  await page.getByLabel("戳擊模式").selectOption("1");
  await page.getByLabel("允許重複戳").check();
  const pokeSoundField = page.locator("label.field").filter({ hasText: "戳擊音效 URL" });
  await pokeSoundField.getByRole("button", { name: "選擇資產" }).click();
  await expect(page.getByRole("dialog", { name: "選擇 OBS 資產" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "選擇音訊" })).toBeVisible();
  await page.getByRole("button", { name: /poke\.mp3/ }).click();
  await expect(page.getByLabel("戳擊音效 URL")).toHaveValue("/api/obs-assets/poke-audio/content");
  await page.getByRole("group", { name: "格 1" }).getByLabel("標題").fill("測試獎品");
  await page.getByRole("group", { name: "格 1" }).getByLabel("副標").fill("測試副標");
  await page.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByRole("row", { name: /新測試戳戳樂/ })).toBeVisible();
  expect(requests.find(item => item.method === "POST" && item.path === "/api/poke-templates").body).toMatchObject({
    name: "新測試戳戳樂",
    gridRows: 2,
    gridColumns: 2,
    mode: 1,
    allowRePoking: true,
    publicationStatus: 1,
    pokeSoundUrl: "/api/obs-assets/poke-audio/content",
    cells: expect.arrayContaining([expect.objectContaining({ index: 0, title: "測試獎品", subTitle: "測試副標", isRevealed: false, revealedAt: null })])
  });

  const createdRow = page.getByRole("row", { name: /新測試戳戳樂/ });
  await createdRow.getByRole("button", { name: "編輯" }).click();
  await page.getByLabel("模板名稱").fill("取消不應儲存");
  await page.getByRole("button", { name: "取消" }).click();
  await createdRow.getByRole("button", { name: "編輯" }).click();
  await expect(page.getByLabel("模板名稱")).toHaveValue("新測試戳戳樂");
  await page.getByLabel("模板名稱").fill("更新後戳戳樂");
  await page.getByLabel("啟用").check();
  await page.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByRole("row", { name: /更新後戳戳樂/ })).toBeVisible();
  expect(requests.find(item => item.method === "PUT").path).toBe("/api/poke-templates/18");
  expect(requests.find(item => item.method === "PUT").body.publicationStatus).toBe(0);

  page.once("dialog", dialog => dialog.accept());
  await page.getByRole("row", { name: /更新後戳戳樂/ }).getByRole("button", { name: "刪除" }).click();
  await expect(page.getByRole("row", { name: /更新後戳戳樂/ })).toHaveCount(0);
  expect(requests.find(item => item.method === "DELETE").path).toBe("/api/poke-templates/18");

  await page.getByRole("link", { name: "開啟舊版管理頁" }).click();
  await expect(page).toHaveURL(/\/legacy\/pokebox$/);
  await expect(page.getByText("戳戳樂模板", { exact: true }).first()).toBeVisible();
  await page.getByRole("button", { name: "活動管理" }).click();
  const vueNavigation = page.waitForRequest(request => request.isNavigationRequest()
    && request.frame() === page.mainFrame()
    && new URL(request.url()).pathname === "/pokebox");
  await page.getByRole("link", { name: "管理戳戳樂活動" }).click();
  await vueNavigation;
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
});

test("PokeBox edit links survive retry, and test OBS token stays in the fragment", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  const source = makeTemplate();
  let listAttempts = 0;
  await page.route("**/api/poke-templates", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    listAttempts++;
    await route.fulfill(listAttempts === 1
      ? { status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary failure" }) }
      : { status: 200, contentType: "application/json", body: JSON.stringify([source]) });
  });
  let obsTokenRequests = 0;
  await page.route("**/api/obs-sessions", async route => {
    const request = route.request();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    expect(request.postDataJSON()).toEqual({ resourceKind: "pokebox", resourceId: source.publicId, scopes: ["read", "control"] });
    obsTokenRequests++;
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: "mock-obs-token" }) });
  });

  await page.goto(`${baseURL}/pokebox?edit=${source.id}`, { waitUntil: "networkidle" });
  await expect(page).toHaveURL(new RegExp(`/pokebox\\?edit=${source.id}$`));
  await expect(page.getByRole("alert")).toContainText("temporary failure");
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page).toHaveURL(new RegExp(`/pokebox\\?edit=${source.id}$`));
  await expect(page.getByRole("dialog", { name: `編輯戳戳樂模板：${source.name}` })).toBeVisible();
  await expect(page.getByLabel("模板名稱")).toHaveValue(source.name);
  expect(listAttempts).toBe(2);
  await page.getByRole("button", { name: "取消" }).click();

  await page.context().addInitScript(() => {
    if (location.pathname.startsWith("/obs/pokebox/")) {
      Object.defineProperty(window, "__e2eInitialSessionHash", { value: location.hash });
    }
  });
  const popupPromise = page.waitForEvent("popup");
  await page.getByRole("button", { name: "開啟測試 OBS" }).click();
  const popup = await popupPromise;
  await expect(popup).toHaveURL(new RegExp(`/obs/pokebox/${source.publicId}\\?controls=1`));
  expect(new URL(popup.url()).search).toBe("?controls=1");
  expect(new URL(popup.url()).searchParams.has("sessionToken")).toBe(false);
  await expect.poll(() => popup.evaluate(() => window.sessionStorage.getItem("easy-lottery.session-token"))).toBe("mock-obs-token");
  expect(await popup.evaluate(() => window.__e2eInitialSessionHash)).toBe("#sessionToken=mock-obs-token");
  expect(new URL(popup.url()).hash).toBe("");
  expect(obsTokenRequests).toBe(1);
  await popup.close();

  await page.goto(`${baseURL}/pokebox?edit=0`, { waitUntil: "networkidle" });
  await expect(page.getByRole("dialog", { name: "新增戳戳樂模板" })).toBeVisible();
  await page.getByRole("button", { name: "取消" }).click();

  await page.goto(`${baseURL}/pokebox/editor/${source.id}`, { waitUntil: "networkidle" });
  await expect(page).toHaveURL(`${baseURL}/pokebox?edit=${source.id}`);
  await expect(page.getByRole("dialog", { name: `編輯戳戳樂模板：${source.name}` })).toBeVisible();
});

test("seed, import, export, and duplicate retain their existing endpoints", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  const source = makeTemplate();
  let templates = [structuredClone(source)];
  const calls = [];
  await page.route("**/api/poke-templates**", async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    const method = request.method();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    calls.push({ method, path, body: request.postData() });

    if (method === "GET" && path === "/api/poke-templates") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(templates) });
    } else if (method === "POST" && path === "/api/poke-templates/seed-defaults") {
      await route.fulfill({ status: 204, body: "" });
    } else if (method === "GET" && path === "/api/poke-templates/12/export") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(source) });
    } else if (method === "POST" && path === "/api/poke-templates/import") {
      const imported = { ...JSON.parse(request.postData()), id: 20, name: "匯入模板" };
      templates.push(imported);
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(imported) });
    } else if (method === "POST" && path === "/api/poke-templates/12/duplicate") {
      const duplicate = { ...structuredClone(source), id: 21, name: "週末戳戳樂 - 複製" };
      templates.push(duplicate);
      await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(duplicate) });
    } else {
      await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: `Unexpected request ${method} ${path}` }) });
    }
  });

  await page.goto(`${baseURL}/pokebox`, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "載入預設模板" }).click();
  await expect(page.getByRole("status")).toContainText("已載入預設模板");
  expect(calls.some(call => call.method === "POST" && call.path === "/api/poke-templates/seed-defaults")).toBe(true);

  await page.getByText("進階：匯入／匯出模板").click();
  await page.getByLabel("匯入 JSON").fill(JSON.stringify(source));
  await page.getByRole("button", { name: "匯入" }).click();
  await expect(page.getByRole("row", { name: /匯入模板/ })).toBeVisible();
  expect(calls.find(call => call.path === "/api/poke-templates/import").body).toBe(JSON.stringify(source));

  await page.getByRole("row", { name: /週末戳戳樂/ }).getByRole("button", { name: "匯出" }).click();
  await expect(page.getByLabel("匯出結果")).toHaveValue(JSON.stringify(source));
  expect(calls.some(call => call.method === "GET" && call.path === "/api/poke-templates/12/export")).toBe(true);

  await page.getByRole("row", { name: /週末戳戳樂/ }).getByRole("button", { name: "複製" }).click();
  await expect(page.getByRole("dialog", { name: "編輯戳戳樂模板：週末戳戳樂 - 複製" })).toBeVisible();
  await page.getByRole("button", { name: "取消" }).click();
  await expect(page.getByRole("row", { name: /週末戳戳樂 - 複製/ })).toBeVisible();
  expect(calls.some(call => call.method === "POST" && call.path === "/api/poke-templates/12/duplicate")).toBe(true);
});
