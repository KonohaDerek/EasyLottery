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

async function addLockedRoster(page, names, prize) {
  for (const name of names) {
    await page.getByLabel("參加者名稱").fill(name);
    await page.getByRole("button", { name: "新增參加者" }).click();
  }
  await page.getByLabel("新增獎項").fill(prize);
  await page.getByRole("button", { name: "新增獎項" }).click();
  await page.getByRole("button", { name: "鎖定名單" }).click();
  const lockStatus = page.getByRole("status", { name: "抽獎名單鎖定狀態" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  return lockStatus;
}

test("a locked pool cannot be unlocked while a reloaded draw is unresolved, and the retried winner is applied exactly once", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const drawRequests = [];
  let failFirstSave = true;
  await page.route("**/api/lottery-draw-records", async route => {
    if (route.request().method() === "POST") {
      drawRequests.push(route.request().postDataJSON());
      if (failFirstSave) {
        failFirstSave = false;
        await route.abort("connectionreset");
        return;
      }
    }
    await route.fallback();
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  const lockStatus = await addLockedRoster(page, ["Locked A", "Locked B"], "Locked prize");

  // The save is lost in transit; the API has no record, so the draw stays pending and retryable.
  await page.locator(".draw-button").click();
  const pending = page.locator(".pending-draw");
  const retry = page.getByRole("button", { name: "以相同結果重試保存" });
  await expect(retry).toBeVisible();
  expect(drawRequests).toHaveLength(1);

  await page.reload({ waitUntil: "networkidle" });
  await expect(pending).toContainText("抽獎結果待對帳");
  await expect(retry).toBeVisible();
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(page.locator(".people-list h3 span")).toHaveText("2 / 2");
  await expect(page.locator(".prize-list li")).toHaveCount(1);

  // Unlock is disabled while the draw is unresolved, and the handler refuses it even if the control is forced.
  const unlock = lockStatus.getByRole("button", { name: "解除鎖定" });
  await expect(unlock).toBeDisabled();
  const storedBefore = await page.evaluate(() => ({ lock: sessionStorage.getItem("easy-lottery.core-lottery.locked-pool"), local: JSON.stringify(localStorage) }));
  for (let attempt = 0; attempt < 2; attempt++) {
    await unlock.evaluate(button => { button.removeAttribute("disabled"); button.click(); });
  }
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(lockStatus.getByRole("button", { name: "確認解除鎖定" })).toHaveCount(0);
  await expect(page.getByLabel("參加者名稱")).toBeDisabled();
  expect(await page.evaluate(() => ({ lock: sessionStorage.getItem("easy-lottery.core-lottery.locked-pool"), local: JSON.stringify(localStorage) }))).toEqual(storedBefore);

  // Retrying confirms the same draw and applies the winner to the restored locked roster and prize queue.
  await retry.click();
  await expect(page.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  await expect(pending).toHaveCount(0);
  expect(drawRequests).toHaveLength(2);
  // The restored retry body is the original API payload: the storage-only `phase` marker is never sent.
  expect(drawRequests[1]).not.toHaveProperty("phase");
  expect(drawRequests[1]).toEqual(drawRequests[0]);
  await expect(page.locator(".participant-list li.is-winner")).toHaveCount(1);
  await expect(page.locator(".prize-list li")).toHaveCount(0);
  await expect(page.locator(".winner-list li")).toHaveCount(1);
  await expect(unlock).toBeEnabled();

  // A further reload restores the advanced pool without applying the winner again or resubmitting.
  await page.reload({ waitUntil: "networkidle" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(pending).toHaveCount(0);
  await expect(page.locator(".participant-list li.is-winner")).toHaveCount(1);
  await expect(page.locator(".prize-list li")).toHaveCount(0);
  await expect(page.locator(".winner-list li")).toHaveCount(1);
  expect(drawRequests).toHaveLength(2);
});

test("a saved draw whose locked-pool write is rejected stays pending and reconciles the same drawId exactly once after reload", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  const rejectFlag = "e2e.reject-locked-pool-writes";
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // Synthetic storage fault: while the flag is set, only writes of the locked-pool key in sessionStorage throw.
  await page.addInitScript(({ key, flag }) => {
    const original = Storage.prototype.setItem;
    Storage.prototype.setItem = function (name, value) {
      if (this === window.sessionStorage && name === key && window.sessionStorage.getItem(flag) === "1")
        throw new DOMException("synthetic quota exceeded", "QuotaExceededError");
      return original.call(this, name, value);
    };
  }, { key: lockKey, flag: rejectFlag });
  const drawRequests = [];
  const summaryRequests = [];
  page.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (outgoing.method() === "POST" && path === "/api/lottery-draw-records") drawRequests.push(outgoing.postDataJSON());
    if (outgoing.method() === "GET" && /^\/api\/lottery-draw-records\/[^/]+\/summary$/.test(path)) summaryRequests.push(path);
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  const lockStatus = await addLockedRoster(page, ["Storage A", "Storage B"], "Storage prize");
  const lockBefore = await page.evaluate(key => sessionStorage.getItem(key), lockKey);
  await page.evaluate(flag => sessionStorage.setItem(flag, "1"), rejectFlag);

  // The server saves the draw, but advancing the tab lock fails: the same drawId must stay pending and locked.
  await page.locator(".draw-button").click();
  const pending = page.locator(".pending-draw");
  await expect(pending).toContainText("無法更新本分頁的鎖定名單");
  expect(drawRequests).toHaveLength(1);
  const drawId = drawRequests[0].drawId;
  expect(await page.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawId, pendingKey)).toBe(drawId);
  expect(await page.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockBefore);
  await expect(page.locator(".draw-button")).toBeDisabled();
  await expect(lockStatus.getByRole("button", { name: "解除鎖定" })).toBeDisabled();
  await expect(page.getByLabel("參加者名稱")).toBeDisabled();
  await expect(page.locator(".winner-list li")).toHaveCount(0);
  await expect(page.locator(".prize-list li")).toHaveCount(1);

  // Re-querying while storage still rejects the write neither duplicates the winner nor resubmits.
  const summariesBefore = summaryRequests.length;
  await page.getByRole("button", { name: "重新查詢紀錄" }).click();
  await expect.poll(() => summaryRequests.length).toBeGreaterThan(summariesBefore);
  await expect(pending).toContainText("無法更新本分頁的鎖定名單");
  await expect(page.locator(".winner-list li")).toHaveCount(0);
  expect(await page.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawId, pendingKey)).toBe(drawId);
  expect(drawRequests).toHaveLength(1);

  // Storage recovers; reload reconciles the same drawId against the restored pre-draw lock and only then clears it.
  await page.evaluate(flag => sessionStorage.removeItem(flag), rejectFlag);
  const summariesBeforeReload = summaryRequests.length;
  await page.reload({ waitUntil: "networkidle" });
  await expect(page.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  await expect(pending).toHaveCount(0);
  expect(summaryRequests.slice(summariesBeforeReload)).toContain(`/api/lottery-draw-records/${drawId}/summary`);
  expect(drawRequests).toHaveLength(1);
  expect(await page.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  const advanced = await page.evaluate(key => JSON.parse(sessionStorage.getItem(key)), lockKey);
  expect(advanced.winners.map(winner => winner.drawId)).toEqual([drawId]);
  expect(advanced.prizes).toEqual([]);
  await expect(page.locator(".participant-list li.is-winner")).toHaveCount(1);
  await expect(page.locator(".winner-list li")).toHaveCount(1);
  await expect(page.locator(".prize-list li")).toHaveCount(0);
  await expect(lockStatus.getByRole("button", { name: "解除鎖定" })).toBeEnabled();

  // Another reload keeps exactly one award and sends nothing new.
  await page.reload({ waitUntil: "networkidle" });
  await expect(pending).toHaveCount(0);
  await expect(page.locator(".winner-list li")).toHaveCount(1);
  await expect(page.locator(".participant-list li.is-winner")).toHaveCount(1);
  expect(drawRequests).toHaveLength(1);
});

test("a member CSV still being read when the pool is locked is never applied", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // Synthetic delay: File.text() (Blob.prototype.text) waits until the test releases it.
  await page.addInitScript(() => {
    const original = Blob.prototype.text;
    let release;
    const gate = new Promise(resolve => { release = resolve; });
    window.__releaseCsvText = () => release();
    Blob.prototype.text = async function () {
      window.__csvTextStarted = true;
      await gate;
      return original.call(this);
    };
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await page.getByLabel("參加者名稱").fill("Before lock");
  await page.getByRole("button", { name: "新增參加者" }).click();
  await page.getByLabel("新增獎項").fill("CSV race prize");
  await page.getByRole("button", { name: "新增獎項" }).click();
  const rosterCount = page.locator(".people-list h3 span");
  await expect(rosterCount).toHaveText("1 / 1");

  const csvInput = page.getByLabel("YouTube Studio 會員 CSV");
  await expect(csvInput).toBeEnabled();
  await csvInput.setInputFiles({ name: "members.csv", mimeType: "text/csv", buffer: Buffer.from("會員,目前級別\nRace Member,Gold\nRace Member Two,Gold\n", "utf8") });
  await page.waitForFunction(() => window.__csvTextStarted === true);

  // Lock while the file read is still in flight, then let the read finish.
  await page.getByRole("button", { name: "鎖定名單" }).click();
  const lockStatus = page.getByRole("status", { name: "抽獎名單鎖定狀態" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  const lockBefore = await page.evaluate(key => sessionStorage.getItem(key), lockKey);
  await page.evaluate(() => window.__releaseCsvText());

  await expect(page.getByRole("alert").filter({ hasText: "未套用此檔案" })).toBeVisible();
  await expect(rosterCount).toHaveText("1 / 1");
  await expect(page.locator(".participant-list")).not.toContainText("Race Member");
  await expect(page.locator(".prize-list li")).toHaveCount(1);
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  expect(await page.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockBefore);
});

test("a saved draw from another tab's lock is never applied to this tab's different lock and keeps it pending and blocked until a fresh unlocked tab reconciles it", async ({ context, page: pageB, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  // Both pages share one browser context: localStorage (the pending draw ID) is shared, sessionStorage (the lock) is not.
  await pageB.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const bRequests = [];
  pageB.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) bRequests.push(`${outgoing.method()} ${path}`);
  });

  // Page B locks synthetic pool P2.
  await pageB.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await addLockedRoster(pageB, ["Pool2 A", "Pool2 B", "Pool2 C"], "Pool2 prize");
  const lockB = await pageB.evaluate(key => sessionStorage.getItem(key), lockKey);

  // Page A locks synthetic pool P1; its POST is persisted by the API but the response is lost, and its summary
  // lookups return 503 so the draw stays pending in the shared localStorage.
  const pageA = await context.newPage();
  await pageA.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const aDrawRequests = [];
  let persistedStatus = null;
  await pageA.route("**/api/lottery-draw-records", async route => {
    if (route.request().method() === "POST" && persistedStatus === null) {
      aDrawRequests.push(route.request().postDataJSON());
      persistedStatus = (await route.fetch()).status();
      await route.abort("connectionreset");
      return;
    }
    await route.fallback();
  });
  await pageA.route("**/api/lottery-draw-records/*/summary", route =>
    route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "synthetic unavailable" }) }));
  await pageA.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await addLockedRoster(pageA, ["Pool1 A", "Pool1 B"], "Pool1 prize");
  // A's pre-draw tab lock, as a browser session restore of the closed tab A would bring it back.
  const lockA = await pageA.evaluate(key => sessionStorage.getItem(key), lockKey);
  await pageA.locator(".draw-button").click();
  await expect(pageA.locator(".pending-draw")).toContainText("保存結果仍未知");
  expect(aDrawRequests).toHaveLength(1);
  expect(persistedStatus).toBeGreaterThanOrEqual(200);
  expect(persistedStatus).toBeLessThan(300);
  const drawId = aDrawRequests[0].drawId;
  expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawId, pendingKey)).toBe(drawId);

  const pending = pageB.locator(".pending-draw");
  const lockAlert = pageB.locator(".pool-lock-error");
  const clearLock = lockAlert.getByRole("button", { name: "清除鎖定資料" });
  async function expectBUntouchedAndBlocked() {
    await expect(pending).toContainText("與本分頁目前恢復的鎖定名單狀態");
    await expect(pending).toContainText("不一致");
    await expect(pending).toContainText("請勿清除本分頁的鎖定，也請勿重新抽獎");
    await expect(lockAlert).toBeVisible();
    await expect(pageB.locator(".people-list h3 span")).toHaveText("3 / 3");
    await expect(pageB.locator(".participant-list li.is-winner")).toHaveCount(0);
    await expect(pageB.locator(".prize-list li")).toHaveCount(1);
    await expect(pageB.locator(".prize-list")).toContainText("Pool2 prize");
    await expect(pageB.locator(".winner-list li")).toHaveCount(0);
    await expect(pageB.locator(".draw-button")).toBeDisabled();
    await expect(clearLock).toBeDisabled();
    expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawId, pendingKey)).toBe(drawId);
    expect(await pageB.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockB);
  }

  // Page B reloads, picks up A's pending ID and receives the P1 record, which does not match its P2 lock.
  await pageB.reload({ waitUntil: "networkidle" });
  expect(bRequests).toContain(`GET /api/lottery-draw-records/${drawId}/summary`);
  await expectBUntouchedAndBlocked();

  // Still unapplied and blocked across another reload; B never submits a draw of its own.
  await pageB.reload({ waitUntil: "networkidle" });
  await expectBUntouchedAndBlocked();
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);

  // Origin tab A closes without reconciling, so its tab-scoped P1 lock is gone; the pending ID stays in localStorage.
  await pageA.close();
  expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawId, pendingKey)).toBe(drawId);

  // Supported recovery: a fresh tab C in the same browser profile, with no locked pool, reconciles the shared pending ID.
  const pageC = await context.newPage();
  await pageC.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const cRequests = [];
  pageC.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) cRequests.push(`${outgoing.method()} ${path}`);
  });
  // The first lookup is unavailable so the pending draw is observably kept until a saved record is reconciled.
  const unavailableSummary = route =>
    route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "synthetic unavailable" }) });
  await pageC.route("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pageC.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  expect(await pageC.evaluate(key => sessionStorage.getItem(key), lockKey)).toBeNull();
  await expect(pageC.getByRole("status", { name: "抽獎名單鎖定狀態" })).toHaveCount(0);
  const pendingC = pageC.locator(".pending-draw");
  await expect(pendingC).toContainText("保存結果仍未知");
  expect(await pageC.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawId, pendingKey)).toBe(drawId);

  await pageC.unroute("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pendingC.getByRole("button", { name: "重新查詢紀錄" }).click();
  // C has no draw or lock of its own, so A's saved draw is only reconciled; nothing is added to C's page.
  await expect(pageC.getByRole("status").filter({ hasText: "已對帳其他分頁" })).toContainText("均未變更");
  await expect(pendingC).toHaveCount(0);
  await expect(pageC.locator(".winner-list li")).toHaveCount(0);
  expect(cRequests).toContain(`GET /api/lottery-draw-records/${drawId}/summary`);
  expect(cRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageC.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  // C records the reconciled draw against P1's lock version in the shared, browser-profile ledger.
  const { lockHash: lockHashA, lockedAtUtc: lockedAtA } = JSON.parse(lockA);
  const ledgerKey = `easy-lottery.core-lottery.resolved-lock-draws.${lockHashA}.${lockedAtA}`;
  expect(await pageC.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);

  // Browser session restore reopens old tab A as D with A's pre-draw sessionStorage lock; no pending ID remains, so
  // only the ledger shows this roster already drew. D must stay blocked: no draw, no unlock, no clear, no POST.
  const pageD = await context.newPage();
  await pageD.addInitScript(({ value, key, lock }) => {
    sessionStorage.setItem("easy-lottery.session-token", value);
    if (sessionStorage.getItem(key) === null) sessionStorage.setItem(key, lock);
  }, { value: token, key: lockKey, lock: lockA });
  const dRequests = [];
  pageD.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) dRequests.push(`${outgoing.method()} ${path}`);
  });
  async function expectDStaleAndBlocked() {
    await expect(pageD.locator(".pending-draw")).toHaveCount(0);
    const staleAlert = pageD.locator(".pool-lock-error");
    await expect(staleAlert).toContainText("舊版本");
    await expect(staleAlert).toContainText(drawId);
    await expect(staleAlert).toContainText("關閉並捨棄此分頁");
    await expect(pageD.locator(".draw-button")).toBeDisabled();
    await expect(staleAlert.getByRole("button", { name: "清除鎖定資料" })).toBeDisabled();
    await expect(pageD.getByRole("status", { name: "抽獎名單鎖定狀態" })).toHaveCount(0);
    await expect(pageD.locator(".prize-list")).toContainText("Pool1 prize");
    expect(await pageD.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockA);
    expect(await pageD.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  }
  await pageD.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await expectDStaleAndBlocked();
  await pageD.reload({ waitUntil: "networkidle" });
  await expectDStaleAndBlocked();
  expect(dRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageD.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);
  await pageD.close();

  // B reloads: the pending draw is gone and B's own P2 lock, roster, prize and winners are exactly as before.
  await pageB.reload({ waitUntil: "networkidle" });
  await expect(pending).toHaveCount(0);
  await expect(lockAlert).toHaveCount(0);
  await expect(pageB.getByRole("status", { name: "抽獎名單鎖定狀態" })).toContainText("抽獎名單已鎖定");
  await expect(pageB.locator(".people-list h3 span")).toHaveText("3 / 3");
  await expect(pageB.locator(".participant-list li.is-winner")).toHaveCount(0);
  await expect(pageB.locator(".prize-list li")).toHaveCount(1);
  await expect(pageB.locator(".prize-list")).toContainText("Pool2 prize");
  await expect(pageB.locator(".winner-list li")).toHaveCount(0);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  expect(await pageB.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockB);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  await pageC.close();
});

