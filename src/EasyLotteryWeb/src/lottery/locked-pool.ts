import {
  normalizeInteractionSource,
  normalizeParticipantIdentity,
  type Participant,
  type Winner
} from "./draw";

/** Tab-scoped (sessionStorage) key; holds only roster/prize data, never session tokens or chat credentials. */
export const lockedPoolStorageKey = "easy-lottery.core-lottery.locked-pool";
export const lockedPoolSchemaVersion = 1;

export interface LockedPool {
  schemaVersion: typeof lockedPoolSchemaVersion;
  lockedAtUtc: string;
  /** Hash of the pool at lock time; stays constant while confirmed draws advance the pool. */
  lockHash: string;
  participants: Participant[];
  prizes: string[];
  winners: Winner[];
  /** Integrity hash of every other field; detects corrupted or hand-edited storage. */
  hash: string;
}

export type LockedPoolReadResult =
  | { status: "none" }
  | { status: "valid"; pool: LockedPool }
  | { status: "invalid"; reason: string };

/** Deterministic 64-bit FNV-1a (two 32-bit lanes). Integrity check only, not a security boundary. */
export function hashText(value: string): string {
  let lowHash = 0x811c9dc5;
  let highHash = 0xcbf29ce4;
  for (let index = 0; index < value.length; index++) {
    const code = value.charCodeAt(index);
    lowHash = Math.imul(lowHash ^ (code & 0xff), 0x01000193) >>> 0;
    highHash = Math.imul(highHash ^ (code >>> 8) ^ lowHash, 0x01000193) >>> 0;
  }
  return `${highHash.toString(16).padStart(8, "0")}${lowHash.toString(16).padStart(8, "0")}`;
}

function canonicalParticipant(participant: Participant): Participant {
  return {
    id: participant.id,
    lotteryGroupId: participant.lotteryGroupId,
    name: participant.name,
    // channelScope is added only when present so pools locked before it existed keep their integrity hash.
    ...(participant.identity ? {
      identity: {
        platform: participant.identity.platform,
        accountId: participant.identity.accountId,
        ...(participant.identity.channelScope ? { channelScope: participant.identity.channelScope } : {})
      }
    } : {}),
    ...(participant.source ? {
      source: {
        roundId: participant.source.roundId,
        snapshotId: participant.source.snapshotId,
        audienceProfileId: participant.source.audienceProfileId
      }
    } : {}),
    level: participant.level,
    group: participant.group,
    tags: [...participant.tags],
    isWinner: participant.isWinner
  };
}

function canonicalWinner(winner: Winner): Winner {
  return { member: canonicalParticipant(winner.member), prize: winner.prize, ...(winner.drawId ? { drawId: winner.drawId } : {}) };
}

export function poolHash(participants: readonly Participant[], prizes: readonly string[]): string {
  return hashText(JSON.stringify({ participants: participants.map(canonicalParticipant), prizes: [...prizes] }));
}

function contentHash(pool: Omit<LockedPool, "hash">): string {
  return hashText(JSON.stringify({
    schemaVersion: pool.schemaVersion,
    lockedAtUtc: pool.lockedAtUtc,
    lockHash: pool.lockHash,
    participants: pool.participants.map(canonicalParticipant),
    prizes: [...pool.prizes],
    winners: pool.winners.map(canonicalWinner)
  }));
}

function seal(pool: Omit<LockedPool, "hash">): LockedPool {
  const body: Omit<LockedPool, "hash"> = {
    schemaVersion: lockedPoolSchemaVersion,
    lockedAtUtc: pool.lockedAtUtc,
    lockHash: pool.lockHash,
    participants: pool.participants.map(canonicalParticipant),
    prizes: [...pool.prizes],
    winners: pool.winners.map(canonicalWinner)
  };
  return { ...body, hash: contentHash(body) };
}

export function createLockedPool(
  participants: readonly Participant[],
  prizes: readonly string[],
  winners: readonly Winner[],
  now: Date = new Date()
): LockedPool {
  if (!participants.length) throw new Error("目前沒有參加者，無法鎖定抽獎名單。");
  validateParticipants(participants);
  return seal({
    schemaVersion: lockedPoolSchemaVersion,
    lockedAtUtc: now.toISOString(),
    lockHash: poolHash(participants, prizes),
    participants: [...participants],
    prizes: [...prizes],
    winners: [...winners]
  });
}

