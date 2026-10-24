<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { ApiError, apiRequest } from "../api/client";
import { createParticipant, drawLottery, filterParticipantsByGroup, participantGroups, participantLabel, secureRandomIndex, type DrawResult, type Participant, type Winner } from "../lottery/draw";
import { clearPendingCoreLotteryDrawId, createCoreLotteryDrawId, createCoreLotteryDrawPayload, createCoreLotteryDrawPayloadFromPreparedState, createCoreLotteryDrawPreparedState, matchesPendingCoreLotteryDraw, matchesPreparedCoreLotteryDraw, pendingCoreLotteryDrawStorageKey, preflightPendingCoreLotteryDrawStorage, readPendingCoreLotteryDrawId, storePendingCoreLotteryDrawId, storePendingCoreLotteryDrawPreparedState, type CoreLotteryDrawPayload, type CoreLotteryDrawPreparedState, type CoreLotteryDrawRecord, type CoreLotteryDrawSummary, type CoreLotteryDrawParticipantSnapshot } from "../core-lottery-records/records";
import { expandYoutubeMembers, exportParticipants, exportPrizes, parseParticipantImport, parsePrizes, parseYoutubeMembers, type DataFormat } from "../lottery/import-export";
import { clearOvertimeToken, getYouTubeCredentialStatus, listYouTubeLiveChatMessages, publishSuperChatToOvertime, readDrawingRules, resolveYouTubeLiveChat, writeDrawingRules } from "../lottery/api";
import { processChatBatch, toOvertimeSuperChat, type ChatMessage, type ChatRules } from "../lottery/chat";

const participants = ref<Participant[]>([]);
const prizes = ref<string[]>([]);
const winners = ref<Winner[]>([]);
const pendingDrawId = ref<string | null>(null);
const pendingDraw = ref<CoreLotteryDrawPayload | null>(null);
const pendingPreparedDraw = ref<CoreLotteryDrawPreparedState | null>(null);
const pendingDrawMessage = ref("");
const canRetryPendingDraw = ref(false);
const isSavingDraw = ref(false);
const isReconcilingDraw = ref(false);
const isCheckingPendingDraw = ref(true);
const pendingStorageAvailable = ref(true);
const selectedGroup = ref("");
const busy = ref(false);
const error = ref("");
const notice = ref("");
const participantFormat = ref<DataFormat>("json");
const prizeFormat = ref<DataFormat>("json");
const participantText = ref("");
const prizeText = ref("");
const newPrize = ref("");
const newName = ref("");
const newGroup = ref("");
const newTags = ref("");
const levelRates = ref<Record<string, number>>({});
const settingsETag = ref("");
const ratesReady = ref(false);
const ratesDirty = ref(false);
const youtubeConfigured = ref<boolean | null>(null);
const liveUrl = ref("");
const keyword = ref("");
const whiteList = ref("");
const blackList = ref("");
const cooldownSeconds = ref(0);
const chatActive = ref(false);
const chatConnecting = ref(false);
const chatStatus = ref("");
const chatId = ref("");
const chatCursor = ref("");
const lastAccepted = ref(new Map<string, number>());
const recentMessages = ref<ChatMessage[]>([]);
let pollTimer: number | undefined;
let pollingGeneration: number | null = null;
let chatGeneration = 0;
let chatSessionController: AbortController | null = null;

const groups = computed(() => participantGroups(participants.value));
const visibleParticipants = computed(() => filterParticipantsByGroup(participants.value, selectedGroup.value));
const eligibleCount = computed(() => participants.value.filter(item => !item.isWinner).length);
const drawLocked = computed(() => isCheckingPendingDraw.value || Boolean(pendingDrawId.value) || isSavingDraw.value || isReconcilingDraw.value || !pendingStorageAvailable.value);

interface PendingLocalDraw {
  drawId: string;
  participantsAfter: Participant[];
  prizesAfter: string[];
  winner: Winner;
}

let pendingLocalDraw: PendingLocalDraw | null = null;

async function loadInitialSettings() {
  const [rules, status] = await Promise.allSettled([readDrawingRules(), getYouTubeCredentialStatus()]);
  if (rules.status === "fulfilled") {
    levelRates.value = rules.value.settings.levelRates;
    settingsETag.value = rules.value.etag;
    ratesReady.value = Boolean(rules.value.etag && rules.value.etag !== "*");
    if (!ratesReady.value) error.value = "抽獎倍率缺少版本標記，已停用儲存以避免覆蓋其他設定。";
  } else error.value = `抽獎倍率載入失敗：${messageOf(rules.reason)}`;
  if (status.status === "fulfilled") youtubeConfigured.value = status.value.configured;
  else if (!error.value) error.value = `YouTube 狀態讀取失敗：${messageOf(status.reason)}`;
}

async function saveLevelRates() {
  if (!ratesReady.value || !settingsETag.value) {
    error.value = "目前沒有可用的設定版本，請重新載入抽獎倍率後再儲存。";
    return;
  }
  const submittedRates = { ...levelRates.value };
  busy.value = true;
  error.value = "";
  try {
    settingsETag.value = await writeDrawingRules(submittedRates, settingsETag.value);
    ratesDirty.value = !sameLevelRates(levelRates.value, submittedRates);
    notice.value = ratesDirty.value
      ? "送出時的倍率已儲存；期間新增的變更仍未儲存。"
      : "會員等級倍率已儲存。";
  } catch (cause) {
    error.value = cause instanceof ApiError && cause.status === 409
      ? "抽獎倍率已在其他分頁更新，請重新載入倍率後再儲存。"
      : `抽獎倍率儲存失敗：${messageOf(cause)}`;
  } finally { busy.value = false; }
}

async function refreshLevelRates() {
  try {
    const response = await readDrawingRules();
    levelRates.value = response.settings.levelRates;
    settingsETag.value = response.etag;
    ratesReady.value = Boolean(response.etag && response.etag !== "*");
    if (!ratesReady.value) {
      error.value = "抽獎倍率缺少版本標記，已停用儲存以避免覆蓋其他設定。";
      return;
    }
    ratesDirty.value = false;
    error.value = "";
    notice.value = "已重新載入目前倍率。";
  } catch (cause) { error.value = `倍率重新載入失敗：${messageOf(cause)}`; }
}

function setLevelRate(level: string, value: string) {
  const rate = Number(value);
  levelRates.value = { ...levelRates.value, [level]: Number.isFinite(rate) ? Math.max(1, Math.trunc(rate)) : 1 };
  ratesDirty.value = true;
}

