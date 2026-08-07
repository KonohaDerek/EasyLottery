import { expect, test } from "./fixtures.mjs";

async function login(request) {
  const response = await request.get("/api/session-token");
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

  try {
    await formal.goto(obsUrl(baseURL, "pokebox", template.publicId, readToken));
    await expect(formal.locator(".poke-cell")).toHaveCount(2);
    await expect(formal.locator(".cell-title")).toHaveCount(0);

    await control.goto(obsUrl(baseURL, "pokebox", template.publicId, controlToken, true));
    await expect(control.getByRole("button", { name: "重設" })).toBeVisible();
    await control.locator(".poke-cell").first().click();
    await expect(formal.locator(".cell-title").first()).toContainText("CI 戳戳獎品", { timeout: 10_000 });

    await control.getByRole("button", { name: "重設" }).click();
    await expect(formal.locator(".cell-title")).toHaveCount(0, { timeout: 10_000 });
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
      resultDisplayDurationSeconds: 1,
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
  const formal = await formalContext.newPage();
  const control = await controlContext.newPage();

  try {
    await formal.goto(obsUrl(baseURL, "roulette", template.publicId, readToken));
    await expect(formal.locator(".roulette-container")).toHaveCount(1);
    await expect(formal.locator(".obs-roulette-wrapper")).toHaveClass(/obs-animation-idle/);
    await expect(formal.locator(".obs-win-result")).toHaveCount(0);

    await control.goto(obsUrl(baseURL, "roulette", template.publicId, controlToken, true));
    const wheel = control.locator(".roulette-container");
    await wheel.focus();
    await wheel.press("Enter");
    await expect(formal.locator(".obs-win-result")).toBeVisible({ timeout: 10_000 });
    await expect(formal.locator(".win-title")).toContainText("CI 轉盤獎品");
  } finally {
    await formalContext.close();
    await controlContext.close();
    const deleted = await request.delete(`/api/roulette-templates/${template.id}`, {
      headers: { "X-EasyLottery-Session-Token": adminToken }
    });
    expect(deleted.status()).toBe(204);
  }
});
