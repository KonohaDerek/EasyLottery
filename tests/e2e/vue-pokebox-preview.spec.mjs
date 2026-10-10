import { expect, test } from "./fixtures.mjs";

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
  return session.token;
}

const template = {
  id: 12,
  publicId: "12fa9d7d-96d6-4e86-895f-79f3291b1994",
  name: "預覽用模板"
};

test("anonymous PokeBox preview returns to login without reading templates or issuing a session", async ({ page, baseURL }) => {
  const apiRequests = [];
  page.on("request", request => {
    if (new URL(request.url()).pathname.startsWith("/api/poke-templates")
      || new URL(request.url()).pathname === "/api/obs-sessions") apiRequests.push(request.url());
  });

  await page.goto(`${baseURL}/pokebox/preview/12`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/pokebox/preview/12");
  expect(apiRequests).toEqual([]);
});

test("invalid PokeBox preview id returns to management without an API request", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  const detailRequests = [];
  page.on("request", request => {
    if (new URL(request.url()).pathname.match(/^\/api\/poke-templates\/[^/]+$/)) detailRequests.push(request.url());
  });
  await page.route("**/api/poke-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));

  await page.goto(`${baseURL}/pokebox/preview/not-an-id`, { waitUntil: "networkidle" });
  await expect(page).toHaveURL(/\/pokebox$/);
  expect(detailRequests).toEqual([]);
});

test("PokeBox preview issues a scoped session and keeps its token in the OBS URL fragment", async ({ page, baseURL }) => {
  const adminToken = await installAdminToken(page, baseURL);
  const obsToken = "test+preview/token==";
  const calls = [];
  await page.route("**/api/poke-templates/12", async route => {
    calls.push({ path: new URL(route.request().url()).pathname, method: route.request().method() });
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(adminToken);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(template) });
  });
  await page.route("**/api/obs-sessions", async route => {
    const request = route.request();
    calls.push({ path: new URL(request.url()).pathname, method: request.method() });
    expect(request.headers()["x-easylottery-session-token"]).toBe(adminToken);
    expect(request.postDataJSON()).toEqual({ resourceKind: "pokebox", resourceId: template.publicId, scopes: ["read", "control"] });
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: obsToken }) });
  });
  await page.route("**/obs/pokebox/**", route => route.fulfill({ status: 200, contentType: "text/html", body: "<html>overlay</html>" }));

  await page.goto(`${baseURL}/pokebox/preview/12`, { waitUntil: "networkidle" });
  await expect.poll(() => new URL(page.url()).pathname).toBe(`/obs/pokebox/${template.publicId}`);
  const destination = new URL(page.url());
  expect(destination.searchParams.get("controls")).toBe("1");
  expect(destination.searchParams.has("sessionToken")).toBe(false);
  expect(new URLSearchParams(destination.hash.slice(1)).get("sessionToken")).toBe(obsToken);
  expect(calls).toEqual([
    { path: "/api/poke-templates/12", method: "GET" },
    { path: "/api/obs-sessions", method: "POST" }
  ]);
});

test("missing PokeBox template returns to management without issuing an OBS session", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let sessionRequests = 0;
  await page.route("**/api/poke-templates/404", route => route.fulfill({ status: 404, body: "" }));
  await page.route("**/api/poke-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route("**/api/obs-sessions", route => {
    sessionRequests++;
    return route.fulfill({ status: 500, body: "unexpected" });
  });

  await page.goto(`${baseURL}/pokebox/preview/404`, { waitUntil: "networkidle" });
  await expect(page).toHaveURL(/\/pokebox$/);
  await expect(page.getByText("尚無模板，請新增模板或載入預設模板。")).toBeVisible();
  expect(sessionRequests).toBe(0);
});

