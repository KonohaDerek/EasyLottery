import {
  createId,
  createParticipant,
  isUuid,
  normalizeParticipantIdentity,
  participantIdentityKey,
  participantScopedIdentityKey,
  type Participant,
  type ParticipantIdentity,
  type ParticipantInteractionSource
} from "./draw";

export interface EligibilityEntry {
  audienceProfileId: string;
  /** Display data only; never used as an identity key. */
  displayName: string;
  identities: ParticipantIdentity[];
}

export interface EligibilityPreview {
  roundId: string;
  roundName: string;
  roundStatus: string;
  imported: boolean;
  snapshotId: string | null;
  snapshotCreatedAtUtc: string | null;
  /** Live candidate count reported by the API (may differ from an already imported snapshot). */
  liveEligibleCount: number;
  /** Exactly the profiles an import returns. */
  entries: EligibilityEntry[];
}

export type EligibilityEntryKind = "new" | "duplicate" | "linked" | "identity-conflict";

export interface EligibilityEntryClassification {
  entry: EligibilityEntry;
  kind: EligibilityEntryKind;
  /** Existing participants that already carry this profile (duplicate) or are linked to it (linked). */
  participantIds: string[];
  /**
   * identity-conflict only: existing participants sharing the platform account ID without an exact
   * platform + channelScope + accountId match (missing or different scope, or already claimed). Reported, never linked.
   */
  identityConflictIds: string[];
  /** Participants with the same display name but no confirmed identity link; reported, never merged. */
  nameCollisionIds: string[];
}

export interface EligibilityImportSummary {
  eligible: number;
  added: number;
  duplicates: number;
  linked: number;
  identityConflicts: number;
  nameCollisions: number;
}

export interface EligibilityImportResult {
  participants: Participant[];
  added: Participant[];
  classifications: EligibilityEntryClassification[];
  summary: EligibilityImportSummary;
}

/** Rounds whose eligibility may be imported. Cancelled rounds are rejected by the API as well. */
export const importableRoundStatuses: readonly string[] = ["Live", "Paused", "Settled"];

const maxDisplayNameLength = 160;

export function parseEligibilityPreview(value: unknown, expectedRoundId: string): EligibilityPreview {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("互動回合資格回應格式無效。");
  const candidate = value as Record<string, unknown>;
  if (!isUuid(candidate.roundId) || candidate.roundId.toLowerCase() !== expectedRoundId.toLowerCase())
    throw new Error("互動回合資格回應與所選回合不一致。");
  if (typeof candidate.imported !== "boolean" || !Array.isArray(candidate.entries))
    throw new Error("互動回合資格回應缺少快照資料。");
  const rawSnapshotId = candidate.snapshotId;
  if (rawSnapshotId !== null && rawSnapshotId !== undefined && !isUuid(rawSnapshotId)) throw new Error("互動回合資格快照識別碼無效。");
  const snapshotId: string | null = isUuid(rawSnapshotId) ? rawSnapshotId : null;
  if (candidate.imported && snapshotId === null) throw new Error("已匯入的資格快照缺少識別碼。");

  const seen = new Set<string>();
  const entries: EligibilityEntry[] = [];
  for (const raw of candidate.entries) {
    if (!raw || typeof raw !== "object" || Array.isArray(raw)) throw new Error("互動回合資格名單格式無效。");
    const row = raw as Record<string, unknown>;
    if (!isUuid(row.audienceProfileId)) throw new Error("互動回合資格名單包含無效的觀眾識別碼。");
    const audienceProfileId = row.audienceProfileId.toLowerCase();
    if (seen.has(audienceProfileId)) continue;
    seen.add(audienceProfileId);
    const identities: ParticipantIdentity[] = [];
    const identityKeys = new Set<string>();
    for (const identity of Array.isArray(row.identities) ? row.identities : []) {
      const normalized = normalizeParticipantIdentity(identity);
      // Same account ID under different channel scopes are different domain identities; keep both.
      const key = normalized ? JSON.stringify([normalized.platform, normalized.channelScope ?? null, normalized.accountId]) : undefined;
      if (normalized && key && !identityKeys.has(key)) {
        identityKeys.add(key);
        identities.push(normalized);
      }
    }
    entries.push({ audienceProfileId, displayName: typeof row.displayName === "string" ? row.displayName.trim() : "", identities });
  }

  return {
    roundId: candidate.roundId.toLowerCase(),
    roundName: typeof candidate.roundName === "string" ? candidate.roundName : "",
    roundStatus: typeof candidate.roundStatus === "string" ? candidate.roundStatus : "",
    imported: candidate.imported,
    snapshotId: snapshotId?.toLowerCase() ?? null,
    snapshotCreatedAtUtc: typeof candidate.snapshotCreatedAtUtc === "string" ? candidate.snapshotCreatedAtUtc : null,
    liveEligibleCount: Number.isSafeInteger(candidate.eligibleCount) ? candidate.eligibleCount as number : entries.length,
    entries
  };
}

function nameKey(name: string): string {
  return name.trim().toLocaleLowerCase();
}

/**
 * Deterministically classifies each snapshot entry against the current pool:
 * - duplicate: a participant already carries the same AudienceProfile ID (from any round).
 * - linked: unsourced manual participants whose external identity exactly matches platform + channelScope + accountId
 *   of this profile; they gain provenance, no new ticket.
 * - identity-conflict: the platform account ID matches but not exactly (manual identity or entry lacks a channel scope,
 *   or the scopes differ), or the exact identity is already claimed by an earlier entry or by an imported row of another
 *   profile (e.g. after a later merge); added as a separate entry and reported, never linked.
 * - new: added as one entry. Same display name alone never merges and is only reported.
 */
