import { normalizeInteractionSource, type Participant, type ParticipantInteractionSource } from "../lottery/draw";

export type CoreLotteryEligibilityStatus = "eligible" | "already-won";

export interface CoreLotteryDrawParticipantSnapshot {
  participantId: string;
  lotteryGroupId: string;
  displayName: string;
  externalIdentity?: { platform: string; accountId: string };
  /** Admin-only interaction provenance; absent for manual entries and legacy history. */
  interactionSource?: ParticipantInteractionSource;
  level: string;
  group: string;
  tags: string[];
  eligibilityStatus: CoreLotteryEligibilityStatus;
}

export interface CoreLotteryDrawRuleSnapshot {
  algorithmVersion: "secure-random-index-v1";
  eligibilityPolicyVersion: "not-previously-won-v1";
  groupWinnerPolicyVersion: "exclude-lottery-group-after-win-v1";
  eligibleEntryCount: number;
  prizeSelectionPolicyVersion: "first-remaining-prize-v1";
  prizeQueue: string[];
}

/** Locked participant-list version; absent for unlocked draws and legacy history. */
export interface CoreLotteryDrawPoolSnapshotVersion {
  lockHash: string;
  lockedAtUtc: string;
}

export interface CoreLotteryDrawPayload {
  drawId: string;
  winnerParticipantId: string;
  ruleSnapshot: CoreLotteryDrawRuleSnapshot;
  participants: CoreLotteryDrawParticipantSnapshot[];
  poolSnapshotVersion?: CoreLotteryDrawPoolSnapshotVersion;
}

export interface CoreLotteryDrawPreparedState {
  phase: "prepared";
  drawId: string;
  ruleSnapshot: CoreLotteryDrawRuleSnapshot;
  participants: CoreLotteryDrawParticipantSnapshot[];
  poolSnapshotVersion?: CoreLotteryDrawPoolSnapshotVersion;
  /** Missing on legacy entries written before the selected outcome was persisted. */
  selectionState?: "ready" | "selected";
  winnerParticipantId?: string;
}

export type PendingCoreLotteryDrawState = CoreLotteryDrawPreparedState | (CoreLotteryDrawPayload & { phase: "result" });

export interface CoreLotteryDrawRecord extends Omit<CoreLotteryDrawPayload, "poolSnapshotVersion"> {
  drawnAtUtc: string;
  winnerDisplayName: string;
  prizeName: string;
  /** The API returns null for unlocked and legacy draws. */
  poolSnapshotVersion?: CoreLotteryDrawPoolSnapshotVersion | null;
}

export interface CoreLotteryDrawSummary {
  drawId: string;
  drawnAtUtc: string;
  winnerParticipantId: string;
  winnerDisplayName: string;
  prizeName: string;
  participantCount: number;
  eligibleEntryCount: number;
  ruleSnapshot?: CoreLotteryDrawRuleSnapshot | null;
  poolSnapshotVersion?: CoreLotteryDrawPoolSnapshotVersion | null;
}