function sameLevelRates(left: Record<string, number>, right: Record<string, number>): boolean {
  const leftEntries = Object.entries(left).sort(([a], [b]) => a.localeCompare(b));
  const rightEntries = Object.entries(right).sort(([a], [b]) => a.localeCompare(b));
  return leftEntries.length === rightEntries.length
    && leftEntries.every(([level, rate], index) => level === rightEntries[index][0] && rate === rightEntries[index][1]);
}

function ensureLevelRates(levels: readonly string[]): boolean {
  const next = { ...levelRates.value };
  let changed = false;
  for (const rawLevel of levels) {
    const level = rawLevel.trim();
    if (level && !Object.keys(next).some(existing => existing.toLocaleLowerCase() === level.toLocaleLowerCase())) {
      next[level] = 1;
      changed = true;
    }
  }
  if (changed) { levelRates.value = next; ratesDirty.value = true; }
  return changed;
}

function importParticipants() {
  if (drawLocked.value) return;
  error.value = "";
  try {
    const imported = parseParticipantImport(participantText.value, participantFormat.value);
    if (!imported.length) { notice.value = "沒有可匯入的參加者資料，原名單保持不變。"; return; }
    participants.value = imported;
    winners.value = [];
    selectedGroup.value = "";
    ensureLevelRates(imported.map(item => item.level));
    notice.value = `已匯入 ${imported.length} 位參加者。`;
    if (ratesDirty.value) void saveLevelRates();
  } catch (cause) { error.value = `參加者匯入失敗：${messageOf(cause)}`; }
}

function importPrizes() {
  if (drawLocked.value) return;
  error.value = "";
  try {
    const imported = parsePrizes(prizeText.value, prizeFormat.value);
    if (!imported.length) { notice.value = "沒有可匯入的獎項資料，原獎項保持不變。"; return; }
    prizes.value = imported;
    winners.value = [];
    notice.value = `已匯入 ${imported.length} 個獎項。`;
  } catch (cause) { error.value = `獎項匯入失敗：${messageOf(cause)}`; }
}

async function importYoutubeMembers(event: Event) {
  if (drawLocked.value) return;
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = "";
  if (!file) return;
  if (!ratesReady.value) {
    error.value = "會員倍率尚未載入，請等候載入成功後再匯入，避免使用錯誤權重。";
    return;
  }
  error.value = "";
  if (file.size > 5 * 1024 * 1024) { error.value = "會員 CSV 超過 5 MB，請先縮小檔案。"; return; }
  try {
    const members = parseYoutubeMembers(await file.text());
    if (!members.length) { notice.value = "CSV 中沒有可匯入的會員，原名單保持不變。"; return; }
    ensureLevelRates(members.map(member => member.level));
    const expanded = expandYoutubeMembers(members, levelRates.value);
    participants.value = expanded.participants;
    winners.value = [];
    selectedGroup.value = "";
    notice.value = `已匯入 ${members.length} 位會員，共 ${expanded.participants.length} 張抽獎籤。`;
    if (ratesDirty.value) await saveLevelRates();
  } catch (cause) { error.value = `YouTube 會員 CSV 匯入失敗：${messageOf(cause)}`; }
}

function download(name: string, content: string, mime: string) {
  const url = URL.createObjectURL(new Blob([content], { type: mime }));
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

function exportParticipantList() {
  const format = participantFormat.value;
  download(`participants-${new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z")}.${format}`,
    exportParticipants(participants.value, format), format === "csv" ? "text/csv;charset=utf-8" : "application/json;charset=utf-8");
}

function exportPrizeList() {
  const format = prizeFormat.value;
  download(`prizes-${new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z")}.${format}`,
    exportPrizes(prizes.value, format), format === "csv" ? "text/csv;charset=utf-8" : "application/json;charset=utf-8");
}

function addPrize() {
  if (drawLocked.value) return;
  const value = newPrize.value.trim();
  if (!value) return;
  prizes.value.push(value);
  newPrize.value = "";
}

function addParticipant() {
  if (drawLocked.value) return;
  const name = newName.value.trim();
  if (!name) return;
  participants.value.push(createParticipant({ name, group: newGroup.value, tags: newTags.value }));
  newName.value = "";
  newGroup.value = "";
  newTags.value = "";
  notice.value = `已新增參加者「${name}」。`;
}

function removeParticipant(participantId: string) {
  if (drawLocked.value) return;
  participants.value = participants.value.filter(item => item.id !== participantId);
  if (selectedGroup.value && !groups.value.some(group => group.toLocaleLowerCase() === selectedGroup.value.toLocaleLowerCase())) selectedGroup.value = "";
}

function applyPersistedWinner(record: CoreLotteryDrawRecord) {
  const local = pendingLocalDraw;
  if (local) {
    participants.value = local.participantsAfter;
    prizes.value = local.prizesAfter;
  }
  const snapshotWinner = record.participants?.find(participant => participant.participantId === record.winnerParticipantId);
  const member = local?.winner.member ?? (snapshotWinner ? participantFromSnapshot(snapshotWinner) : null);
  if (!member) {
    pendingDrawMessage.value = "伺服器保存結果缺少得獎籤快照，已鎖定抽獎；請由管理員核對。";
    return;
  }
  winners.value = [{ member, prize: record.prizeName, drawId: record.drawId }, ...winners.value];
  notice.value = `已保存抽獎紀錄：${record.winnerDisplayName} 獲得「${record.prizeName}」。`;
  pendingLocalDraw = null;
  try {
    clearPendingCoreLotteryDrawId(window.localStorage, record.drawId);
  } catch {
    pendingStorageAvailable.value = false;
    pendingDrawMessage.value = "結果已確認保存，但瀏覽器無法清除待對帳狀態；目前不會開始下一次抽獎。";
    error.value = pendingDrawMessage.value;
  }
  pendingDraw.value = null;
  pendingDrawId.value = null;
  canRetryPendingDraw.value = false;
}

function participantFromSnapshot(snapshot: CoreLotteryDrawParticipantSnapshot): Participant {
  return {
    id: snapshot.participantId,
    lotteryGroupId: snapshot.lotteryGroupId,
    name: snapshot.displayName,
    ...(snapshot.externalIdentity ? { identity: { ...snapshot.externalIdentity } } : {}),
    level: snapshot.level,
    group: snapshot.group,
    tags: [...snapshot.tags],
    isWinner: true
  };
}

function setPendingDraw(value: CoreLotteryDrawPayload) {
  pendingDraw.value = value;
  pendingPreparedDraw.value = null;
  pendingDrawId.value = value.drawId;
  canRetryPendingDraw.value = false;
}

function setPreparedDraw(value: CoreLotteryDrawPreparedState) {
  pendingDraw.value = null;
  pendingPreparedDraw.value = value;
  pendingDrawId.value = value.drawId;
  canRetryPendingDraw.value = false;
}

async function reconcilePendingDraw() {
  const current = pendingDraw.value;
  const prepared = pendingPreparedDraw.value;
  const drawId = current?.drawId ?? prepared?.drawId;
  if (!drawId) return;
  isReconcilingDraw.value = true;
  canRetryPendingDraw.value = false;
  pendingDrawMessage.value = "正在以原抽獎識別碼查詢保存結果…";
  try {
    const summaryResponse = await apiRequest<CoreLotteryDrawSummary>(`/api/lottery-draw-records/${encodeURIComponent(drawId)}/summary`);
    const record = await loadReconciliationRecord(summaryResponse.data);
    const matches = current
      ? matchesPendingCoreLotteryDraw(record, current)
      : prepared ? matchesPreparedCoreLotteryDraw(record, prepared) : false;
    if (!matches) {
      pendingDrawMessage.value = "查到的紀錄與待保存結果不一致，已鎖定抽獎；請由管理員人工核對。";
      return;
    }
    applyPersistedWinner(record);
    pendingDrawMessage.value = "";
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 404) {
      if (current) {
        canRetryPendingDraw.value = true;
        pendingDrawMessage.value = "查無此識別碼的已保存結果。可用相同結果與識別碼重試保存；請勿重新抽獎。";
      } else if (prepared?.selectionState === "selected" && prepared.winnerParticipantId) {
        const exactResult = createCoreLotteryDrawPayloadFromPreparedState(prepared);
        setPendingDraw(exactResult);
        canRetryPendingDraw.value = true;
        try {
          storePendingCoreLotteryDrawId(window.localStorage, exactResult);
          pendingDrawMessage.value = "已從保護快照恢復先前選定的得獎籤；可用相同結果與識別碼重試保存，不會重新抽獎。";
        } catch (storageError) {
          pendingStorageAvailable.value = false;
          pendingDrawMessage.value = `已恢復先前選定的得獎籤，但無法更新瀏覽器暫存；目前頁面仍保留同一結果，可重試保存：${messageOf(storageError)}`;
        }
      } else if (prepared?.selectionState === "ready") {
        try {
          clearPendingCoreLotteryDrawId(window.localStorage, prepared.drawId);
          pendingDraw.value = null;
          pendingPreparedDraw.value = null;
          pendingDrawId.value = null;
          canRetryPendingDraw.value = false;
          pendingDrawMessage.value = "";
          notice.value = "前次只建立了抽獎保護快照，尚未選定得獎籤；已解除暫存，可以重新開始抽獎。";
        } catch (storageError) {
          pendingStorageAvailable.value = false;
          pendingDrawMessage.value = `尚未選定得獎籤，但無法清除抽獎保護快照；工作區維持鎖定：${messageOf(storageError)}`;
        }
      } else {
        pendingDrawMessage.value = "只找到舊版抽獎前保護快照，無法確認當時是否已選定得獎籤；為避免重複抽獎，已鎖定此工作區，請管理員核對。";
      }
    } else {
      pendingDrawMessage.value = `保存結果仍未知：${messageOf(cause)}。目前只會重新查詢，不會重新抽獎或重送。`;
    }
  } finally {
    isReconcilingDraw.value = false;
    isCheckingPendingDraw.value = false;
  }
}