/** Records a confirmed draw outcome; lock time and lock hash are preserved. */
export function advanceLockedPool(
  pool: LockedPool,
  participants: readonly Participant[],
  prizes: readonly string[],
  winners: readonly Winner[]
): LockedPool {
  validateParticipants(participants);
  return seal({ ...pool, participants: [...participants], prizes: [...prizes], winners: [...winners] });
}

/** The participant-list version a draw from this lock records in history; stable across confirmed draws. */
export function lockedPoolVersion(pool: Pick<LockedPool, "lockHash" | "lockedAtUtc">): { lockHash: string; lockedAtUtc: string } {
  return { lockHash: pool.lockHash, lockedAtUtc: pool.lockedAtUtc };
}

/**
 * Browser-profile (localStorage) ledger of every saved drawId reconciled for one locked-pool version, shared by all
 * tabs. A tab whose restored lock misses any of these draws is a stale copy (restored/duplicated tab) and must not draw.
 */
export const resolvedLockDrawsStorageKeyPrefix = "easy-lottery.core-lottery.resolved-lock-draws.";
export const resolvedLockDrawsSchemaVersion = 1;

export type LockedPoolVersion = ReturnType<typeof lockedPoolVersion>;

export type ResolvedLockDrawsReadResult =
  | { status: "none" }
  | { status: "valid"; drawIds: string[] }
  | { status: "invalid"; reason: string };

function normalizeLockVersion(version: LockedPoolVersion): LockedPoolVersion {
  const lockedAt = Date.parse(version.lockedAtUtc);
  if (!/^[\da-f]{16}$/i.test(version.lockHash) || !Number.isFinite(lockedAt)) throw new Error("鎖定名單版本無效。");
  return { lockHash: version.lockHash.toLowerCase(), lockedAtUtc: new Date(lockedAt).toISOString() };
}

export function resolvedLockDrawsStorageKey(version: LockedPoolVersion): string {
  const normalized = normalizeLockVersion(version);
  return `${resolvedLockDrawsStorageKeyPrefix}${normalized.lockHash}.${normalized.lockedAtUtc}`;
}

export function readResolvedLockDraws(storage: Storage, version: LockedPoolVersion): ResolvedLockDrawsReadResult {
  const normalized = normalizeLockVersion(version);
  const serialized = storage.getItem(resolvedLockDrawsStorageKey(normalized));
  if (serialized === null) return { status: "none" };
  let parsed: unknown;
  try { parsed = JSON.parse(serialized); }
  catch { return { status: "invalid", reason: "已對帳抽獎清單不是有效的 JSON。" }; }
  const candidate = parsed && typeof parsed === "object" && !Array.isArray(parsed) ? parsed as Record<string, unknown> : null;
  if (!candidate || candidate.schemaVersion !== resolvedLockDrawsSchemaVersion
    || candidate.lockHash !== normalized.lockHash || candidate.lockedAtUtc !== normalized.lockedAtUtc
    || !Array.isArray(candidate.drawIds) || !candidate.drawIds.length
    || candidate.drawIds.some(drawId => typeof drawId !== "string" || !drawId.trim())
    || new Set(candidate.drawIds.map(drawId => (drawId as string).toLowerCase())).size !== candidate.drawIds.length)
    return { status: "invalid", reason: "已對帳抽獎清單格式無效。" };
  return { status: "valid", drawIds: [...candidate.drawIds as string[]] };
}

/**
 * The stored ledger exists but is unreadable. Unlike a storage API failure this does not clear up on its own: automatic
 * reconciliation for the lock stays blocked. No self-service repair exists; the UI directs the operator to preserve the
 * browser state and contact the system maintainer.
 */
export class ResolvedLockDrawsLedgerCorruptError extends Error {
  readonly reason: string;
  readonly storageKey: string;

  constructor(reason: string, storageKey: string) {
    super(reason);
    this.name = "ResolvedLockDrawsLedgerCorruptError";
    this.reason = reason;
    this.storageKey = storageKey;
  }
}

