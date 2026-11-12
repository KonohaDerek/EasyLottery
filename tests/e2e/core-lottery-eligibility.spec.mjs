import { expect, test } from "./fixtures.mjs";

const roundId = "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0001";
const snapshotId = "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0002";
const profileA = "aaaaaaaa-0000-4000-8000-000000000001";
const profileB = "bbbbbbbb-0000-4000-8000-000000000002";
const lockKey = "easy-lottery.core-lottery.locked-pool";

async function adminToken(request) {
  const response = await request.get("/api/test/session-token");
  expect(response.ok()).toBeTruthy();
  return (await response.json()).token;
}

async function openHistoryDetail(page, baseURL, drawId) {
  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await page.getByRole("link", { name: "抽獎紀錄" }).first().click();
  await expect(page.getByRole("heading", { name: "抽獎紀錄", level: 1 })).toBeVisible();
  await page.getByRole("button", { name: `檢視紀錄 ${drawId}` }).click();
  await expect(page.locator('td[data-testid="roster-interaction-source"]').first()).toBeVisible();
  return page;
}

function snapshotPayload(imported) {
  return {
    roundId,
    eligibleCount: 2,
    existingSnapshotCount: imported ? 2 : 0,
    imported,
    profileIds: [profileA, profileB],
    snapshotId: imported ? snapshotId : null,
    roundName: "E2E 簽到",
    roundStatus: "Settled",
    snapshotCreatedAtUtc: imported ? "2026-11-10T00:00:00Z" : null,
    entries: [
      { audienceProfileId: profileA, displayName: "Mika", identities: [{ platform: "youtube", channelScope: "e2e-channel", accountId: "UC-e2e-mika" }] },
      { audienceProfileId: profileB, displayName: "Mika", identities: [] }
    ]
  };
}