async function loadReconciliationRecord(summary: CoreLotteryDrawSummary): Promise<CoreLotteryDrawRecord> {
  if (!summary.ruleSnapshot) throw new Error("抽獎紀錄摘要缺少規則快照。");
  const participants: CoreLotteryDrawParticipantSnapshot[] = [];
  const pageSize = 100;
  let page = 1;
  let expectedCount: number | null = null;
  let totalPages = 0;
  do {
    const response = await apiRequest<{ items: CoreLotteryDrawParticipantSnapshot[]; page: number; pageSize: number; totalCount: number; totalPages: number }>(
      `/api/lottery-draw-records/${encodeURIComponent(summary.drawId)}/participants?page=${page}&pageSize=${pageSize}`
    );
    const result = response.data;
    if (!Array.isArray(result.items) || result.page !== page || result.pageSize !== pageSize
      || !Number.isSafeInteger(result.totalCount) || result.totalCount < 1
      || !Number.isSafeInteger(result.totalPages) || result.totalPages < 1
      || result.items.length > pageSize || (expectedCount !== null && result.totalCount !== expectedCount)) {
      throw new Error("抽獎名單分頁回應不完整，仍需人工核對。");
    }
    expectedCount = result.totalCount;
    totalPages = result.totalPages;
    participants.push(...result.items);
    page++;
  } while (page <= totalPages);

  if (expectedCount !== participants.length || expectedCount !== summary.participantCount)
    throw new Error("抽獎名單分頁筆數與紀錄摘要不一致。");
  return { ...summary, ruleSnapshot: summary.ruleSnapshot, participants };
}

async function submitPendingDraw() {
  const current = pendingDraw.value;
  if (!current || isSavingDraw.value || isReconcilingDraw.value) return;
  isSavingDraw.value = true;
  canRetryPendingDraw.value = false;
  pendingDrawMessage.value = "正在保存抽獎結果…";
  try {
    const response = await apiRequest<CoreLotteryDrawSummary>("/api/lottery-draw-records", {
      method: "POST",
      body: JSON.stringify(current)
    });
    const saved = response.data.ruleSnapshot ? { ...response.data, ruleSnapshot: response.data.ruleSnapshot, participants: current.participants } : null;
    if (!saved || !matchesPendingCoreLotteryDraw(saved, current)) {
      pendingDrawMessage.value = "伺服器回傳結果與本次抽獎不一致，正在查詢同一識別碼對帳。";
      await reconcilePendingDraw();
      return;
    }
    applyPersistedWinner(saved);
    pendingDrawMessage.value = "";
  } catch (cause) {
    if (cause instanceof ApiError && (cause.status === 400 || cause.status === 413)) {
      canRetryPendingDraw.value = false;
      pendingDrawMessage.value = cause.status === 413
        ? "伺服器拒絕過大的完整參與名單快照；原抽選已鎖定且不會重抽。請保留此頁並聯絡管理員處理。"
        : "伺服器拒絕此抽獎快照；原抽選已鎖定且不會重抽。請由管理員核對資料。";
      return;
    }
    pendingDrawMessage.value = `保存回應未確認（${messageOf(cause)}），正在查詢同一識別碼；不會重新抽獎。`;
    await reconcilePendingDraw();
  } finally {
    isSavingDraw.value = false;
  }
}

async function retryPendingDraw() {
  if (!canRetryPendingDraw.value || !pendingDraw.value) return;
  await submitPendingDraw();
}