/**
 * Idempotently records a reconciled drawId for this lock version; throws (writing nothing) on storage or ledger failure.
 * An existing but invalid ledger throws ResolvedLockDrawsLedgerCorruptError; storage API failures propagate unchanged.
 */
export function appendResolvedLockDraw(storage: Storage, version: LockedPoolVersion, drawId: string): void {
  if (!drawId.trim()) throw new Error("抽獎識別碼無效。");
  const normalized = normalizeLockVersion(version);
  const current = readResolvedLockDraws(storage, normalized);
  // Never overwrite an unreadable ledger: that would forget earlier draws and re-open a stale tab.
  if (current.status === "invalid") throw new ResolvedLockDrawsLedgerCorruptError(current.reason, resolvedLockDrawsStorageKey(normalized));
  const drawIds = current.status === "valid" ? current.drawIds : [];
  if (drawIds.some(existing => existing.toLowerCase() === drawId.toLowerCase())) return;
  storage.setItem(resolvedLockDrawsStorageKey(normalized), JSON.stringify({
    schemaVersion: resolvedLockDrawsSchemaVersion,
    ...normalized,
    drawIds: [...drawIds, drawId]
  }));
}

/** Ledger drawIds this pool's own winners do not contain; non-empty means the pool is a stale copy of its lock. */
export function missingResolvedLockDraws(pool: LockedPool, resolvedDrawIds: readonly string[]): string[] {
  const own = new Set(pool.winners.flatMap(winner => winner.drawId ? [winner.drawId.toLowerCase()] : []));
  return resolvedDrawIds.filter(drawId => !own.has(drawId.toLowerCase()));
}

/**
 * Single same-origin Web Lock serializing every draw, reconciliation and locked-pool unlock/clear across tabs. It is held
 * from the pre-draw checks through API reconciliation, so a second tab can never start a draw or overwrite the shared
 * pending-draw record while another tab's draw is unresolved in flight.
 */
export const coreLotteryOriginLockName = "easy-lottery.core-lottery.origin-draw";

export type OriginDrawLockResult =
  | { status: "ran" }
  | { status: "busy" }
  | { status: "unsupported" }
  | { status: "failed"; reason: string };

/** The browser's LockManager, or null when Web Locks are unavailable (callers must then fail closed). */
export function originLockManager(navigatorLike: { locks?: LockManager } | undefined = globalThis.navigator): LockManager | null {
  const locks = navigatorLike?.locks;
  return locks && typeof locks.request === "function" ? locks : null;
}

/**
 * Runs `action` only while holding the origin draw lock. Never waits: another holder yields "busy" without running.
 * There is deliberately no storage-lease fallback; without Web Locks the action is refused ("unsupported").
 * Errors thrown by `action` itself propagate after the lock is released.
 */
export async function runWithOriginDrawLock(locks: LockManager | null, action: () => Promise<void>): Promise<OriginDrawLockResult> {
  if (!locks) return { status: "unsupported" };
  let ran = false;
  try {
    await locks.request(coreLotteryOriginLockName, { mode: "exclusive", ifAvailable: true }, async lock => {
      if (!lock) return;
      ran = true;
      await action();
    });
  } catch (cause) {
    if (ran) throw cause;
    return { status: "failed", reason: cause instanceof Error ? cause.message : "無法取得跨分頁抽獎鎖。" };
  }
  return ran ? { status: "ran" } : { status: "busy" };
}

export function storeLockedPool(storage: Storage, pool: LockedPool): void {
  storage.setItem(lockedPoolStorageKey, JSON.stringify(pool));
}

export function clearLockedPool(storage: Storage): void {
  storage.removeItem(lockedPoolStorageKey);
}

export function readLockedPool(storage: Storage): LockedPoolReadResult {
  const serialized = storage.getItem(lockedPoolStorageKey);
  if (serialized === null) return { status: "none" };
  let parsed: unknown;
  try { parsed = JSON.parse(serialized); }
  catch { return { status: "invalid", reason: "鎖定名單資料不是有效的 JSON。" }; }
  try {
    return { status: "valid", pool: validateLockedPool(parsed) };
  } catch (cause) {
    return { status: "invalid", reason: cause instanceof Error ? cause.message : "鎖定名單資料無效。" };
  }
}

