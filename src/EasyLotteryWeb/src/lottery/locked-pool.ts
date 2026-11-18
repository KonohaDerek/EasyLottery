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
