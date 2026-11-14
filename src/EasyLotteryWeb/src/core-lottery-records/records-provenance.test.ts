import { describe, expect, it } from "vitest";
import {
  createCoreLotteryDrawPayload,
  createCoreLotteryDrawPayloadFromPreparedState,
  createCoreLotteryDrawPreparedState,
  matchesPendingCoreLotteryDraw,
  matchesPreparedCoreLotteryDraw,
  readPendingCoreLotteryDrawId,
  storePendingCoreLotteryDrawId,
  validateAndNormalizePending,
  type CoreLotteryDrawRecord
} from "./records";
import type { Participant } from "../lottery/draw";

function memoryStorage(): Storage {
  const values = new Map<string, string>();
  return {
    get length() { return values.size; },
    clear: () => values.clear(),
    getItem: key => values.get(key) ?? null,
    key: index => [...values.keys()][index] ?? null,
    removeItem: key => { values.delete(key); },
    setItem: (key, value) => { values.set(key, String(value)); }
  };
}

const drawId = "fd366ac8-0dae-4677-a3c6-515b5baf137b";
const source = {
  roundId: "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0001",
  snapshotId: "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0002",
  audienceProfileId: "aaaaaaaa-0000-4000-8000-000000000001"
};
const participants: Participant[] = [
  { id: "ticket-1", lotteryGroupId: "interaction-profile:a", name: "Mika", identity: { platform: "youtube", accountId: "UC-a" }, source, level: "", group: "", tags: [], isWinner: false },
  { id: "ticket-2", lotteryGroupId: "manual", name: "Mika", level: "", group: "", tags: [], isWinner: false }
];

function savedRecord(overrides: Partial<CoreLotteryDrawRecord> = {}): CoreLotteryDrawRecord {
  const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");
  return { ...payload, drawnAtUtc: "2026-11-10T00:00:00Z", winnerDisplayName: "Mika", prizeName: "Prize", ...overrides };
}

describe("core lottery draw provenance", () => {
  it("snapshots interaction source only for imported entries, keeping same-name manual rows distinct", () => {
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");

    expect(payload.participants[0].interactionSource).toEqual(source);
    expect(payload.participants[1]).not.toHaveProperty("interactionSource");
    expect(payload.participants[0].lotteryGroupId).not.toBe(payload.participants[1].lotteryGroupId);
  });

  it("persists provenance in pending storage and matches server records with null manual sources and upper-case GUIDs", () => {
    const storage = memoryStorage();
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");
    storePendingCoreLotteryDrawId(storage, payload);
    expect(readPendingCoreLotteryDrawId(storage)).toEqual({ phase: "result", ...payload });

    const fromServer = savedRecord({
      participants: [
        { ...payload.participants[0], interactionSource: { roundId: source.roundId.toUpperCase(), snapshotId: source.snapshotId, audienceProfileId: source.audienceProfileId.toUpperCase() } },
        { ...payload.participants[1], interactionSource: null as never, externalIdentity: null as never }
      ]
    });
    expect(matchesPendingCoreLotteryDraw(fromServer, payload)).toBe(true);
    const prepared = createCoreLotteryDrawPreparedState(drawId, participants, ["Prize"], "ticket-1");
    expect(matchesPreparedCoreLotteryDraw(fromServer, prepared)).toBe(true);
  });

  it("treats changed or missing provenance as a different outcome", () => {
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");
    const otherProfile = savedRecord({
      participants: [{ ...payload.participants[0], interactionSource: { ...source, audienceProfileId: "bbbbbbbb-0000-4000-8000-000000000002" } }, payload.participants[1]]
    });
    const missing = savedRecord({
      participants: [{ ...payload.participants[0], interactionSource: undefined }, payload.participants[1]]
    });

    expect(matchesPendingCoreLotteryDraw(otherProfile, payload)).toBe(false);
    expect(matchesPendingCoreLotteryDraw(missing, payload)).toBe(false);
  });

  it("rejects malformed provenance instead of silently dropping it", () => {
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");
    const malformed = { ...payload, participants: [{ ...payload.participants[0], interactionSource: { ...source, snapshotId: "" } }, payload.participants[1]] };

    expect(() => validateAndNormalizePending(malformed)).toThrow("互動來源");
  });
});

describe("core lottery draw pool snapshot version", () => {
  const version = { lockHash: "0123456789abcdef", lockedAtUtc: "2026-11-10T08:00:00.000Z" };

  it("carries the lock version through prepared, selected and result states and across a storage reload", () => {
    const storage = memoryStorage();
    const prepared = createCoreLotteryDrawPreparedState(drawId, participants, ["Prize"], undefined, version);
    const selected = createCoreLotteryDrawPreparedState(drawId, participants, ["Prize"], "ticket-1", version);
    const result = createCoreLotteryDrawPayloadFromPreparedState(selected);
    storePendingCoreLotteryDrawId(storage, result);

    expect(prepared.poolSnapshotVersion).toEqual(version);
    expect(result.poolSnapshotVersion).toEqual(version);
    expect(readPendingCoreLotteryDrawId(storage)).toEqual({ phase: "result", ...result });
    expect(JSON.parse(JSON.stringify(result)).poolSnapshotVersion).toEqual(version);
  });

  it("matches the server echo (offset timestamp, upper-case hash) but not a missing or different version", () => {
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1", version);
    const prepared = createCoreLotteryDrawPreparedState(drawId, participants, ["Prize"], "ticket-1", version);
    const echoed = savedRecord({ poolSnapshotVersion: { lockHash: version.lockHash.toUpperCase(), lockedAtUtc: "2026-11-10T08:00:00+00:00" } });

    expect(matchesPendingCoreLotteryDraw(echoed, payload)).toBe(true);
    expect(matchesPreparedCoreLotteryDraw(echoed, prepared)).toBe(true);
    expect(matchesPendingCoreLotteryDraw(savedRecord({ poolSnapshotVersion: null }), payload)).toBe(false);
    expect(matchesPendingCoreLotteryDraw(savedRecord({ poolSnapshotVersion: { ...version, lockHash: "fedcba9876543210" } }), payload)).toBe(false);
    expect(matchesPendingCoreLotteryDraw(savedRecord({ poolSnapshotVersion: { ...version, lockedAtUtc: "2026-11-10T08:00:01.000Z" } }), payload)).toBe(false);
  });

  it("keeps unlocked and legacy draws version-free and matching null server values", () => {
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");

    expect(payload).not.toHaveProperty("poolSnapshotVersion");
    expect(matchesPendingCoreLotteryDraw(savedRecord({ poolSnapshotVersion: null }), payload)).toBe(true);
    expect(matchesPendingCoreLotteryDraw(savedRecord({ poolSnapshotVersion: version }), payload)).toBe(false);
  });

  it("rejects a malformed lock version instead of dropping it", () => {
    const payload = createCoreLotteryDrawPayload(drawId, participants, ["Prize"], "ticket-1");

    expect(() => validateAndNormalizePending({ ...payload, poolSnapshotVersion: { lockHash: "short", lockedAtUtc: version.lockedAtUtc } })).toThrow("鎖定名單版本");
    expect(() => validateAndNormalizePending({ ...payload, poolSnapshotVersion: { lockHash: version.lockHash, lockedAtUtc: "not-a-date" } })).toThrow("鎖定名單版本");
  });
});