test("core lottery imports interaction eligibility by stable profile, locks the pool across reloads and keeps provenance in history", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);

  let imported = false;
  let importCalls = 0;
  await page.route("**/api/interactions/rounds**", async route => {
    const outgoing = route.request();
    const path = new URL(outgoing.url()).pathname;
    if (outgoing.method() === "GET" && path === "/api/interactions/rounds") {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify([
        { id: roundId, name: "E2E 簽到", status: "Settled", type: "SignIn", commandPrefix: "!join", voteOptions: [], correctAnswerPoints: 0, eligibilityTickets: 1 }
      ]) });
      return;
    }
    if (outgoing.method() === "GET" && path.endsWith("/eligibility-preview")) {
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(snapshotPayload(imported)) });
      return;
    }
    if (outgoing.method() === "POST" && path.endsWith("/eligibility-import")) {
      importCalls++;
      if (importCalls === 1) {
        await route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ error: "暫時無法匯入" }) });
        return;
      }
      imported = true;
      await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(snapshotPayload(true)) });
      return;
    }
    await route.fallback();
  });
  const drawRequests = [];
  page.on("request", outgoing => {
    if (outgoing.method() === "POST" && new URL(outgoing.url()).pathname === "/api/lottery-draw-records") drawRequests.push(outgoing.postDataJSON());
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  const rosterCount = page.locator(".people-list h3 span");
  const participantName = page.getByLabel("參加者名稱");

  // A manual participant with the same display name and no identity link stays distinct.
  await participantName.fill("Mika");
  await page.getByRole("button", { name: "新增參加者" }).click();
  await page.getByLabel("新增獎項").fill("E2E 獎品");
  await page.getByRole("button", { name: "新增獎項" }).click();
  await expect(rosterCount).toHaveText("1 / 1");

  await page.getByRole("button", { name: "載入互動回合" }).click();
  await page.locator(".eligibility-card select").selectOption(roundId);
  await page.getByRole("button", { name: "預覽資格" }).click();
  await expect(page.getByTestId("eligibility-count")).toHaveText("2");
  await expect(page.getByTestId("eligibility-added")).toHaveText("2");
  await expect(page.getByTestId("eligibility-name-collisions")).toHaveText("2");
  await expect(page.getByText("名稱與既有參加者相同但未確認為同一身份", { exact: false })).toBeVisible();
  await expect(rosterCount).toHaveText("1 / 1");

  // API failure leaves the list untouched and is retryable.
  await page.getByRole("button", { name: "確認匯入資格" }).click();
  await expect(page.locator(".eligibility-error")).toContainText("資格匯入失敗");
  await expect(rosterCount).toHaveText("1 / 1");
  await page.getByRole("button", { name: "確認匯入資格" }).click();
  await expect(page.getByRole("status").filter({ hasText: "已匯入" })).toBeVisible();
  await expect(rosterCount).toHaveText("3 / 3");

  // Retrying the same snapshot adds no extra entries.
  await page.getByRole("button", { name: "預覽資格" }).click();
  await expect(page.getByTestId("eligibility-duplicates")).toHaveText("2");
  await expect(page.getByTestId("eligibility-added")).toHaveText("0");
  await page.getByRole("button", { name: "確認匯入資格" }).click();
  await expect(rosterCount).toHaveText("3 / 3");
  expect(importCalls).toBe(3);

  // Lock freezes participant, prize and interaction-import mutations.
  await page.getByRole("button", { name: "鎖定名單" }).click();
  const lockStatus = page.getByRole("status", { name: "抽獎名單鎖定狀態" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(participantName).toBeDisabled();
  await expect(page.getByLabel("新增獎項")).toBeDisabled();
  await expect(page.getByRole("button", { name: "匯入名單" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "確認匯入資格" })).toBeDisabled();

  // Same-tab reload restores the exact locked pool.
  await page.reload({ waitUntil: "networkidle" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(rosterCount).toHaveText("3 / 3");
  await expect(participantName).toBeDisabled();
  const lockedPool = await page.evaluate(key => JSON.parse(sessionStorage.getItem(key)), lockKey);
  expect(lockedPool.lockHash).toMatch(/^[\da-f]{16}$/);
  // Channel scope from the imported identity is carried into the locked pool.
  expect(lockedPool.participants.find(row => row.source?.audienceProfileId === profileA).identity).toEqual({ platform: "youtube", accountId: "UC-e2e-mika", channelScope: "e2e-channel" });

  await page.locator(".draw-button").click();
  await expect(page.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  expect(drawRequests).toHaveLength(1);
  const posted = drawRequests[0];
  // The draw identifies the locked participant-list version it used.
  expect(posted.poolSnapshotVersion).toEqual({ lockHash: lockedPool.lockHash, lockedAtUtc: lockedPool.lockedAtUtc });
  const importedRows = posted.participants.filter(row => row.interactionSource);
  expect(importedRows.map(row => row.interactionSource.audienceProfileId).sort()).toEqual([profileA, profileB]);
  expect(importedRows.every(row => row.interactionSource.roundId === roundId && row.interactionSource.snapshotId === snapshotId)).toBe(true);
  expect(posted.participants.filter(row => !row.interactionSource)).toHaveLength(1);

  // Saved history keeps the source IDs (admin-only endpoint).
  const history = await request.get(`/api/lottery-draw-records/${posted.drawId}/participants?page=1&pageSize=50`, { headers: { "X-EasyLottery-Session-Token": token } });
  expect(history.ok()).toBeTruthy();
  const historyRows = (await history.json()).items;
  expect(historyRows.filter(row => row.interactionSource).map(row => row.interactionSource.audienceProfileId).sort()).toEqual([profileA, profileB]);
  const anonymousHistory = await request.get(`/api/lottery-draw-records/${posted.drawId}/participants`);
  expect([401, 403]).toContain(anonymousHistory.status());
  const summary = await request.get(`/api/lottery-draw-records/${posted.drawId}/summary`, { headers: { "X-EasyLottery-Session-Token": token } });
  expect(summary.ok()).toBeTruthy();
  const savedVersion = (await summary.json()).poolSnapshotVersion;
  expect(savedVersion.lockHash).toBe(lockedPool.lockHash);
  expect(Date.parse(savedVersion.lockedAtUtc)).toBe(Date.parse(lockedPool.lockedAtUtc));

  // Reload after the draw restores the advanced pool (winner marked, prize consumed) without redrawing.
  await page.reload({ waitUntil: "networkidle" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(page.locator(".winner-list li")).toHaveCount(1);
  await expect(page.locator(".prize-list li")).toHaveCount(0);
  expect(drawRequests).toHaveLength(1);

  // Explicit two-step unlock resumes mutations without touching saved history.
  await page.getByRole("button", { name: "解除鎖定" }).click();
  await page.getByRole("button", { name: "確認解除鎖定" }).click();
  await expect(lockStatus).toHaveCount(0);
  await expect(participantName).toBeEnabled();
  const historyAfterUnlock = await request.get(`/api/lottery-draw-records/${posted.drawId}/participants?page=1&pageSize=50`, { headers: { "X-EasyLottery-Session-Token": token } });
  expect((await historyAfterUnlock.json()).items).toEqual(historyRows);

  // The paginated admin list exposes the same pool version.
  const list = await request.get("/api/lottery-draw-records?page=1&pageSize=100", { headers: { "X-EasyLottery-Session-Token": token } });
  expect(list.ok()).toBeTruthy();
  const listItem = (await list.json()).items.find(item => item.drawId === posted.drawId);
  expect(listItem.poolSnapshotVersion).toEqual(savedVersion);

  // The admin history detail shows the lock version and each roster row's interaction source.
  const dialog = await openHistoryDetail(page, baseURL, posted.drawId);
  await expect(dialog.locator('[data-testid="draw-detail-lock-hash"]')).toHaveText(lockedPool.lockHash);
  await expect(dialog.locator('[data-testid="draw-detail-locked-at"]')).toHaveAttribute("datetime", savedVersion.lockedAtUtc);
  const sourceCells = dialog.locator('td[data-testid="roster-interaction-source"]');
  await expect(sourceCells).toHaveCount(3);
  for (const profileId of [profileA, profileB]) {
    const cell = sourceCells.filter({ hasText: profileId });
    await expect(cell).toHaveCount(1);
    await expect(cell).toContainText(roundId);
    await expect(cell).toContainText(snapshotId);
  }
  await expect(sourceCells.filter({ hasNotText: profileA }).filter({ hasNotText: profileB })).toHaveText("—");
});

test("corrupt locked-pool storage blocks drawing until explicitly cleared", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await page.evaluate(key => sessionStorage.setItem(key, JSON.stringify({ schemaVersion: 1, lockedAtUtc: "2026-11-10T00:00:00Z", lockHash: "0000000000000000", hash: "tampered", participants: [{ id: "x", lotteryGroupId: "x", name: "X", level: "", group: "", tags: [], isWinner: false }], prizes: ["P"], winners: [] })), lockKey);
  await page.reload({ waitUntil: "networkidle" });

  const lockAlert = page.locator(".pool-lock-error");
  await expect(lockAlert).toContainText("抽獎名單鎖定無法驗證");
  await expect(lockAlert).toContainText("完整性");
  await expect(page.getByLabel("參加者名稱")).toBeDisabled();
  await expect(page.locator(".people-list h3 span")).toHaveText("0 / 0");

  await page.getByRole("button", { name: "清除鎖定資料" }).click();
  await expect(lockAlert).toHaveCount(0);
  await expect(page.getByLabel("參加者名稱")).toBeEnabled();
  expect(await page.evaluate(key => sessionStorage.getItem(key), lockKey)).toBeNull();
});

test("an unlocked manual draw sends and stores no pool snapshot version", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const drawRequests = [];
  page.on("request", outgoing => {
    if (outgoing.method() === "POST" && new URL(outgoing.url()).pathname === "/api/lottery-draw-records") drawRequests.push(outgoing.postDataJSON());
  });
  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });

  await page.getByLabel("參加者名稱").fill("Manual only");
  await page.getByRole("button", { name: "新增參加者" }).click();
  await page.getByLabel("新增獎項").fill("Unlocked prize");
  await page.getByRole("button", { name: "新增獎項" }).click();
  await page.locator(".draw-button").click();
  await expect(page.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();

  expect(drawRequests).toHaveLength(1);
  expect(drawRequests[0]).not.toHaveProperty("poolSnapshotVersion");
  const summary = await request.get(`/api/lottery-draw-records/${drawRequests[0].drawId}/summary`, { headers: { "X-EasyLottery-Session-Token": token } });
  expect(summary.ok()).toBeTruthy();
  expect((await summary.json()).poolSnapshotVersion ?? null).toBeNull();

  // Unlocked draws stay readable in the history detail with em dashes for version and source.
  const dialog = await openHistoryDetail(page, baseURL, drawRequests[0].drawId);
  await expect(dialog.locator('[data-testid="draw-detail-lock-hash"]')).toContainText("—");
  await expect(dialog.locator('[data-testid="draw-detail-locked-at"]')).toHaveText("—");
  await expect(dialog.locator('td[data-testid="roster-interaction-source"]')).toHaveText("—");
});