export function validateLockedPool(value: unknown): LockedPool {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("鎖定名單資料格式無效。");
  const candidate = value as Record<string, unknown>;
  if (candidate.schemaVersion !== lockedPoolSchemaVersion) throw new Error("鎖定名單版本不受支援。");
  if (typeof candidate.lockedAtUtc !== "string" || !Number.isFinite(Date.parse(candidate.lockedAtUtc))
    || typeof candidate.lockHash !== "string" || !/^[\da-f]{16}$/.test(candidate.lockHash)
    || typeof candidate.hash !== "string"
    || !Array.isArray(candidate.participants) || !candidate.participants.length
    || !Array.isArray(candidate.prizes) || candidate.prizes.some(prize => typeof prize !== "string")
    || !Array.isArray(candidate.winners))
    throw new Error("鎖定名單資料格式無效。");
  const participants = candidate.participants.map(parseParticipant);
  validateParticipants(participants);
  const winners = candidate.winners.map(parseWinner);
  const body: Omit<LockedPool, "hash"> = {
    schemaVersion: lockedPoolSchemaVersion,
    lockedAtUtc: candidate.lockedAtUtc,
    lockHash: candidate.lockHash,
    participants,
    prizes: [...candidate.prizes as string[]],
    winners
  };
  if (contentHash(body) !== candidate.hash) throw new Error("鎖定名單完整性檢查失敗。");
  return { ...body, hash: candidate.hash };
}

function parseParticipant(value: unknown): Participant {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("鎖定名單參加者格式無效。");
  const row = value as Record<string, unknown>;
  if (typeof row.id !== "string" || !row.id.trim() || typeof row.lotteryGroupId !== "string" || !row.lotteryGroupId.trim()
    || typeof row.name !== "string" || !row.name.trim() || typeof row.level !== "string" || typeof row.group !== "string"
    || !Array.isArray(row.tags) || row.tags.some(tag => typeof tag !== "string") || typeof row.isWinner !== "boolean")
    throw new Error("鎖定名單參加者格式無效。");
  const identity = row.identity === undefined ? undefined : normalizeParticipantIdentity(row.identity);
  if (row.identity !== undefined && !identity) throw new Error("鎖定名單外部帳號識別無效。");
  const source = row.source === undefined ? undefined : normalizeInteractionSource(row.source);
  if (row.source !== undefined && !source) throw new Error("鎖定名單互動來源無效。");
  return {
    id: row.id,
    lotteryGroupId: row.lotteryGroupId,
    name: row.name,
    ...(identity ? { identity } : {}),
    ...(source ? { source } : {}),
    level: row.level,
    group: row.group,
    tags: [...row.tags as string[]],
    isWinner: row.isWinner
  };
}

function parseWinner(value: unknown): Winner {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("鎖定名單得獎紀錄格式無效。");
  const row = value as Record<string, unknown>;
  if (typeof row.prize !== "string" || (row.drawId !== undefined && typeof row.drawId !== "string"))
    throw new Error("鎖定名單得獎紀錄格式無效。");
  return { member: parseParticipant(row.member), prize: row.prize, ...(typeof row.drawId === "string" ? { drawId: row.drawId } : {}) };
}

function validateParticipants(participants: readonly Participant[]): void {
  const ids = new Set<string>();
  for (const participant of participants) {
    if (ids.has(participant.id)) throw new Error("鎖定名單包含重複的抽獎籤識別碼。");
    ids.add(participant.id);
  }
}

/** Applies a server-confirmed winner to a restored pool exactly like drawLottery does (group exclusion, first prize). */
export function applyConfirmedWinner(
  participants: readonly Participant[],
  prizes: readonly string[],
  winnerParticipantId: string
): { participants: Participant[]; prizes: string[] } | null {
  const winner = participants.find(participant => participant.id === winnerParticipantId && !participant.isWinner);
  if (!winner || !prizes.length) return null;
  return {
    participants: participants.map(participant => participant.lotteryGroupId === winner.lotteryGroupId
      ? { ...participant, tags: [...participant.tags], isWinner: true }
      : { ...participant, tags: [...participant.tags] }),
    prizes: prizes.slice(1)
  };
}