test("a same-tab reconcile after locked-pool storage recovers leaves no stale global error", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  const rejectFlag = "e2e.reject-locked-pool-writes";
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // Synthetic storage fault: while the flag is set, only writes of the locked-pool key in sessionStorage throw.
  await page.addInitScript(({ key, flag }) => {
    const original = Storage.prototype.setItem;
    Storage.prototype.setItem = function (name, value) {
      if (this === window.sessionStorage && name === key && window.sessionStorage.getItem(flag) === "1")
        throw new DOMException("synthetic quota exceeded", "QuotaExceededError");
      return original.call(this, name, value);
    };
  }, { key: lockKey, flag: rejectFlag });
  const drawRequests = [];
  page.on("request", outgoing => {
    if (outgoing.method() === "POST" && new URL(outgoing.url()).pathname === "/api/lottery-draw-records") drawRequests.push(outgoing.postDataJSON());
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await addLockedRoster(page, ["Recover A", "Recover B"], "Recover prize");
  await page.evaluate(flag => sessionStorage.setItem(flag, "1"), rejectFlag);

  await page.locator(".draw-button").click();
  const pending = page.locator(".pending-draw");
  await expect(pending).toContainText("無法更新本分頁的鎖定名單");
  const globalError = page.locator(".lottery-alert.error");
  // The storage warning is shown only as the pending-draw message, never as a separate global error.
  await expect(globalError).toHaveCount(0);
  expect(drawRequests).toHaveLength(1);
  const drawId = drawRequests[0].drawId;

  // Storage recovers and the same page reconciles the same drawId without a reload.
  await page.evaluate(flag => sessionStorage.removeItem(flag), rejectFlag);
  await page.getByRole("button", { name: "重新查詢紀錄" }).click();
  await expect(page.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  await expect(pending).toHaveCount(0);
  await expect(globalError).toHaveCount(0);
  expect(drawRequests).toHaveLength(1);
  expect(await page.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  const advanced = await page.evaluate(key => JSON.parse(sessionStorage.getItem(key)), lockKey);
  expect(advanced.winners.map(winner => winner.drawId)).toEqual([drawId]);
  await expect(page.locator(".winner-list li")).toHaveCount(1);
  await expect(page.locator(".prize-list li")).toHaveCount(0);
});

/** Opens a second tab in the same context carrying `lock` as its tab-scoped locked pool (a duplicated or copied tab). */
async function openSameLockTab(context, baseURL, token, lock) {
  const page = await context.newPage();
  await page.addInitScript(({ value, key, lockValue }) => {
    sessionStorage.setItem("easy-lottery.session-token", value);
    if (sessionStorage.getItem(key) === null) sessionStorage.setItem(key, lockValue);
  }, { value: token, key: lockKey, lockValue: lock });
  const requests = [];
  page.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) requests.push(`${outgoing.method()} ${path}`);
  });
  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  const lockStatus = page.getByRole("status", { name: "抽獎名單鎖定狀態" });
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  return { page, requests, lockStatus };
}