async function restorePendingDraw() {
  try {
    const stored = readPendingCoreLotteryDrawId(window.localStorage);
    if (stored) {
      if (stored.phase === "result") {
        setPendingDraw(stored);
        pendingDrawMessage.value = "發現尚待對帳的抽獎結果。先查詢保存紀錄，不會再次抽獎。";
      } else {
        setPreparedDraw(stored);
        pendingDrawMessage.value = "發現抽獎前保存的完整名單保護快照。先查詢原識別碼，不會再次抽獎。";
      }
      await reconcilePendingDraw();
      return;
    }
    isCheckingPendingDraw.value = false;
  } catch (cause) {
    pendingStorageAvailable.value = false;
    isCheckingPendingDraw.value = false;
    error.value = `無法讀取瀏覽器待對帳狀態，為避免重複抽獎已停用抽獎：${messageOf(cause)}`;
  }
}

async function drawPrize() {
  if (drawLocked.value || chatConnecting.value) return;
  let drawId: string;
  let pendingStorage: Storage;
  try {
    drawId = createCoreLotteryDrawId();
    pendingStorage = window.localStorage;
    const probeKey = `${pendingCoreLotteryDrawStorageKey}.probe.${drawId}`;
    pendingStorage.setItem(probeKey, "x".repeat(1024));
    pendingStorage.removeItem(probeKey);
  }
  catch (cause) { error.value = messageOf(cause); return; }
  let result: DrawResult;
  const eligibleBeforeDraw = participants.value.filter(participant => !participant.isWinner);
  if (eligibleBeforeDraw.length === 0 || prizes.value.length === 0) {
    result = drawLottery(participants.value, prizes.value);
    notice.value = result.reason === "no-prizes" ? "請先加入獎項再抽獎。" : "目前沒有尚未得獎的參加者。";
    return;
  }
  let preparedState: CoreLotteryDrawPreparedState;
  try {
    preparedState = createCoreLotteryDrawPreparedState(drawId, participants.value, prizes.value);
    const preflightPayload = createCoreLotteryDrawPayload(drawId, participants.value, prizes.value, eligibleBeforeDraw[0].id);
    preflightPendingCoreLotteryDrawStorage(pendingStorage, preflightPayload);
    storePendingCoreLotteryDrawPreparedState(pendingStorage, preparedState);
  } catch (cause) {
    error.value = `完整名單快照無法安全暫存，尚未抽選：${messageOf(cause)}`;
    return;
  }
  try {
    result = drawLottery(participants.value, prizes.value, candidateCount => {
      if (candidateCount !== eligibleBeforeDraw.length) throw new Error("抽獎候選名單在選定前發生變化。");
      const selectedIndex = secureRandomIndex(candidateCount);
      const selectedParticipant = eligibleBeforeDraw[selectedIndex];
      if (!selectedParticipant) throw new Error("安全亂數回傳了無效的抽獎候選索引。");
      const selectedState = createCoreLotteryDrawPreparedState(drawId, participants.value, prizes.value, selectedParticipant.id);
      // Persist the selected outcome before drawLottery mutates participant and prize state.
      storePendingCoreLotteryDrawPreparedState(pendingStorage, selectedState);
      preparedState = selectedState;
      return selectedIndex;
    });
  }
  catch (cause) {
    if (preparedState.selectionState === "selected") {
      setPreparedDraw(preparedState);
      pendingDrawMessage.value = "得獎籤已安全暫存，但本次抽獎尚未完成；正在查詢原識別碼以恢復同一結果。";
      error.value = `抽獎中止，保留已選定結果供對帳：${messageOf(cause)}`;
      await reconcilePendingDraw();
    } else {
      try { clearPendingCoreLotteryDrawId(pendingStorage, drawId); }
      catch { pendingStorageAvailable.value = false; }
      error.value = `無法安全抽選：${messageOf(cause)}`;
    }
    return;
  }
  if (!result.winner) {
    try { clearPendingCoreLotteryDrawId(pendingStorage, drawId); }
    catch { pendingStorageAvailable.value = false; }
    notice.value = result.reason === "no-prizes" ? "請先加入獎項再抽獎。" : "目前沒有尚未得獎的參加者。";
    return;
  }
  const operation = createCoreLotteryDrawPayload(drawId, participants.value, prizes.value, result.winner.member.id);
  pendingLocalDraw = { drawId, participantsAfter: result.participants, prizesAfter: result.prizes, winner: result.winner };
  try {
    storePendingCoreLotteryDrawId(pendingStorage, operation);
  } catch (cause) {
    pendingStorageAvailable.value = false;
    setPendingDraw(operation);
    pendingDrawMessage.value = `抽選已完成但完整快照未能寫入瀏覽器暫存，已鎖定後續抽獎；此分頁仍保留原結果，請勿重新抽選：${messageOf(cause)}`;
    error.value = pendingDrawMessage.value;
    await submitPendingDraw();
    return;
  }
  if (chatActive.value) stopChatCapture();
  setPendingDraw(operation);
  pendingDrawMessage.value = "抽獎結果已暫存於本機待提交；確認伺服器保存後才會顯示為完成。";
  await submitPendingDraw();
}

function chatRules(): ChatRules {
  return { keyword: keyword.value, whiteList: whiteList.value, blackList: blackList.value, cooldownSeconds: cooldownSeconds.value };
}

async function startChatCapture() {
  if (drawLocked.value || chatConnecting.value || chatActive.value) return;
  const generation = ++chatGeneration;
  const controller = new AbortController();
  chatSessionController?.abort();
  chatSessionController = controller;
  chatConnecting.value = true;
  error.value = "";
  chatStatus.value = "正在連線直播聊天室…";
  try {
    const resolvedChatId = await resolveYouTubeLiveChat(liveUrl.value.trim(), controller.signal);
    if (!isCurrentChat(generation, controller)) return;
    chatId.value = resolvedChatId;
    chatCursor.value = new Date().toISOString();
    lastAccepted.value.clear();
    chatActive.value = true;
    chatStatus.value = "聊天室連線中，每 5 秒檢查新訊息。";
    pollTimer = window.setInterval(() => { void pollChat(generation); }, 5000);
  } catch (cause) {
    if (!isCurrentChat(generation, controller)) return;
    chatActive.value = false;
    chatStatus.value = "";
    error.value = `無法開始擷取聊天室：${messageOf(cause)}`;
  } finally {
    if (generation === chatGeneration) chatConnecting.value = false;
  }
}

function stopChatCapture() {
  chatGeneration++;
  chatConnecting.value = false;
  chatActive.value = false;
  if (pollTimer !== undefined) window.clearInterval(pollTimer);
  pollTimer = undefined;
  chatSessionController?.abort();
  chatSessionController = null;
  chatStatus.value = "聊天室擷取已停止。";
}