export interface CoreLotteryDrawHistoryPage {
  items: CoreLotteryDrawSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CoreLotteryDrawParticipantPage {
  items: CoreLotteryDrawParticipantSnapshot[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CoreLotteryDrawFilters {
  beforeDate: string;
  fromDate: string;
  page: number;
  pageSize: number;
  search: string;
}

export const pendingCoreLotteryDrawStorageKey = "easy-lottery.pending-core-draw-id";

export function buildCoreLotteryDrawHistoryQuery(filters: CoreLotteryDrawFilters): string {
  const params = new URLSearchParams();
  params.set("page", String(Math.max(1, Math.trunc(filters.page))));
  params.set("pageSize", String(Math.max(1, Math.min(100, Math.trunc(filters.pageSize)))));
  if (filters.search.trim()) params.set("search", filters.search.trim());
  const fromUtc = localDateBoundaryUtc(filters.fromDate, 0);
  const beforeUtc = localDateBoundaryUtc(filters.beforeDate, 1);
  if (fromUtc) params.set("fromUtc", fromUtc);
  if (beforeUtc) params.set("beforeUtc", beforeUtc);
  return params.toString();
}

export function createCoreLotteryDrawId(randomUuid: () => string = () => globalThis.crypto.randomUUID()): string {
  const drawId = randomUuid();
  if (!isUuid(drawId)) throw new Error("無法建立穩定的抽獎識別碼，請使用支援安全亂數的瀏覽器。");
  return drawId;
}

export function createCoreLotteryDrawPayload(
  drawId: string,
  participants: readonly Participant[],
  prizes: readonly string[],
  winnerParticipantId: string,
  poolSnapshotVersion?: CoreLotteryDrawPoolSnapshotVersion
): CoreLotteryDrawPayload {
  const participantSnapshots = participants.map(toParticipantSnapshot);
  const eligibleEntryCount = participantSnapshots.filter(participant => participant.eligibilityStatus === "eligible").length;
  const payload: CoreLotteryDrawPayload = {
    drawId,
    winnerParticipantId,
    ruleSnapshot: {
      algorithmVersion: "secure-random-index-v1",
      eligibilityPolicyVersion: "not-previously-won-v1",
      groupWinnerPolicyVersion: "exclude-lottery-group-after-win-v1",
      eligibleEntryCount,
      prizeSelectionPolicyVersion: "first-remaining-prize-v1",
      prizeQueue: prizes.map(prize => prize.trim())
    },
    participants: participantSnapshots,
    ...(poolSnapshotVersion ? { poolSnapshotVersion } : {})
  };
  return validateAndNormalizePending(payload);
}

export function createCoreLotteryDrawPreparedState(
  drawId: string,
  participants: readonly Participant[],
  prizes: readonly string[],
  winnerParticipantId?: string,
  poolSnapshotVersion?: CoreLotteryDrawPoolSnapshotVersion
): CoreLotteryDrawPreparedState {
  const eligibleParticipant = participants.find(participant => !participant.isWinner);
  if (!eligibleParticipant) throw new Error("目前沒有尚未得獎的參加者。");
  const payload = createCoreLotteryDrawPayload(drawId, participants, prizes, winnerParticipantId ?? eligibleParticipant.id, poolSnapshotVersion);
  return {
    phase: "prepared",
    drawId: payload.drawId,
    ruleSnapshot: payload.ruleSnapshot,
    participants: payload.participants,
    ...(payload.poolSnapshotVersion ? { poolSnapshotVersion: payload.poolSnapshotVersion } : {}),
    ...(winnerParticipantId
      ? { selectionState: "selected" as const, winnerParticipantId: payload.winnerParticipantId }
      : { selectionState: "ready" as const })
  };
}

export function createCoreLotteryDrawPayloadFromPreparedState(
  prepared: CoreLotteryDrawPreparedState
): CoreLotteryDrawPayload {
  const normalized = validateAndNormalizePrepared(prepared);
  if (normalized.selectionState !== "selected" || !normalized.winnerParticipantId)
    throw new Error("抽獎保護快照尚未保存選定的得獎籤。");
  return validateAndNormalizePending({
    drawId: normalized.drawId,
    winnerParticipantId: normalized.winnerParticipantId,
    ruleSnapshot: normalized.ruleSnapshot,
    participants: normalized.participants,
    ...(normalized.poolSnapshotVersion ? { poolSnapshotVersion: normalized.poolSnapshotVersion } : {})
  });
}

export function storePendingCoreLotteryDrawId(storage: Storage, pending: CoreLotteryDrawPayload): void {
  storage.setItem(pendingCoreLotteryDrawStorageKey, JSON.stringify({
    phase: "result",
    ...validateAndNormalizePending(pending)
  }));
}

export function storePendingCoreLotteryDrawPreparedState(storage: Storage, pending: CoreLotteryDrawPreparedState): void {
  storage.setItem(pendingCoreLotteryDrawStorageKey, JSON.stringify(validateAndNormalizePrepared(pending)));
}

export function preflightPendingCoreLotteryDrawStorage(
  storage: Storage,
  pending: CoreLotteryDrawPayload
): void {
  const probeKey = `${pendingCoreLotteryDrawStorageKey}.probe.${pending.drawId}`;
  try {
    storage.setItem(probeKey, JSON.stringify(validateAndNormalizePending(pending)));
  } finally {
    storage.removeItem(probeKey);
  }
}

export function readPendingCoreLotteryDrawId(storage: Storage): PendingCoreLotteryDrawState | null {
  const serialized = storage.getItem(pendingCoreLotteryDrawStorageKey);
  if (serialized === null) return null;
  let pending: unknown;
  try { pending = JSON.parse(serialized); }
  catch { throw new Error("待對帳的抽獎結果資料格式無效。"); }
  if (pending && typeof pending === "object" && !Array.isArray(pending)
    && (pending as Record<string, unknown>).phase === "prepared") return validateAndNormalizePrepared(pending);
  const record = pending && typeof pending === "object" ? pending as Record<string, unknown> : {};
  return { phase: "result", ...validateAndNormalizePending(record) };
}

export function clearPendingCoreLotteryDrawId(storage: Storage, drawId: string): void {
  const pending = readPendingCoreLotteryDrawId(storage);
  if (pending?.drawId === drawId) storage.removeItem(pendingCoreLotteryDrawStorageKey);
}

export function matchesPendingCoreLotteryDraw(
  record: CoreLotteryDrawRecord,
  expected: CoreLotteryDrawPayload
): boolean {
  if (!isUuid(record.drawId)) return false;
  try {
    const normalizedExpected = validateAndNormalizePending(expected);
    if (record.drawId.toLowerCase() !== normalizedExpected.drawId.toLowerCase()) return false;
    const savedPayload = validateAndNormalizePending({
      drawId: record.drawId,
      winnerParticipantId: record.winnerParticipantId,
      ruleSnapshot: record.ruleSnapshot,
      participants: record.participants,
      poolSnapshotVersion: record.poolSnapshotVersion
    });
    return JSON.stringify(savedPayload) === JSON.stringify(normalizedExpected)
      && record.winnerDisplayName === winnerForPayload(normalizedExpected).displayName
      && record.prizeName === normalizedExpected.ruleSnapshot.prizeQueue[0];
  } catch {
    return false;
  }
}

export function matchesPreparedCoreLotteryDraw(record: CoreLotteryDrawRecord, expected: CoreLotteryDrawPreparedState): boolean {
  if (!isUuid(record.drawId) || record.drawId.toLowerCase() !== expected.drawId.toLowerCase()) return false;
  if (expected.selectionState === "ready") return false;
  if (expected.winnerParticipantId && expected.winnerParticipantId !== record.winnerParticipantId) return false;
  try {
    const actual = validateAndNormalizePrepared({
      phase: "prepared",
      drawId: record.drawId,
      ...(expected.selectionState ? { selectionState: expected.selectionState } : {}),
      ...(expected.winnerParticipantId ? { winnerParticipantId: record.winnerParticipantId } : {}),
      ruleSnapshot: record.ruleSnapshot,
      participants: record.participants,
      poolSnapshotVersion: record.poolSnapshotVersion
    });
    return JSON.stringify(actual) === JSON.stringify(validateAndNormalizePrepared(expected))
      && record.winnerParticipantId === record.participants.find(participant => participant.participantId === record.winnerParticipantId)?.participantId
      && record.winnerDisplayName === record.participants.find(participant => participant.participantId === record.winnerParticipantId)?.displayName
      && record.prizeName === expected.ruleSnapshot.prizeQueue[0];
  } catch {
    return false;
  }
}

export function validateAndNormalizePrepared(value: unknown): CoreLotteryDrawPreparedState {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("待對帳的抽獎意圖格式無效。");
  const candidate = value as Record<string, unknown>;
  if (typeof candidate.drawId !== "string" || !isUuid(candidate.drawId)
    || !Array.isArray(candidate.participants) || !candidate.ruleSnapshot || typeof candidate.ruleSnapshot !== "object")
    throw new Error("待對帳的抽獎意圖格式無效。");
  const participants = candidate.participants as CoreLotteryDrawParticipantSnapshot[];
  const eligible = participants.find(participant => participant?.eligibilityStatus === "eligible");
  if (!eligible) throw new Error("待對帳的抽獎意圖沒有合格參與籤。");
  if (candidate.selectionState !== undefined && candidate.selectionState !== "ready" && candidate.selectionState !== "selected")
    throw new Error("待對帳的抽獎意圖狀態無效。");
  if (candidate.selectionState === "selected" && (typeof candidate.winnerParticipantId !== "string" || !candidate.winnerParticipantId.trim()))
    throw new Error("已選定的抽獎意圖缺少得獎籤識別碼。");
  if (candidate.selectionState === "ready" && candidate.winnerParticipantId !== undefined)
    throw new Error("尚未抽選的意圖不應包含得獎籤識別碼。");
  if (candidate.winnerParticipantId !== undefined && typeof candidate.winnerParticipantId !== "string")
    throw new Error("待對帳的得獎籤識別碼格式無效。");
  const payload = validateAndNormalizePending({
    drawId: candidate.drawId,
    winnerParticipantId: candidate.winnerParticipantId ?? eligible.participantId,
    ruleSnapshot: candidate.ruleSnapshot,
    participants: candidate.participants,
    poolSnapshotVersion: candidate.poolSnapshotVersion
  });
  return {
    phase: "prepared",
    drawId: payload.drawId,
    ruleSnapshot: payload.ruleSnapshot,
    participants: payload.participants,
    ...(payload.poolSnapshotVersion ? { poolSnapshotVersion: payload.poolSnapshotVersion } : {}),
    ...(candidate.selectionState ? { selectionState: candidate.selectionState } : {}),
    ...(candidate.winnerParticipantId ? { winnerParticipantId: payload.winnerParticipantId } : {})
  };
}

export function validateAndNormalizePending(value: unknown): CoreLotteryDrawPayload {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("待對帳的抽獎結果資料格式無效。");
  const candidate = value as Record<string, unknown>;
  if (typeof candidate.drawId !== "string" || !isUuid(candidate.drawId)
    || typeof candidate.winnerParticipantId !== "string" || !candidate.winnerParticipantId.trim()
    || !Array.isArray(candidate.participants) || !candidate.ruleSnapshot || typeof candidate.ruleSnapshot !== "object")
    throw new Error("待對帳的抽獎結果資料格式無效。");

  const rawRules = candidate.ruleSnapshot as Record<string, unknown>;
  if (rawRules.algorithmVersion !== "secure-random-index-v1"
    || rawRules.eligibilityPolicyVersion !== "not-previously-won-v1"
    || rawRules.groupWinnerPolicyVersion !== "exclude-lottery-group-after-win-v1"
    || rawRules.prizeSelectionPolicyVersion !== "first-remaining-prize-v1"
    || !Number.isSafeInteger(rawRules.eligibleEntryCount)
    || !Array.isArray(rawRules.prizeQueue) || rawRules.prizeQueue.length === 0
    || rawRules.prizeQueue.some(prize => typeof prize !== "string" || !prize.trim()))
    throw new Error("抽獎規則快照無效。");

  const seenIds = new Set<string>();
  const participants = candidate.participants.map((value): CoreLotteryDrawParticipantSnapshot => {
    if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("抽獎名單快照格式無效。");
    const row = value as Record<string, unknown>;
    if (typeof row.participantId !== "string" || !row.participantId.trim() || seenIds.has(row.participantId)
      || typeof row.lotteryGroupId !== "string" || !row.lotteryGroupId.trim()
      || typeof row.displayName !== "string" || !row.displayName.trim()
      || typeof row.level !== "string" || typeof row.group !== "string"
      || !Array.isArray(row.tags) || row.tags.some(tag => typeof tag !== "string")
      || (row.eligibilityStatus !== "eligible" && row.eligibilityStatus !== "already-won"))
      throw new Error("抽獎名單快照包含無效資料。");
    seenIds.add(row.participantId);

    let externalIdentity: CoreLotteryDrawParticipantSnapshot["externalIdentity"];
    if (row.externalIdentity !== undefined && row.externalIdentity !== null) {
      if (typeof row.externalIdentity !== "object" || Array.isArray(row.externalIdentity)) throw new Error("外部帳號識別格式無效。");
      const identity = row.externalIdentity as Record<string, unknown>;
      if (typeof identity.platform !== "string" || !identity.platform.trim()
        || typeof identity.accountId !== "string" || !identity.accountId.trim())
        throw new Error("外部帳號識別格式無效。");
      externalIdentity = { platform: identity.platform.trim().toLowerCase(), accountId: identity.accountId.trim() };
    }

    let interactionSource: ParticipantInteractionSource | undefined;
    if (row.interactionSource !== undefined && row.interactionSource !== null) {
      interactionSource = normalizeInteractionSource(row.interactionSource);
      if (!interactionSource) throw new Error("互動來源識別格式無效。");
    }

    return {
      participantId: row.participantId,
      lotteryGroupId: row.lotteryGroupId,
      displayName: row.displayName,
      ...(externalIdentity ? { externalIdentity } : {}),
      ...(interactionSource ? { interactionSource } : {}),
      level: row.level,
      group: row.group,
      tags: [...row.tags] as string[],
      eligibilityStatus: row.eligibilityStatus
    };
  });
  const eligibleEntryCount = participants.filter(participant => participant.eligibilityStatus === "eligible").length;
  if (eligibleEntryCount < 1 || eligibleEntryCount !== rawRules.eligibleEntryCount)
    throw new Error("合格抽獎籤數與完整名單快照不一致。");
  const ruleSnapshot: CoreLotteryDrawRuleSnapshot = {
    algorithmVersion: "secure-random-index-v1",
    eligibilityPolicyVersion: "not-previously-won-v1",
    groupWinnerPolicyVersion: "exclude-lottery-group-after-win-v1",
    eligibleEntryCount,
    prizeSelectionPolicyVersion: "first-remaining-prize-v1",
    prizeQueue: (rawRules.prizeQueue as string[]).map(prize => prize.trim())
  };
  const poolSnapshotVersion = normalizePoolSnapshotVersion(candidate.poolSnapshotVersion);
  const normalized: CoreLotteryDrawPayload = {
    drawId: candidate.drawId,
    winnerParticipantId: candidate.winnerParticipantId.trim(),
    ruleSnapshot,
    participants,
    ...(poolSnapshotVersion ? { poolSnapshotVersion } : {})
  };
  const winner = winnerForPayload(normalized);
  if (!winner || winner.eligibilityStatus !== "eligible") throw new Error("得獎籤不在本次合格參與名單中。");
  return normalized;
}

/** null/undefined mean "not locked"; anything else must be a valid lock hash plus timestamp (normalized to ISO UTC). */
function normalizePoolSnapshotVersion(value: unknown): CoreLotteryDrawPoolSnapshotVersion | undefined {
  if (value === undefined || value === null) return undefined;
  if (typeof value !== "object" || Array.isArray(value)) throw new Error("鎖定名單版本格式無效。");
  const candidate = value as Record<string, unknown>;
  const lockedAt = typeof candidate.lockedAtUtc === "string" ? Date.parse(candidate.lockedAtUtc) : Number.NaN;
  if (typeof candidate.lockHash !== "string" || !/^[\da-f]{16}$/i.test(candidate.lockHash) || !Number.isFinite(lockedAt))
    throw new Error("鎖定名單版本格式無效。");
  return { lockHash: candidate.lockHash.toLowerCase(), lockedAtUtc: new Date(lockedAt).toISOString() };
}

function toParticipantSnapshot(participant: Participant): CoreLotteryDrawParticipantSnapshot {
  return {
    participantId: participant.id,
    lotteryGroupId: participant.lotteryGroupId,
    displayName: participant.name,
    ...(participant.identity ? {
      externalIdentity: { platform: participant.identity.platform, accountId: participant.identity.accountId }
    } : {}),
    ...(participant.source ? {
      interactionSource: {
        roundId: participant.source.roundId,
        snapshotId: participant.source.snapshotId,
        audienceProfileId: participant.source.audienceProfileId
      }
    } : {}),
    level: participant.level,
    group: participant.group,
    tags: [...participant.tags],
    eligibilityStatus: participant.isWinner ? "already-won" : "eligible"
  };
}

function winnerForPayload(payload: CoreLotteryDrawPayload): CoreLotteryDrawParticipantSnapshot {
  const winner = payload.participants.find(participant => participant.participantId === payload.winnerParticipantId);
  if (!winner) throw new Error("抽獎名單中找不到得獎籤。");
  return winner;
}

function isUuid(value: string): boolean {
  return /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(value);
}

function localDateBoundaryUtc(dateValue: string, dayOffset: number): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateValue);
  if (!match) return null;
  const [, year, month, day] = match;
  const date = new Date(Number(year), Number(month) - 1, Number(day));
  if (date.getFullYear() !== Number(year) || date.getMonth() !== Number(month) - 1 || date.getDate() !== Number(day)) return null;
  date.setDate(date.getDate() + dayOffset);
  return date.toISOString();
}