test("a same-lock tab is refused while another tab's draw holds the origin lock, then is blocked as stale once that draw resolves", async ({ context, page: pageA, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  await pageA.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const aDrawRequests = [];
  let releaseA;
  const aSaveGate = new Promise(resolve => { releaseA = resolve; });
  await pageA.route("**/api/lottery-draw-records", async route => {
    if (route.request().method() === "POST") {
      aDrawRequests.push(route.request().postDataJSON());
      // A's save stays in flight, so A keeps holding the origin Web Lock until the test releases it.
      await aSaveGate;
    }
    await route.fallback();
  });
  await pageA.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await addLockedRoster(pageA, ["Dup A", "Dup B"], "Dup prize");
  const lockA = await pageA.evaluate(key => sessionStorage.getItem(key), lockKey);

  // B carries the same locked-pool version and loaded before A drew, so its load-time stale check passed.
  const { page: pageB, requests: bRequests, lockStatus: lockStatusB } = await openSameLockTab(context, baseURL, token, lockA);
  await expect(pageB.locator(".draw-button")).toBeEnabled();

  await pageA.locator(".draw-button").click();
  await expect.poll(() => aDrawRequests.length).toBe(1);
  const drawId = aDrawRequests[0].drawId;
  const pendingBefore = await pageB.evaluate(key => localStorage.getItem(key), pendingKey);
  expect(JSON.parse(pendingBefore)).toMatchObject({ phase: "result", drawId });

  // While A holds the lock, B's draw and unlock are refused before anything is drawn, sent or written.
  const bError = pageB.locator(".lottery-alert.error");
  await pageB.locator(".draw-button").click();
  await expect(bError).toContainText("另一個 EasyLottery 分頁正在抽獎");
  await expect(bError).toContainText("未抽選、未送出");
  await lockStatusB.getByRole("button", { name: "解除鎖定" }).click();
  await lockStatusB.getByRole("button", { name: "確認解除鎖定" }).click();
  await expect(bError).toContainText("鎖定名單維持不變");
  await expect(lockStatusB).toContainText("抽獎名單已鎖定");
  await expect(pageB.locator(".pending-draw")).toHaveCount(0);
  await expect(pageB.locator(".winner-list li")).toHaveCount(0);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBe(pendingBefore);
  expect(await pageB.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockA);

  // A finishes and records the reconciled draw in the shared per-lock ledger.
  releaseA();
  await expect(pageA.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  expect(aDrawRequests).toHaveLength(1);
  const { lockHash, lockedAtUtc } = JSON.parse(lockA);
  const ledgerKey = `easy-lottery.core-lottery.resolved-lock-draws.${lockHash}.${lockedAtUtc}`;
  expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();

  // B never reloaded; the under-lock re-check of the ledger blocks its stale copy of the roster.
  await expect(pageB.locator(".draw-button")).toBeEnabled();
  await pageB.locator(".draw-button").click();
  const staleAlert = pageB.locator(".pool-lock-error");
  await expect(staleAlert).toContainText("舊版本");
  await expect(staleAlert).toContainText(drawId);
  await expect(pageB.locator(".draw-button")).toBeDisabled();
  await expect(staleAlert.getByRole("button", { name: "清除鎖定資料" })).toBeDisabled();
  await expect(lockStatusB).toHaveCount(0);
  await expect(pageB.locator(".prize-list")).toContainText("Dup prize");
  await expect(pageB.locator(".winner-list li")).toHaveCount(0);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageB.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockA);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);
  await pageB.close();
});