function isCurrentChat(generation: number, controller: AbortController): boolean {
  return generation === chatGeneration && chatSessionController === controller && !controller.signal.aborted;
}

async function pollChat(generation: number) {
  const controller = chatSessionController;
  if (!chatActive.value || !controller || !isCurrentChat(generation, controller) || pollingGeneration === generation) return;
  pollingGeneration = generation;
  try {
    const messages = await listYouTubeLiveChatMessages(chatId.value, controller.signal);
    if (!isCurrentChat(generation, controller) || !chatActive.value) return;
    const batch = processChatBatch(messages, chatCursor.value, chatRules(), lastAccepted.value, participants.value);
    chatCursor.value = batch.cursor;
    lastAccepted.value = batch.lastAccepted;
    if (batch.addedParticipants.length) participants.value = [...participants.value, ...batch.addedParticipants];
    if (batch.accepted.length) {
      recentMessages.value = [...recentMessages.value, ...batch.accepted].slice(-5);
      for (const message of batch.accepted) {
        if (!isCurrentChat(generation, controller) || !chatActive.value) break;
        const event = toOvertimeSuperChat(message);
        if (!event) continue;
        try { await publishSuperChatToOvertime(message, event, controller.signal); }
        catch (cause) { if (!controller.signal.aborted) error.value = `聊天室名單已更新，但 SuperChat 加班台同步失敗：${messageOf(cause)}`; }
      }
      if (!isCurrentChat(generation, controller) || !chatActive.value) return;
      chatStatus.value = `已接受 ${batch.accepted.length} 則新訊息。`;
    } else chatStatus.value = batch.toastMessage;
  } catch (cause) {
    if (isCurrentChat(generation, controller)) chatStatus.value = `聊天室暫時無法讀取：${messageOf(cause)}`;
  } finally {
    if (pollingGeneration === generation) pollingGeneration = null;
  }
}

function messageOf(cause: unknown): string {
  return cause instanceof Error ? cause.message : "發生未預期錯誤，請稍後重試。";
}

onMounted(() => { void loadInitialSettings(); void restorePendingDraw(); });
onBeforeUnmount(() => {
  stopChatCapture();
});
</script>

