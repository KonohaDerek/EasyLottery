import { expect, test } from "./fixtures.mjs";

const sampleActivity = {
  id: 7,
  publicId: "f6fa9d7d-96d6-4e86-895f-79f3291b1994",
  name: "週末 Donate 活動",
  type: 0,
  minimumDonationAmount: 100,
  startsAtUtc: new Date(Date.now() - 60_000).toISOString(),
  endsAtUtc: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString(),
  animation: 1,
  polaroidTemplateKey: "celebration",
  useAiCongratulation: true,
  showDonateInformation: true,
  resultDisplayDurationSeconds: 30,
  animationDurationSeconds: 10,
  useWebmAnimation: false,
  webmAnimationUrl: "",
  webmPosterUrl: "",
  webmAnimationLoop: false,
  isEnabled: true,
  prizes: [{ id: 23, name: "特獎", imageUrl: "", quantity: 5, remainingQuantity: 2, probability: 100, isGrandPrize: true }]
};

async function installAdminToken(page, baseURL) {
  const response = await page.request.get(`${baseURL}/api/test/session-token`);
  expect(response.ok()).toBe(true);
  const session = await response.json();
  await page.addInitScript(token => sessionStorage.setItem("easy-lottery.session-token", token), session.token);
  return session.token;
}

test("Donate activities route redirects unauthenticated visitors without requesting activities", async ({ page, baseURL }) => {
  let requests = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/donate-activities") requests++;
  });
  await page.goto(`${baseURL}/donate-activities`, { waitUntil: "networkidle" });
  expect(new URL(page.url()).pathname).toBe("/login");
  expect(new URL(page.url()).searchParams.get("redirect")).toBe("/donate-activities");
  expect(requests).toBe(0);
});

test("admin can create, edit, and delete Donate activities using the existing API contract", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  let activities = [structuredClone(sampleActivity)];
  const writes = [];
  await page.route("**/api/donate-activities**", async route => {
    const request = route.request();
    expect(request.headers()["x-easylottery-session-token"]).toBe(token);
    const method = request.method();
    if (method === "GET") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(activities) });
      return;
    }
    if (method === "POST" || method === "PUT") {
      const body = request.postDataJSON();
      writes.push({ method, url: new URL(request.url()).pathname, body });
      if (method === "POST") {
        const saved = { ...body, id: 8, prizes: body.prizes.map((prize, index) => ({ ...prize, id: index + 1 })) };
        activities.push(saved);
        await route.fulfill({ status: 201, contentType: "application/json", body: JSON.stringify(saved) });
      } else {
        activities = activities.map(activity => activity.id === body.id ? body : activity);
        await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
      }
      return;
    }
    writes.push({ method, url: new URL(request.url()).pathname });
    activities = activities.filter(activity => activity.id !== Number(new URL(request.url()).pathname.split("/").at(-1)));
    await route.fulfill({ status: 204, body: "" });
  });
  let assetAttempts = 0;
  await page.route("**/api/obs-assets", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    assetAttempts++;
    if (assetAttempts === 1) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ error: "asset list unavailable" }) });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify([{ id: "c0d44530-5ba8-46e1-97f4-f126be40e048", fileName: "prize.png", kind: 0, contentType: "image/png", length: 4096 }])
    });
  });

  await page.goto(`${baseURL}/donate-activities`, { waitUntil: "networkidle" });
  await expect(page.getByRole("heading", { name: "Donate 活動", exact: true })).toBeVisible();
  await expect(page.getByRole("row", { name: /週末 Donate 活動/ })).toBeVisible();

  await page.getByRole("button", { name: "新增活動" }).click();
  await page.getByLabel("活動名稱").fill("新測試活動");
  await page.getByLabel("獎項名稱").fill("測試獎品");
  await page.getByRole("button", { name: "選擇資產" }).click();
  await expect(page.getByRole("dialog", { name: "選擇 OBS 資產" })).toBeVisible();
  await expect(page.getByRole("alert")).toContainText("asset list unavailable");
  await page.getByRole("button", { name: "重試" }).click();
  await page.getByRole("button", { name: /prize\.png/ }).click();
  expect(assetAttempts).toBe(2);
  await page.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByRole("row", { name: /新測試活動/ })).toBeVisible();
  expect(writes[0].method).toBe("POST");
  expect(writes[0].url).toBe("/api/donate-activities");
  expect(writes[0].body).toMatchObject({ name: "新測試活動", animation: 0, polaroidTemplateKey: "classic" });
  expect(writes[0].body.prizes[0]).toMatchObject({ name: "測試獎品", probability: 100, imageUrl: "/api/obs-assets/c0d44530-5ba8-46e1-97f4-f126be40e048/content" });

  const newRow = page.getByRole("row", { name: /新測試活動/ });
  await newRow.getByRole("button", { name: "編輯" }).click();
  await page.getByLabel("活動名稱").fill("更新後活動");
  await page.getByLabel("抽獎動畫秒數").fill("12");
  await page.getByRole("button", { name: "儲存", exact: true }).click();
  await expect(page.getByRole("row", { name: /更新後活動/ })).toBeVisible();
  expect(writes[1].method).toBe("PUT");
  expect(writes[1].url).toBe("/api/donate-activities/8");
  expect(writes[1].body).toMatchObject({ id: 8, name: "更新後活動", animationDurationSeconds: 12 });
  expect(writes[1].body.prizes[0]).toMatchObject({ id: 1, remainingQuantity: 1 });

  page.on("dialog", dialog => dialog.accept());
  await page.getByRole("row", { name: /更新後活動/ }).getByRole("button", { name: "刪除" }).click();
  await expect(page.getByRole("row")).toHaveCount(2);
  await expect(page.getByRole("row", { name: /更新後活動/ })).toHaveCount(0);
  expect(writes[2]).toMatchObject({ method: "DELETE", url: "/api/donate-activities/8" });
});