export function classifyEligibility(
  participants: readonly Participant[],
  entries: readonly EligibilityEntry[]
): EligibilityEntryClassification[] {
  const byProfile = new Map<string, string[]>();
  const manualByScopedIdentity = new Map<string, string[]>();
  const byAccount = new Map<string, string[]>();
  const byName = new Map<string, string[]>();
  const push = (map: Map<string, string[]>, key: string, id: string) => map.set(key, [...(map.get(key) ?? []), id]);
  for (const participant of participants) {
    if (participant.source) push(byProfile, participant.source.audienceProfileId.toLowerCase(), participant.id);
    else {
      const scopedKey = participantScopedIdentityKey(participant.identity);
      if (scopedKey) push(manualByScopedIdentity, scopedKey, participant.id);
    }
    // Every participant (manual or imported) with the same platform account is a potential conflict when not linked exactly.
    const accountKey = participantIdentityKey(participant.identity);
    if (accountKey) push(byAccount, accountKey, participant.id);
    const key = nameKey(participant.name);
    if (key) byName.set(key, [...(byName.get(key) ?? []), participant.id]);
  }

  const claimed = new Set<string>();
  const batchNames = new Map<string, string[]>();
  return entries.map(entry => {
    const profile = entry.audienceProfileId.toLowerCase();
    const existing = byProfile.get(profile);
    if (existing?.length) return { entry, kind: "duplicate" as const, participantIds: [...existing], nameCollisionIds: [], identityConflictIds: [] };

    const matched = [...new Set(entry.identities.flatMap(identity => manualByScopedIdentity.get(participantScopedIdentityKey(identity) ?? "") ?? []))];
    const available = matched.filter(id => !claimed.has(id));
    let kind: EligibilityEntryKind = "new";
    let participantIds: string[] = [];
    let identityConflictIds: string[] = [];
    if (available.length) {
      kind = "linked";
      participantIds = available;
      for (const id of available) claimed.add(id);
      byProfile.set(profile, available);
    } else {
      // Same platform account without an available exact link: scope missing/different, already claimed by an earlier
      // entry, or an imported row whose profile was later merged/unlinked. Imported provenance is never rewritten;
      // the entry is added separately and flagged for the host.
      identityConflictIds = [...new Set(entry.identities.flatMap(identity => byAccount.get(participantIdentityKey(identity) ?? "") ?? []))];
      if (identityConflictIds.length) kind = "identity-conflict";
    }

    const key = nameKey(entry.displayName);
    const nameCollisionIds = key
      ? [...(byName.get(key) ?? []), ...(batchNames.get(key) ?? [])].filter(id => !participantIds.includes(id))
      : [];
    if (key && kind !== "linked") batchNames.set(key, [...(batchNames.get(key) ?? []), `profile:${profile}`]);
    return { entry, kind, participantIds, nameCollisionIds, identityConflictIds };
  });
}

export function summarizeEligibility(classifications: readonly EligibilityEntryClassification[]): EligibilityImportSummary {
  return {
    eligible: classifications.length,
    added: classifications.filter(item => item.kind === "new" || item.kind === "identity-conflict").length,
    duplicates: classifications.filter(item => item.kind === "duplicate").length,
    linked: classifications.filter(item => item.kind === "linked").length,
    identityConflicts: classifications.filter(item => item.kind === "identity-conflict").length,
    nameCollisions: classifications.filter(item => item.nameCollisionIds.length > 0).length
  };
}

export function entryDisplayName(entry: EligibilityEntry): string {
  const name = entry.displayName.trim() || `觀眾 ${entry.audienceProfileId.slice(0, 8)}`;
  return name.length > maxDisplayNameLength ? name.slice(0, maxDisplayNameLength) : name;
}

/**
 * Builds the next participant list from an imported snapshot without mutating the input.
 * Re-applying the same snapshot is a no-op because every profile becomes a duplicate.
 */
export function applyEligibilityImport(
  participants: readonly Participant[],
  imported: EligibilityPreview,
  idFactory: () => string = createId
): EligibilityImportResult {
  if (!imported.imported || !imported.snapshotId) throw new Error("尚未取得已匯入的資格快照，名單保持不變。");
  const classifications = classifyEligibility(participants, imported.entries);
  const sourceFor = (entry: EligibilityEntry): ParticipantInteractionSource => ({
    roundId: imported.roundId,
    snapshotId: imported.snapshotId as string,
    audienceProfileId: entry.audienceProfileId
  });

  const linkedSources = new Map<string, ParticipantInteractionSource>();
  for (const item of classifications) {
    if (item.kind === "linked") for (const id of item.participantIds) linkedSources.set(id, sourceFor(item.entry));
  }
  const next = participants.map(participant => {
    const source = linkedSources.get(participant.id);
    return source ? { ...participant, tags: [...participant.tags], source } : participant;
  });

  const added = classifications
    .filter(item => item.kind === "new" || item.kind === "identity-conflict")
    .map(item => createParticipant({
      name: entryDisplayName(item.entry),
      lotteryGroupId: `interaction-profile:${item.entry.audienceProfileId}`,
      ...(item.entry.identities[0] ? { identity: item.entry.identities[0] } : {}),
      source: sourceFor(item.entry),
      group: "",
      tags: []
    }, idFactory));

  return { participants: [...next, ...added], added, classifications, summary: summarizeEligibility(classifications) };
}