<template>
  <main class="lottery-page">
    <header class="lottery-hero">
      <div class="hero-copy">
        <div class="lottery-eyebrow"><span class="hero-orbit" aria-hidden="true">✦</span> EASYLOTTERY · LIVE DRAW STUDIO</div>
        <h1>抽獎工作區</h1>
        <p>整理名單、設定獎項，讓每一次揭曉都剛剛好。</p>
      </div>
      <div class="hero-stats" aria-label="抽獎狀態">
        <div><span>參加者</span><strong>{{ participants.length }}</strong></div>
        <div><span>尚未得獎</span><strong>{{ eligibleCount }}</strong></div>
        <div><span>剩餘獎項</span><strong>{{ prizes.length }}</strong></div>
        <div><span>已揭曉</span><strong>{{ winners.length }}</strong></div>
      </div>
      <span class="hero-spark spark-one" aria-hidden="true">✦</span><span class="hero-spark spark-two" aria-hidden="true">✧</span>
    </header>

    <div v-if="error" class="lottery-alert error" role="alert"><span>{{ error }}</span><button type="button" aria-label="關閉錯誤訊息" @click="error = ''">關閉</button></div>
    <div v-if="notice" class="lottery-alert notice" role="status" aria-live="polite"><span>{{ notice }}</span><button type="button" aria-label="關閉提示" @click="notice = ''">關閉</button></div>
    <section v-if="pendingDrawId" class="lottery-alert pending-draw" role="status" aria-live="polite">
      <div><strong>抽獎結果待對帳</strong><span>{{ pendingDrawMessage }}</span></div>
      <div class="pending-draw-actions">
        <v-btn size="small" variant="outlined" :disabled="isReconcilingDraw || isSavingDraw" @click="reconcilePendingDraw">重新查詢紀錄</v-btn>
        <v-btn v-if="canRetryPendingDraw" size="small" color="primary" :disabled="isReconcilingDraw || isSavingDraw" @click="retryPendingDraw">以相同結果重試保存</v-btn>
        <v-btn size="small" variant="tonal" to="/lottery-draw-records">查看抽獎紀錄</v-btn>
      </div>
    </section>

    <div class="lottery-column">
      <section class="lottery-card chat-card">
        <div class="card-heading"><div><div class="card-kicker">LIVE CHAT</div><h2>YouTube 直播聊天室</h2></div>
          <span class="connection-pill" :class="youtubeConfigured ? 'is-ready' : 'is-muted'"><span aria-hidden="true"></span>{{ youtubeConfigured === null ? "檢查 API" : youtubeConfigured ? "API 已設定" : "尚未設定 API" }}</span>
        </div>
        <p class="card-description">符合關鍵字與名單條件的觀眾會加入參加者；YouTube API Key 僅由伺服器使用。</p>
        <div v-if="youtubeConfigured !== true" class="youtube-setup-hint" role="status">
          <p>{{ youtubeConfigured === false ? "尚未設定 YouTube Data API Key；設定後才能擷取直播聊天室。" : "目前無法確認 YouTube API 設定，請檢查後再開始擷取。" }}</p>
          <v-btn color="primary" variant="tonal" to="/system/youtube-login">設定 YouTube 直播</v-btn>
        </div>
        <div class="field-grid">
          <label class="field full-field">直播網址<input v-model="liveUrl" type="url" placeholder="https://www.youtube.com/watch?v=…" :disabled="chatActive" autocomplete="url"></label>
          <label class="field full-field">通關密語（逗號或換行分隔）<textarea v-model="keyword" rows="2" placeholder="例如：抽獎,參加" :disabled="chatActive"></textarea></label>
          <label class="field">白名單（留空代表不限）<textarea v-model="whiteList" rows="2" placeholder="頻道名稱" :disabled="chatActive"></textarea></label>
          <label class="field">黑名單<textarea v-model="blackList" rows="2" placeholder="頻道名稱" :disabled="chatActive"></textarea></label>
          <label class="field cooldown-field">同帳號冷卻秒數<input v-model.number="cooldownSeconds" type="number" min="0" max="86400" :disabled="chatActive"></label>
        </div>
        <div class="card-actions">
          <v-btn v-if="!chatActive && !chatConnecting" color="primary" :disabled="!youtubeConfigured || !liveUrl.trim() || !keyword.trim()" @click="startChatCapture">開始擷取</v-btn>
          <v-btn v-else color="error" variant="outlined" @click="stopChatCapture">{{ chatConnecting ? "取消連線" : "停止擷取" }}</v-btn>
          <span class="chat-status" role="status" aria-live="polite">{{ chatStatus }}</span>
        </div>
        <ul v-if="recentMessages.length" class="chat-message-list" aria-label="最近符合條件的聊天室訊息">
          <li v-for="message in recentMessages" :key="message.id"><strong>{{ message.authorName }}</strong><span>{{ message.displayMessage }}</span></li>
        </ul>
      </section>

      <section class="lottery-card import-card">
        <div class="card-heading"><div><div class="card-kicker">IMPORT / EXPORT</div><h2>名單與獎項資料</h2></div></div>
        <div class="import-layout">
          <article class="import-panel"><h3>參加者</h3>
            <label class="field">資料格式<select v-model="participantFormat"><option value="json">JSON</option><option value="csv">CSV</option></select></label>
            <label class="field">匯入資料<textarea v-model="participantText" rows="6" placeholder="貼上參加者 JSON 或 CSV"></textarea></label>
            <div class="compact-actions"><v-btn size="small" variant="tonal" @click="exportParticipantList">匯出名單</v-btn><v-btn size="small" color="primary" :disabled="drawLocked" @click="importParticipants">匯入名單</v-btn></div>
          </article>
          <article class="import-panel"><h3>獎項</h3>
            <label class="field">資料格式<select v-model="prizeFormat"><option value="json">JSON</option><option value="csv">CSV</option></select></label>
            <label class="field">匯入資料<textarea v-model="prizeText" rows="6" placeholder="貼上獎項 JSON 或 CSV"></textarea></label>
            <div class="compact-actions"><v-btn size="small" variant="tonal" @click="exportPrizeList">匯出獎項</v-btn><v-btn size="small" color="primary" :disabled="drawLocked" @click="importPrizes">匯入獎項</v-btn></div>
          </article>
        </div>
        <div class="member-import-row"><label class="field">YouTube Studio 會員 CSV<input type="file" accept=".csv,text/csv" :disabled="!ratesReady || busy || drawLocked" @change="importYoutubeMembers"></label><span>支援「會員」與「目前級別」欄位；依等級倍率展開抽獎籤。</span></div>
        <div v-if="Object.keys(levelRates).length" class="rates-panel">
          <div class="rates-heading"><div><h3>會員等級倍率</h3><p>每位會員依倍率取得抽獎籤，至少為 1。</p></div><div class="compact-actions"><v-btn size="small" variant="outlined" @click="refreshLevelRates">重新載入</v-btn><v-btn size="small" color="primary" :disabled="!ratesReady || !ratesDirty || busy" @click="saveLevelRates">儲存倍率</v-btn></div></div>
          <div class="rate-list"><label v-for="level in Object.keys(levelRates).sort()" :key="level" class="rate-item">{{ level }}<input :aria-label="`${level} 會員倍率`" type="number" min="1" step="1" :value="levelRates[level]" @input="setLevelRate(level, ($event.target as HTMLInputElement).value)"><span>倍</span></label></div>
        </div>
      </section>

      <section class="lottery-card roster-card">
        <div class="card-heading"><div><div class="card-kicker">DRAWING STAGE</div><h2>抽獎名單</h2></div><v-btn class="draw-button" color="primary" size="large" :disabled="drawLocked || chatConnecting || !eligibleCount || !prizes.length" @click="drawPrize"><span aria-hidden="true">✦</span> {{ isSavingDraw ? "保存中…" : "抽獎" }}</v-btn></div>
        <div class="entry-grid">
          <form class="entry-form" @submit.prevent="addPrize"><label class="field">新增獎項<input v-model="newPrize" :disabled="drawLocked" placeholder="輸入獎項名稱"></label><v-btn type="submit" size="small" variant="tonal" :disabled="drawLocked">新增獎項</v-btn></form>
          <form class="entry-form participant-entry" @submit.prevent="addParticipant"><label class="field">參加者名稱<input v-model="newName" :disabled="drawLocked" placeholder="輸入名稱"></label><label class="field">群組<input v-model="newGroup" :disabled="drawLocked" placeholder="例如：VIP、備用"></label><label class="field">標籤<input v-model="newTags" :disabled="drawLocked" placeholder="例如：A 區,高倍率"></label><v-btn type="submit" size="small" variant="tonal" :disabled="drawLocked">新增參加者</v-btn></form>
        </div>
        <div class="draw-columns">
          <section class="draw-list prize-list"><div class="list-heading"><h3>待抽獎項 <span>{{ prizes.length }}</span></h3><button v-if="prizes.length" class="text-action" type="button" :disabled="drawLocked" @click="prizes = []">清除全部</button></div>
            <p v-if="!prizes.length" class="empty-copy">新增獎項後就能開始抽獎。</p>
            <ol v-else><li v-for="(prize, index) in prizes" :key="`${prize}-${index}`"><span class="prize-number">{{ String(index + 1).padStart(2, "0") }}</span><span>{{ prize }}</span><button type="button" :disabled="drawLocked" :aria-label="`移除獎項 ${prize}`" @click="prizes.splice(index, 1)">×</button></li></ol>
          </section>
          <section class="draw-list people-list"><div class="list-heading"><h3>參加者 <span>{{ visibleParticipants.length }} / {{ participants.length }}</span></h3><div class="list-tools"><select v-model="selectedGroup" aria-label="群組篩選" :disabled="drawLocked"><option value="">全部群組</option><option v-for="group in groups" :key="group" :value="group">{{ group }}</option></select><button v-if="participants.length" class="text-action" type="button" :disabled="drawLocked" @click="participants = []; winners = []; selectedGroup = ''">清除名單</button></div></div>
            <p v-if="!visibleParticipants.length" class="empty-copy">匯入名單或新增參加者，抽獎舞台就會亮起。</p>
            <ul v-else class="participant-list"><li v-for="participant in visibleParticipants" :key="participant.id" :class="{ 'is-winner': participant.isWinner }"><span class="participant-avatar" aria-hidden="true">{{ participant.name.slice(0, 1) || "?" }}</span><div class="participant-details"><strong>{{ participantLabel(participant) }}</strong><span v-if="participant.tags.length" class="tag-list"><i v-for="tag in participant.tags" :key="tag">{{ tag }}</i></span></div><button v-if="!participant.isWinner" type="button" :disabled="drawLocked" :aria-label="`移除參加者 ${participant.name}`" @click="removeParticipant(participant.id)">移除</button><span v-else class="winner-mark">已中獎</span></li></ul>
          </section>
        </div>
        <section class="winner-list"><div class="list-heading"><h3>得獎名單 <span>{{ winners.length }}</span></h3><button v-if="winners.length" class="text-action" type="button" :disabled="drawLocked" @click="winners = []">清除得獎紀錄</button></div>
          <p v-if="!winners.length" class="empty-copy">幸運時刻將會在這裡揭曉。</p>
          <ol v-else><li v-for="(winner, index) in winners" :key="winner.drawId ?? `${winner.member.id}-${index}`"><span class="winner-star" aria-hidden="true">✦</span><div><span>恭喜 {{ participantLabel(winner.member) }}</span><strong>{{ winner.prize }}</strong></div></li></ol>
          <v-btn class="history-link" variant="outlined" to="/lottery-draw-records">查看已保存的抽獎紀錄</v-btn>
        </section>
      </section>
    </div>
  </main>