test("probability over 100 percent blocks save and API errors can be retried", async ({ page, baseURL }) => {
  await installAdminToken(page, baseURL);
  let attempts = 0;
  await page.route("**/api/donate-activities", async route => {
    attempts++;
    if (attempts === 1) {
      await route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ error: "temporary failure" }) });
      return;
    }
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([sampleActivity]) });
  });
  await page.goto(`${baseURL}/donate-activities`, { waitUntil: "networkidle" });
  await expect(page.getByRole("alert")).toContainText("temporary failure");
  await page.getByRole("button", { name: "重新載入" }).click();
  await expect(page.getByRole("row", { name: /週末 Donate 活動/ })).toBeVisible();
  expect(attempts).toBe(2);

  await page.getByRole("button", { name: "編輯" }).click();
  await page.getByLabel("中獎機率 (%)").fill("101");
  await expect(page.getByRole("button", { name: "儲存", exact: true })).toBeDisabled();
});

test("Blazor navigation performs a full-page handoff to Vue Donate activities", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  await page.route("**/api/donate-activities", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
  });
  await page.goto(baseURL, { waitUntil: "networkidle" });
  await page.getByRole("button", { name: "活動管理" }).click();
  const navigation = page.waitForRequest(request => request.isNavigationRequest()
    && request.frame() === page.mainFrame()
    && new URL(request.url()).pathname === "/donate-activities");
  await page.getByRole("link", { name: "建立與管理 Donate 抽獎活動" }).click();
  await navigation;
  await expect(page.locator("#app[data-v-app]")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Donate 活動", exact: true })).toBeVisible();
});

test("test OBS token stays in the fragment and the legacy Blazor page remains reachable", async ({ page, baseURL }) => {
  const token = await installAdminToken(page, baseURL);
  await page.route("**/api/donate-activities", async route => {
    expect(route.request().headers()["x-easylottery-session-token"]).toBe(token);
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([sampleActivity]) });
  });
  let obsHeader = "";
  await page.route("**/api/obs-sessions", async route => {
    obsHeader = route.request().headers()["x-easylottery-session-token"] ?? "";
    expect(route.request().postDataJSON()).toEqual({ resourceKind: "donate", resourceId: sampleActivity.publicId, scopes: ["read", "control"] });
    await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ token: "mock-obs-token" }) });
  });
  await page.goto(`${baseURL}/donate-activities`, { waitUntil: "networkidle" });
  const popupPromise = page.waitForEvent("popup");
  await page.getByRole("button", { name: "開啟測試 OBS" }).click();
  const popup = await popupPromise;
  await popup.waitForLoadState("domcontentloaded");
  await expect(popup).toHaveURL(new RegExp(`/obs/donate/${sampleActivity.publicId}\\?controls=1`));
  expect(obsHeader).toBe(token);
  expect(new URL(popup.url()).search).toBe("?controls=1");
  expect(new URL(popup.url()).searchParams.has("sessionToken")).toBe(false);

  await page.getByRole("link", { name: "開啟舊版管理頁" }).click();
  await expect(page).toHaveURL(/\/legacy\/donate-activities$/);
  await expect(page.getByRole("article").getByText("Donate 抽獎活動", { exact: true })).toBeVisible();
  await popup.close();
});