test("a same-lock tab that has not reloaded adopts another tab's unresolved pending draw instead of starting or replacing one", async ({ context, page: pageA, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  await pageA.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // A's POST is persisted by the API but its response is lost, and A's lookups fail, so A releases the origin lock with
  // its draw still pending in the shared localStorage.
  const aDrawRequests = [];
  let persistedStatus = null;
  await pageA.route("**/api/lottery-draw-records", async route => {
    if (route.request().method() === "POST" && persistedStatus === null) {
      aDrawRequests.push(route.request().postDataJSON());
      persistedStatus = (await route.fetch()).status();
      await route.abort("connectionreset");
      return;
    }
    await route.fallback();
  });
  const unavailableSummary = route =>
    route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "synthetic unavailable" }) });
  await pageA.route("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pageA.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await addLockedRoster(pageA, ["Adopt A", "Adopt B"], "Adopt prize");
  const lockA = await pageA.evaluate(key => sessionStorage.getItem(key), lockKey);

  const { page: pageB, requests: bRequests, lockStatus: lockStatusB } = await openSameLockTab(context, baseURL, token, lockA);
  await pageB.route("**/api/lottery-draw-records/*/summary", unavailableSummary);

  await pageA.locator(".draw-button").click();
  await expect(pageA.locator(".pending-draw")).toContainText("保存結果仍未知");
  expect(aDrawRequests).toHaveLength(1);
  expect(persistedStatus).toBeGreaterThanOrEqual(200);
  expect(persistedStatus).toBeLessThan(300);
  const drawId = aDrawRequests[0].drawId;
  const pendingBefore = await pageB.evaluate(key => localStorage.getItem(key), pendingKey);
  expect(JSON.parse(pendingBefore)?.drawId).toBe(drawId);

  // B has not reloaded, so it shows no pending draw; drawing first reads the shared record and adopts A's exact draw.
  const pendingB = pageB.locator(".pending-draw");
  await expect(pendingB).toHaveCount(0);
  await pageB.locator(".draw-button").click();
  await expect(pendingB).toContainText("保存結果仍未知");
  expect(bRequests).toContain(`GET /api/lottery-draw-records/${drawId}/summary`);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBe(pendingBefore);
  await expect(pageB.locator(".draw-button")).toBeDisabled();
  await expect(lockStatusB.getByRole("button", { name: "解除鎖定" })).toBeDisabled();
  await expect(pageB.locator(".winner-list li")).toHaveCount(0);

  // Once lookups recover, B reconciles A's saved draw onto the identical roster without ever submitting its own.
  await pageB.unroute("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pendingB.getByRole("button", { name: "重新查詢紀錄" }).click();
  await expect(pageB.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  await expect(pendingB).toHaveCount(0);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  const advancedB = await pageB.evaluate(key => JSON.parse(sessionStorage.getItem(key)), lockKey);
  expect(advancedB.winners.map(winner => winner.drawId)).toEqual([drawId]);
  expect(advancedB.prizes).toEqual([]);
  const { lockHash, lockedAtUtc } = JSON.parse(lockA);
  const ledgerKey = `easy-lottery.core-lottery.resolved-lock-draws.${lockHash}.${lockedAtUtc}`;
  expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);

  // The origin tab A then reconciles the same drawId; still exactly one POST across both tabs.
  await pageA.unroute("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pageA.locator(".pending-draw").getByRole("button", { name: "重新查詢紀錄" }).click();
  await expect(pageA.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  expect(aDrawRequests).toHaveLength(1);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageA.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);
  await pageB.close();
});

test("an unlocked tab with its own roster reconciles another tab's adopted locked draw without changing its roster, prizes or winners", async ({ context, page: pageA, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  await pageA.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // A's POST is persisted by the API but its response is lost, and A's lookups fail, so A releases the origin lock with
  // its locked draw still pending in the shared localStorage.
  const aDrawRequests = [];
  let persistedStatus = null;
  await pageA.route("**/api/lottery-draw-records", async route => {
    if (route.request().method() === "POST" && persistedStatus === null) {
      aDrawRequests.push(route.request().postDataJSON());
      persistedStatus = (await route.fetch()).status();
      await route.abort("connectionreset");
      return;
    }
    await route.fallback();
  });
  const unavailableSummary = route =>
    route.fulfill({ status: 503, contentType: "application/json", body: JSON.stringify({ message: "synthetic unavailable" }) });
  await pageA.route("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pageA.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await addLockedRoster(pageA, ["Foreign A", "Foreign B"], "Foreign prize");
  const lockA = await pageA.evaluate(key => sessionStorage.getItem(key), lockKey);

  // B has no lock but its own editable participant and prize, and loads before A's pending record exists.
  const pageB = await context.newPage();
  await pageB.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const bRequests = [];
  pageB.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) bRequests.push(`${outgoing.method()} ${path}`);
  });
  await pageB.route("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pageB.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  await pageB.getByLabel("參加者名稱").fill("Own B");
  await pageB.getByRole("button", { name: "新增參加者" }).click();
  await pageB.getByLabel("新增獎項").fill("Own prize");
  await pageB.getByRole("button", { name: "新增獎項" }).click();
  expect(await pageB.evaluate(key => sessionStorage.getItem(key), lockKey)).toBeNull();
  await expect(pageB.getByRole("status", { name: "抽獎名單鎖定狀態" })).toHaveCount(0);
  async function expectBRosterUnchanged() {
    await expect(pageB.locator(".people-list h3 span")).toHaveText("1 / 1");
    await expect(pageB.locator(".participant-list")).toContainText("Own B");
    await expect(pageB.locator(".participant-list li.is-winner")).toHaveCount(0);
    await expect(pageB.locator(".prize-list li")).toHaveCount(1);
    await expect(pageB.locator(".prize-list")).toContainText("Own prize");
    await expect(pageB.locator(".winner-list li")).toHaveCount(0);
    expect(await pageB.evaluate(key => sessionStorage.getItem(key), lockKey)).toBeNull();
  }
  await expectBRosterUnchanged();

  await pageA.locator(".draw-button").click();
  await expect(pageA.locator(".pending-draw")).toContainText("保存結果仍未知");
  expect(aDrawRequests).toHaveLength(1);
  expect(persistedStatus).toBeGreaterThanOrEqual(200);
  expect(persistedStatus).toBeLessThan(300);
  const drawId = aDrawRequests[0].drawId;
  expect(aDrawRequests[0].poolSnapshotVersion).toBeTruthy();
  const pendingBefore = await pageB.evaluate(key => localStorage.getItem(key), pendingKey);
  expect(JSON.parse(pendingBefore)?.drawId).toBe(drawId);
  const { lockHash, lockedAtUtc } = JSON.parse(lockA);
  const ledgerKey = `easy-lottery.core-lottery.resolved-lock-draws.${lockHash}.${lockedAtUtc}`;

  // B draws: it adopts A's pending record instead of drawing its own; while lookups fail nothing is cleared or applied.
  const pendingB = pageB.locator(".pending-draw");
  await expect(pendingB).toHaveCount(0);
  await pageB.locator(".draw-button").click();
  await expect(pendingB).toContainText("保存結果仍未知");
  expect(bRequests).toContain(`GET /api/lottery-draw-records/${drawId}/summary`);
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBe(pendingBefore);
  expect(await pageB.evaluate(key => localStorage.getItem(key), ledgerKey)).toBeNull();
  await expect(pageB.locator(".draw-button")).toBeDisabled();
  await expectBRosterUnchanged();

  // Once lookups recover, the confirmed match clears the pending ID and records the ledger, but B's page is untouched.
  await pageB.unroute("**/api/lottery-draw-records/*/summary", unavailableSummary);
  await pendingB.getByRole("button", { name: "重新查詢紀錄" }).click();
  const reconciled = pageB.getByRole("status").filter({ hasText: "已對帳其他分頁" });
  await expect(reconciled).toContainText(drawId);
  await expect(reconciled).toContainText("本分頁的參加者、獎項與得獎名單均未變更");
  await expect(pendingB).toHaveCount(0);
  await expect(pageB.locator(".pool-lock-error")).toHaveCount(0);
  await expectBRosterUnchanged();
  expect(bRequests.filter(entry => entry.startsWith("POST"))).toEqual([]);
  expect(await pageB.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  expect(await pageB.evaluate(key => JSON.parse(localStorage.getItem(key))?.drawIds, ledgerKey)).toEqual([drawId]);
  // B's own roster stays drawable; nothing of A's draw consumed B's prize or ticket.
  await expect(pageB.locator(".draw-button")).toBeEnabled();
  await pageB.close();
});

test("without Web Locks the page fails closed: drawing, reconciling and unlocking are refused and nothing is written", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // Synthetic unsupported browser: navigator.locks is absent.
  await page.addInitScript(() => {
    Object.defineProperty(Navigator.prototype, "locks", { configurable: true, get: () => undefined });
  });
  const drawRequests = [];
  page.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) drawRequests.push(`${outgoing.method()} ${path}`);
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  const unsupported = page.locator(".origin-lock-unsupported");
  await expect(unsupported).toContainText("Web Locks");

  // Roster editing and locking still work; drawing is disabled and a forced click is refused by the handler.
  const lockStatus = await addLockedRoster(page, ["NoLock A", "NoLock B"], "NoLock prize");
  const drawButton = page.locator(".draw-button");
  await expect(drawButton).toBeDisabled();
  await drawButton.evaluate(button => { button.removeAttribute("disabled"); button.click(); });
  const unlock = lockStatus.getByRole("button", { name: "解除鎖定" });
  await expect(unlock).toBeDisabled();
  const lockBefore = await page.evaluate(key => sessionStorage.getItem(key), lockKey);
  for (let attempt = 0; attempt < 2; attempt++) {
    await unlock.evaluate(button => { button.removeAttribute("disabled"); button.click(); });
  }
  await expect(lockStatus).toContainText("抽獎名單已鎖定");
  await expect(page.locator(".winner-list li")).toHaveCount(0);
  await expect(page.locator(".prize-list li")).toHaveCount(1);
  expect(drawRequests).toEqual([]);
  expect(await page.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  expect(await page.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockBefore);

  // A stored pending draw is adopted for display but never reconciled, retried or cleared without the origin lock.
  const seeded = JSON.stringify({
    phase: "result",
    drawId: "11111111-2222-4333-8444-555555555555",
    winnerParticipantId: "seed-1",
    ruleSnapshot: {
      algorithmVersion: "secure-random-index-v1",
      eligibilityPolicyVersion: "not-previously-won-v1",
      groupWinnerPolicyVersion: "exclude-lottery-group-after-win-v1",
      eligibleEntryCount: 1,
      prizeSelectionPolicyVersion: "first-remaining-prize-v1",
      prizeQueue: ["Seed prize"]
    },
    participants: [{ participantId: "seed-1", lotteryGroupId: "seed-1", displayName: "Seed", level: "", group: "", tags: [], eligibilityStatus: "eligible" }]
  });
  await page.evaluate(({ key, value }) => localStorage.setItem(key, value), { key: pendingKey, value: seeded });
  await page.reload({ waitUntil: "networkidle" });
  const pending = page.locator(".pending-draw");
  await expect(pending).toContainText("不支援跨分頁抽獎鎖");
  // Re-query is disabled without the origin lock, and the handler refuses it even if the control is forced.
  const requery = pending.getByRole("button", { name: "重新查詢紀錄" });
  await expect(requery).toBeDisabled();
  await requery.evaluate(button => { button.removeAttribute("disabled"); button.click(); button.setAttribute("disabled", ""); });
  await expect(requery).toBeDisabled();
  await expect(pending).toContainText("不支援跨分頁抽獎鎖");
  await expect(drawButton).toBeDisabled();
  expect(drawRequests).toEqual([]);
  expect(await page.evaluate(key => localStorage.getItem(key), pendingKey)).toBe(seeded);
  expect(await page.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lockBefore);
});

test("a chat poll in flight when the final pending-draw write fails cannot append a participant that result recovery would discard", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  // Synthetic storage fault: only the final (phase "result") pending-draw write throws; prepared snapshots still persist.
  await page.addInitScript(key => {
    const original = Storage.prototype.setItem;
    Storage.prototype.setItem = function (name, value) {
      if (this === window.localStorage && name === key) {
        let phase = null;
        try { phase = JSON.parse(value)?.phase; } catch { /* not JSON */ }
        if (phase === "result") throw new DOMException("synthetic quota exceeded", "QuotaExceededError");
      }
      return original.call(this, name, value);
    };
  }, pendingKey);
  // Synthetic YouTube chat: the first poll is held in flight until the test releases it with one matching message.
  await page.route("**/api/lottery/youtube/status", route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ configured: true }) }));
  await page.route("**/api/lottery/youtube/live-chat/resolve", route =>
    route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ chatId: "e2e-chat" }) }));
  let releasePoll;
  const pollGate = new Promise(resolve => { releasePoll = resolve; });
  let pollCount = 0;
  let pollSettled = false;
  await page.route("**/api/lottery/youtube/live-chat/*/messages", async route => {
    if (++pollCount > 1) {
      await route.fulfill({ status: 200, contentType: "application/json", body: "[]" });
      return;
    }
    await pollGate;
    const late = [{ id: "e2e-late-1", publishedAt: new Date(Date.now() + 60_000).toISOString(), type: "textMessageEvent", authorName: "Chat Late", authorId: "UC-e2e-chat-late", displayMessage: "join" }];
    try { await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(late) }); }
    catch { /* the page already aborted this poll */ }
    pollSettled = true;
  });
  // The save stays in flight so the poll can be released while the selected draw is still being recovered.
  const drawRequests = [];
  let releaseSave;
  const saveGate = new Promise(resolve => { releaseSave = resolve; });
  await page.route("**/api/lottery-draw-records", async route => {
    if (route.request().method() === "POST") {
      drawRequests.push(route.request().postDataJSON());
      await saveGate;
    }
    await route.fallback();
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  for (const name of ["Chat A", "Chat B"]) {
    await page.getByLabel("參加者名稱").fill(name);
    await page.getByRole("button", { name: "新增參加者" }).click();
  }
  await page.getByLabel("新增獎項").fill("Chat prize");
  await page.getByRole("button", { name: "新增獎項" }).click();
  await page.getByLabel("直播網址").fill("https://www.youtube.com/watch?v=e2e-synthetic");
  await page.getByLabel(/通關密語/).fill("join");
  await page.getByRole("button", { name: "開始擷取" }).click();
  const chatStatus = page.locator(".chat-card .card-actions .chat-status");
  await expect(chatStatus).toContainText("聊天室連線中");
  await expect.poll(() => pollCount, { timeout: 15_000 }).toBe(1);

  // Drawing selects a winner, the final pending write fails, and chat capture stops before the save is awaited.
  await page.locator(".draw-button").click();
  await expect.poll(() => drawRequests.length).toBe(1);
  const pending = page.locator(".pending-draw");
  await expect(pending).toBeVisible();
  await expect(page.locator(".lottery-alert.error")).toContainText("未能寫入瀏覽器暫存");
  await expect(chatStatus).toHaveText("聊天室擷取已停止。");
  await expect(page.getByRole("button", { name: "開始擷取" })).toBeVisible();

  // The poll that was already in flight returns a matching message while the save is still pending.
  releasePoll();
  await expect.poll(() => pollSettled).toBe(true);
  await expect(page.locator(".participant-list li")).toHaveCount(2);
  await expect(page.locator(".participant-list")).not.toContainText("Chat Late");

  releaseSave();
  await expect(page.getByRole("status").filter({ hasText: "已保存抽獎紀錄" })).toBeVisible();
  await expect(pending).toHaveCount(0);
  expect(drawRequests).toHaveLength(1);
  expect(drawRequests[0].participants.map(participant => participant.displayName).sort()).toEqual(["Chat A", "Chat B"]);
  // The late chat entrant was never accepted: not in the roster, not in recent messages, and capture stayed stopped.
  await expect(page.locator(".participant-list li")).toHaveCount(2);
  await expect(page.locator(".participant-list li.is-winner")).toHaveCount(1);
  await expect(page.locator(".participant-list")).not.toContainText("Chat Late");
  await expect(page.locator(".chat-message-list")).toHaveCount(0);
  await expect(chatStatus).toHaveText("聊天室擷取已停止。");
  expect(pollCount).toBe(1);
});