test("PokeBox preview returns to management when template has no public id", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let sessionRequests = 0;
  await page.route("**/api/poke-templates/12", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ...template, publicId: "00000000-0000-0000-0000-000000000000" }) }));
  await page.route("**/api/poke-templates", route => route.fulfill({ status: 200, contentType: "application/json", body: "[]" }));
  await page.route("**/api/obs-sessions", route => {
    sessionRequests++;
    return route.fulfill({ status: 500, body: "unexpected" });
  });

  await page.goto(`${baseURL}/pokebox/preview/12`, { waitUntil: "networkidle" });
  await expect(page).toHaveURL(/\/pokebox$/);
  expect(sessionRequests).toBe(0);
});

test("transient PokeBox preview API errors can be retried", async ({ page, baseURL }) => {
  const adminToken = await installAdminToken(page, baseURL);
  const obsToken = "retry-preview-token";
  let templateRequests = 0;
  let sessionRequests = 0;
  await page.route("**/api/poke-templates/12", async route => {
    templateRequests++;
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(adminToken);
    if (templateRequests === 1) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ error: "暫時無法載入。" }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(template) });
  });
  await page.route("**/api/obs-sessions", async route => {
    sessionRequests++;
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(adminToken);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: obsToken }) });
  });
  await page.route("**/obs/pokebox/**", route => route.fulfill({ status: 200, contentType: "text/html", body: "<html>overlay</html>" }));

  await page.goto(`${baseURL}/pokebox/preview/12`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("暫時無法載入。");
  await page.getByRole("button", { name: "重新嘗試" }).click();
  await expect.poll(() => new URL(page.url()).pathname).toBe(`/obs/pokebox/${template.publicId}`);
  expect(templateRequests).toBe(2);
  expect(sessionRequests).toBe(1);
  expect(new URLSearchParams(new URL(page.url()).hash.slice(1)).get("sessionToken")).toBe(obsToken);
});

test("OBS-session 404 stays on the preview relay and can be retried", async ({ page, baseURL }) => {
  const adminToken = await installAdminToken(page, baseURL);
  const obsToken = "session-retry-token";
  let sessionRequests = 0;
  await page.route("**/api/poke-templates/12", route => route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(template) }));
  await page.route("**/api/obs-sessions", async route => {
    sessionRequests++;
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(adminToken);
    if (sessionRequests === 1) {
      await route.fulfill({ status: 404, contentType: "application/json", body: JSON.stringify({ error: "OBS session temporarily unavailable." }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: obsToken }) });
  });
  await page.route("**/obs/pokebox/**", route => route.fulfill({ status: 200, contentType: "text/html", body: "<html>overlay</html>" }));

  await page.goto(`${baseURL}/pokebox/preview/12`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("OBS session temporarily unavailable.");
  await page.getByRole("button", { name: "重新嘗試" }).click();
  await expect.poll(() => new URL(page.url()).pathname).toBe(`/obs/pokebox/${template.publicId}`);
  expect(sessionRequests).toBe(2);
  expect(new URLSearchParams(new URL(page.url()).hash.slice(1)).get("sessionToken")).toBe(obsToken);
});

test("legacy Blazor PokeBox preview alias still hands off to the existing OBS overlay", async ({ page, baseURL }) => {
  const adminToken = await installAdminToken(page, baseURL);
  const obsToken = "legacy-preview-token";
  await page.route("**/api/poke-templates/12", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(adminToken);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(template) });
  });
  await page.route("**/api/obs-sessions", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(adminToken);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: obsToken }) });
  });
  await page.route("**/obs/pokebox/**", route => route.fulfill({ status: 200, contentType: "text/html", body: "<html>overlay</html>" }));

  await page.goto(`${baseURL}/legacy/pokebox/preview/12`, { waitUntil: "networkidle" });
  await expect.poll(() => new URL(page.url()).pathname).toBe(`/obs/pokebox/${template.publicId}`);
  const destination = new URL(page.url());
  expect(destination.searchParams.get("controls")).toBe("1");
  expect(new URLSearchParams(destination.hash.slice(1)).get("sessionToken")).toBe(obsToken);
});