</template>

<style scoped>
.lottery-page { --ink:var(--creator-ink); --muted:var(--creator-muted); --line:var(--creator-border); display:grid; gap:18px; padding:4px 4px 36px; color:var(--ink); }
.lottery-hero { position:relative; display:grid; grid-template-columns:minmax(0,.9fr) 1.1fr; align-items:center; gap:22px; overflow:hidden; min-height:178px; padding:26px 30px; border:1px solid #f0dfea; border-radius:22px; background:radial-gradient(ellipse at 12% 110%,#ffd7e8 0,transparent 42%),radial-gradient(ellipse at 98% 0%,#e1dcff 0,transparent 35%),linear-gradient(120deg,#fffaf5 4%,#fff 54%,#f8f5ff 100%); box-shadow:0 14px 38px #6f4a7410; }
.hero-copy,.hero-stats { position:relative; z-index:1; }.lottery-eyebrow,.card-kicker { color:#9d598b; font-size:.7rem; font-weight:800; letter-spacing:.16em; }.hero-orbit { display:inline-grid; width:21px; height:21px; margin-right:6px; place-items:center; color:#bb4c8d; border:1px solid #e7b8d1; border-radius:50%; animation:orbit-glow 3.8s ease-in-out infinite; }
.lottery-hero h1 { margin:8px 0 4px; font-size:clamp(1.8rem,3.8vw,2.7rem); letter-spacing:-.035em; }.lottery-hero p { margin:0; color:var(--muted); }.hero-stats { display:grid; grid-template-columns:repeat(4,minmax(70px,1fr)); gap:8px; }.hero-stats>div { display:grid; align-content:center; gap:3px; min-height:78px; padding:12px; border:1px solid #ffffffd9; border-radius:14px; background:#ffffffa8; backdrop-filter:blur(5px); }.hero-stats span { color:var(--muted); font-size:.78rem; }.hero-stats strong { color:#49385d; font-size:1.65rem; line-height:1; }.hero-spark { position:absolute; color:#d896bf; animation:float-sparkle 5s ease-in-out infinite; }.spark-one { top:18px; right:23px; font-size:1.4rem; }.spark-two { right:39%; bottom:20px; font-size:1.1rem; animation-delay:-2s; }
.lottery-column { display:grid; gap:16px; }.lottery-card { padding:20px; border:1px solid var(--line); border-radius:18px; background:#fff; box-shadow:0 8px 24px #35264c09; }.card-heading,.rates-heading,.list-heading { display:flex; align-items:center; justify-content:space-between; gap:14px; }.card-heading h2 { margin:3px 0 0; font-size:1.2rem; letter-spacing:-.015em; }.card-description { margin:9px 0 15px; color:var(--muted); font-size:.9rem; }.connection-pill { display:inline-flex; align-items:center; gap:7px; padding:6px 10px; border-radius:999px; font-size:.78rem; white-space:nowrap; }.connection-pill>span { width:7px; height:7px; border-radius:50%; background:currentColor; }.is-ready { color:#487d61; background:#eef8f0; }.is-muted { color:#777387; background:#f2f0f5; }
.field-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; }.field { display:grid; gap:6px; color:#555269; font-size:.83rem; font-weight:700; }.full-field { grid-column:1/-1; }.field input,.field textarea,.field select,.list-tools select,.rate-item input { box-sizing:border-box; width:100%; min-height:42px; padding:9px 11px; color:var(--ink); font:inherit; font-size:.9rem; font-weight:400; border:1px solid #ded9e7; border-radius:9px; outline:none; background:#fff; transition:border-color .18s,box-shadow .18s; }.field textarea { resize:vertical; }.field :focus,.list-tools select:focus,.rate-item input:focus { border-color:#bb72a7; box-shadow:0 0 0 3px #d992bd2b; }.field :disabled { color:#89869a; background:#f7f6f9; }.cooldown-field { max-width:220px; }.card-actions { display:flex; align-items:center; flex-wrap:wrap; gap:12px; margin-top:14px; }.chat-status { color:var(--muted); font-size:.86rem; }.chat-message-list { display:grid; gap:6px; margin:14px 0 0; padding:0; list-style:none; }.chat-message-list li { display:flex; gap:9px; align-items:baseline; padding:8px 10px; border-radius:8px; background:#faf7fb; font-size:.88rem; }.chat-message-list strong { color:#92567f; white-space:nowrap; }
.import-layout { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; margin-top:14px; }.import-panel { display:grid; align-content:start; gap:10px; padding:14px; border:1px solid #eeeaf2; border-radius:13px; background:#fdfcfe; }.import-panel h3,.rates-panel h3,.draw-list h3,.winner-list h3 { margin:0; font-size:.98rem; }.compact-actions { display:flex; flex-wrap:wrap; gap:7px; align-items:center; }.member-import-row { display:flex; align-items:end; gap:14px; margin-top:14px; padding:12px; border:1px dashed #e3d7e4; border-radius:12px; background:#fffafd; }.member-import-row .field { flex:1; }.member-import-row>span { padding-bottom:9px; color:var(--muted); font-size:.82rem; }.rates-panel { margin-top:14px; padding:14px; border:1px solid #eeeaf2; border-radius:13px; }.rates-heading { align-items:flex-start; }.rates-heading p { margin:3px 0 0; color:var(--muted); font-size:.8rem; }.rate-list { display:flex; flex-wrap:wrap; gap:8px; margin-top:12px; }.rate-item { display:grid; grid-template-columns:auto 56px auto; gap:6px; align-items:center; padding:6px 8px; border:1px solid #eeeaf2; border-radius:10px; color:#555269; font-size:.82rem; }.rate-item input { min-height:34px; padding:5px; text-align:center; }.rate-item span { color:var(--muted); font-size:.78rem; }
.draw-button { min-width:130px; min-height:48px; border-radius:14px; box-shadow:0 7px 18px #ad669122; }.draw-button span { margin-right:6px; animation:orbit-glow 2s ease-in-out infinite; }.entry-grid { display:grid; gap:10px; margin-top:15px; }.entry-form { display:flex; align-items:end; gap:10px; padding:12px; border:1px solid #eeeaf2; border-radius:12px; background:#fdfcfe; }.entry-form .field { flex:1; }.participant-entry { display:grid; grid-template-columns:1.1fr .8fr 1.1fr auto; }
.draw-columns { display:grid; grid-template-columns:minmax(200px,.72fr) minmax(0,1.5fr); gap:14px; margin-top:16px; }.draw-list,.winner-list { min-width:0; padding:14px; border:1px solid #eeeaf2; border-radius:14px; background:#fff; }.list-heading { min-height:30px; }.list-heading h3 { display:flex; align-items:center; gap:8px; }.list-heading h3 span { color:#8f6b8a; font-size:.78rem; font-weight:600; }.text-action { padding:3px 5px; color:#98658a; font:inherit; font-size:.79rem; border:0; background:none; cursor:pointer; }.text-action:hover { color:#673f67; text-decoration:underline; }.prize-list ol,.winner-list ol,.participant-list { display:grid; gap:7px; margin:10px 0 0; padding:0; list-style:none; }.prize-list li { display:grid; grid-template-columns:30px minmax(0,1fr) 28px; gap:8px; align-items:center; padding:8px 9px; border-radius:9px; background:linear-gradient(105deg,#fff9f2,#fff); font-size:.88rem; }.prize-number { color:#be8553; font-size:.72rem; font-weight:800; }.prize-list li button { width:26px; height:26px; color:#9d7793; font-size:1.2rem; border:0; border-radius:50%; background:transparent; cursor:pointer; }.prize-list li button:hover { background:#f9edf4; }
.list-tools { display:flex; flex-wrap:wrap; align-items:center; justify-content:end; gap:7px; }.list-tools select { width:auto; min-height:32px; padding:4px 8px; font-size:.8rem; }.participant-list li { display:flex; gap:9px; align-items:center; padding:8px; border:1px solid #f1edf4; border-radius:10px; transition:transform .2s,border-color .2s; }.participant-list li:hover { transform:translateY(-1px); border-color:#e6d5e5; }.participant-avatar { display:grid; flex:0 0 34px; width:34px; height:34px; place-items:center; color:#87537f; border-radius:50%; background:linear-gradient(145deg,#fbe4f1,#ede5ff); font-weight:800; }.participant-details { display:grid; flex:1; min-width:0; gap:3px; }.participant-details>strong { overflow:hidden; font-size:.86rem; text-overflow:ellipsis; white-space:nowrap; }.participant-list button { padding:5px 8px; color:#896b87; font-size:.76rem; border:1px solid #eee5ef; border-radius:7px; background:#fff; cursor:pointer; }.participant-list .is-winner { opacity:.64; }.tag-list { display:flex; flex-wrap:wrap; gap:4px; }.tag-list i { padding:1px 6px; color:#776b91; font-size:.7rem; font-style:normal; border-radius:99px; background:#f3f0f9; }.winner-mark { color:#9b6a34; font-size:.74rem; white-space:nowrap; }
.winner-list { margin-top:14px; border-color:#f2e8ed; background:linear-gradient(135deg,#fffdf7,#fffafd); }.winner-list li { display:flex; gap:10px; align-items:center; padding:10px; border:1px solid #f2e8ed; border-radius:11px; background:#ffffffc9; animation:winner-reveal .38s ease-out both; }.winner-star { color:#d5a052; font-size:1.2rem; }.winner-list li div { display:flex; flex:1; flex-wrap:wrap; justify-content:space-between; gap:8px; font-size:.88rem; }.winner-list li strong { color:#9e5783; }.empty-copy { margin:10px 0 2px; color:#9692a4; font-size:.84rem; }.lottery-alert { display:flex; justify-content:space-between; gap:12px; align-items:center; padding:10px 13px; border-radius:10px; font-size:.9rem; }.lottery-alert button { color:inherit; border:0; background:none; cursor:pointer; }.lottery-alert.error { color:#8f344a; background:#fff0f1; }.lottery-alert.notice { color:#546e60; background:#eff8f1; }
@keyframes float-sparkle { 0%,100% { transform:translateY(0) rotate(0); opacity:.58; } 50% { transform:translateY(-8px) rotate(18deg); opacity:1; } } @keyframes orbit-glow { 0%,100% { transform:rotate(0) scale(1); opacity:.82; } 50% { transform:rotate(180deg) scale(1.13); opacity:1; } } @keyframes winner-reveal { from { transform:translateY(5px); opacity:0; } to { transform:translateY(0); opacity:1; } }
@media (prefers-reduced-motion:reduce) { *,*::before,*::after { animation-duration:.01ms!important; animation-iteration-count:1!important; scroll-behavior:auto!important; transition-duration:.01ms!important; } }
@media (max-width:960px) { .lottery-hero { grid-template-columns:1fr; }.hero-stats { max-width:640px; width:100%; }.draw-columns { grid-template-columns:1fr; }.participant-entry { grid-template-columns:repeat(2,minmax(0,1fr)); } }
@media (max-width:620px) { .lottery-hero { padding:20px; }.hero-stats { grid-template-columns:repeat(2,minmax(0,1fr)); }.lottery-card { padding:15px; }.field-grid,.import-layout { grid-template-columns:1fr; }.full-field { grid-column:auto; }.member-import-row,.rates-heading,.entry-form { align-items:stretch; flex-direction:column; }.member-import-row>span { padding:0; }.participant-entry { display:grid; grid-template-columns:1fr; }.list-heading { align-items:flex-start; }.list-tools { justify-content:start; }.participant-list li { gap:7px; }.card-heading { align-items:flex-start; flex-wrap:wrap; } }
</style>

<style scoped>
.youtube-setup-hint { display: flex; align-items: center; justify-content: space-between; gap: 0.85rem; margin: -0.25rem 0 1rem; padding: 0.85rem 1rem; border: 1px solid var(--creator-border); border-radius: 0.85rem; background: #f7f5fc; }
.youtube-setup-hint p { margin: 0; color: var(--creator-ink); font-size: 0.9rem; }
.pending-draw { align-items: flex-start; flex-direction: column; border: 1px solid #f0c36d; color: #6f4a09; background: #fff9e8; }
.pending-draw > div:first-child { display: grid; gap: 4px; }
.pending-draw-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.history-link { margin-top: 12px; }
@media (max-width: 620px) { .youtube-setup-hint { align-items: stretch; flex-direction: column; } }
</style>