test("a corrupt reconciled-draw ledger for a restored lock keeps draw, unlock and clear disabled and directs the operator to the maintainer", async ({ page, request, baseURL }) => {
  const token = await adminToken(request);
  const pendingKey = "easy-lottery.pending-core-draw-id";
  await page.addInitScript(value => sessionStorage.setItem("easy-lottery.session-token", value), token);
  const drawRequests = [];
  page.on("request", outgoing => {
    const path = new URL(outgoing.url()).pathname;
    if (path.startsWith("/api/lottery-draw-records")) drawRequests.push(`${outgoing.method()} ${path}`);
  });

  await page.goto(`${baseURL}/`, { waitUntil: "networkidle" });
  const lockStatus = await addLockedRoster(page, ["Ledger A", "Ledger B"], "Ledger prize");
  const lock = await page.evaluate(key => sessionStorage.getItem(key), lockKey);
  const { lockHash, lockedAtUtc } = JSON.parse(lock);
  const ledgerKey = `easy-lottery.core-lottery.resolved-lock-draws.${lockHash}.${lockedAtUtc}`;
  // Synthetic corruption of this lock's reconciled-draw ledger; the tab-scoped lock itself stays valid.
  const corrupt = "{synthetic-corrupt-ledger";
  await page.evaluate(({ key, value }) => localStorage.setItem(key, value), { key: ledgerKey, value: corrupt });
  await page.reload({ waitUntil: "networkidle" });

  const lockAlert = page.locator(".pool-lock-error");
  const clearLock = lockAlert.getByRole("button", { name: "清除鎖定資料" });
  async function expectBlockedWithGuidance() {
    await expect(lockAlert).toContainText("已損毀或格式無效");
    await expect(lockAlert).toContainText(ledgerKey);
    await expect(lockAlert).toContainText("沒有可自行修復此紀錄的功能");
    await expect(lockAlert).toContainText("保留此瀏覽器設定檔與本分頁現況");
    await expect(lockAlert).toContainText("聯絡系統維護人員");
    // No unavailable repair mechanism is claimed.
    await expect(lockAlert).not.toContainText("請管理員至");
    await expect(lockAlert).not.toContainText("依據伺服器紀錄修復");
    await expect(page.locator(".draw-button")).toBeDisabled();
    await expect(clearLock).toBeDisabled();
    // The unverifiable lock hides the lock status panel, so no unlock control is offered.
    await expect(lockStatus).toHaveCount(0);
    await expect(page.locator(".prize-list li")).toHaveCount(1);
    await expect(page.locator(".winner-list li")).toHaveCount(0);
    expect(await page.evaluate(key => sessionStorage.getItem(key), lockKey)).toBe(lock);
    expect(await page.evaluate(key => localStorage.getItem(key), ledgerKey)).toBe(corrupt);
    expect(await page.evaluate(key => localStorage.getItem(key), pendingKey)).toBeNull();
  }
  await expectBlockedWithGuidance();

  // Forced clicks on the disabled controls are refused by their handlers: nothing is drawn, sent, cleared or rewritten.
  await page.locator(".draw-button").evaluate(button => { button.removeAttribute("disabled"); button.click(); button.setAttribute("disabled", ""); });
  await clearLock.evaluate(button => { button.removeAttribute("disabled"); button.click(); button.setAttribute("disabled", ""); });
  await expectBlockedWithGuidance();
  expect(drawRequests).toEqual([]);

  // Still blocked with the same guidance after a reload; the corrupt ledger is never overwritten.
  await page.reload({ waitUntil: "networkidle" });
  await expectBlockedWithGuidance();
  expect(drawRequests).toEqual([]);
});
