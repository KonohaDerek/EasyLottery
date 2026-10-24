import { describe, expect, it } from "vitest";
import {
  buildCoreLotteryDrawHistoryQuery,
  clearPendingCoreLotteryDrawId,
  createCoreLotteryDrawId,
  createCoreLotteryDrawPayload,
  createCoreLotteryDrawPayloadFromPreparedState,
  createCoreLotteryDrawPreparedState,
  matchesPendingCoreLotteryDraw,
  matchesPreparedCoreLotteryDraw,
  pendingCoreLotteryDrawStorageKey,
  preflightPendingCoreLotteryDrawStorage,
  readPendingCoreLotteryDrawId,
  storePendingCoreLotteryDrawId,
  storePendingCoreLotteryDrawPreparedState
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

describe("core lottery history helpers", () => {
  const participants: Participant[] = [
    { id: "ticket-1", lotteryGroupId: "viewer-one", name: "Mika", identity: { platform: "youtube", accountId: "UC-synthetic-1" }, level: "Gold", group: "Members", tags: ["tag-a"], isWinner: false },
    { id: "ticket-2", lotteryGroupId: "viewer-two", name: "Ren", identity: { platform: "twitch", accountId: "tw-synthetic-2" }, level: "", group: "Guests", tags: [], isWinner: false },
    { id: "ticket-3", lotteryGroupId: "viewer-three", name: "Already Won", level: "", group: "Guests", tags: ["excluded"], isWinner: true }
  ];

  it("creates a stable UUID before the drawing operation", () => {
    const id = createCoreLotteryDrawId(() => "fd366ac8-0dae-4677-a3c6-515b5baf137b");
    expect(id).toBe("fd366ac8-0dae-4677-a3c6-515b5baf137b");
    expect(() => createCoreLotteryDrawId(() => "not-a-uuid")).toThrow("穩定的抽獎識別碼");
  });

  it("persists the full immutable draw-time roster, external identities and rules", () => {
    const storage = memoryStorage();
    const operation = createCoreLotteryDrawPayload("fd366ac8-0dae-4677-a3c6-515b5baf137b", participants, ["禮盒", "小禮物"], "ticket-1");
    preflightPendingCoreLotteryDrawStorage(storage, operation);
    expect(storage.getItem(`${pendingCoreLotteryDrawStorageKey}.probe.${operation.drawId}`)).toBeNull();
    storePendingCoreLotteryDrawId(storage, operation);
    expect(readPendingCoreLotteryDrawId(storage)).toEqual({ phase: "result", ...operation });
    const replacement = { ...operation, drawId: "87cb2fe4-a668-46e8-ae8b-f8f88bd32be4" };
    storePendingCoreLotteryDrawId(storage, replacement);
    clearPendingCoreLotteryDrawId(storage, operation.drawId);
    expect(readPendingCoreLotteryDrawId(storage)).toEqual({ phase: "result", ...replacement });
    clearPendingCoreLotteryDrawId(storage, replacement.drawId);
    expect(readPendingCoreLotteryDrawId(storage)).toBeNull();
    expect(operation.participants).toHaveLength(3);
    expect(operation.participants[0].externalIdentity).toEqual({ platform: "youtube", accountId: "UC-synthetic-1" });
    expect(operation.participants[2].eligibilityStatus).toBe("already-won");
    expect(operation.ruleSnapshot.eligibleEntryCount).toBe(2);
    expect(operation.ruleSnapshot.prizeQueue).toEqual(["禮盒", "小禮物"]);
    expect(() => storePendingCoreLotteryDrawId(storage, { ...operation, winnerParticipantId: "missing-ticket" })).toThrow("得獎籤");
  });

  it("stores a complete pre-draw intent so a refresh never starts a second draw", () => {
    const storage = memoryStorage();
    const drawId = "fd366ac8-0dae-4677-a3c6-515b5baf137b";
    const prepared = createCoreLotteryDrawPreparedState(drawId, participants, ["禮盒", "小禮物"], "ticket-2");
    storePendingCoreLotteryDrawPreparedState(storage, prepared);
    const restored = readPendingCoreLotteryDrawId(storage);
    expect(restored).toEqual(prepared);
    if (!restored || restored.phase !== "prepared") throw new Error("expected a saved prepared draw");
    expect(restored.selectionState).toBe("selected");
    expect(restored.winnerParticipantId).toBe("ticket-2");

    const completed = createCoreLotteryDrawPayloadFromPreparedState(restored);
    expect(completed.winnerParticipantId).toBe("ticket-2");
    expect(completed.participants).toHaveLength(3);
    expect(completed.ruleSnapshot.prizeQueue).toEqual(["禮盒", "小禮物"]);
    const record = {
      ...completed,
      drawnAtUtc: "2026-10-24T10:00:00Z",
      winnerDisplayName: "Ren",
      prizeName: "禮盒"
    };
    expect(matchesPreparedCoreLotteryDraw(record, restored)).toBe(true);
    expect(matchesPreparedCoreLotteryDraw({ ...record, winnerParticipantId: "ticket-1", winnerDisplayName: "Mika" }, restored)).toBe(false);
    expect(matchesPreparedCoreLotteryDraw({ ...record, participants: record.participants.slice(1) }, restored)).toBe(false);
  });

  it("distinguishes a prepared snapshot with no selected outcome from a legacy unknown outcome", () => {
    const drawId = "fd366ac8-0dae-4677-a3c6-515b5baf137b";
    const ready = createCoreLotteryDrawPreparedState(drawId, participants, ["禮盒"]);
    expect(ready.selectionState).toBe("ready");
    expect(() => createCoreLotteryDrawPayloadFromPreparedState(ready)).toThrow("尚未保存選定的得獎籤");

    const legacy = { ...ready } as { phase: "prepared"; drawId: string; ruleSnapshot: typeof ready.ruleSnapshot; participants: typeof ready.participants };
    delete (legacy as Partial<typeof ready>).selectionState;
    expect(readPendingCoreLotteryDrawId({
      ...memoryStorage(),
      getItem: () => JSON.stringify(legacy)
    } as Storage)).toEqual(legacy);
  });

  it("builds bounded pages and local-date UTC boundaries", () => {
    const query = new URLSearchParams(buildCoreLotteryDrawHistoryQuery({
      beforeDate: "2026-10-24", fromDate: "2026-10-02", page: 0, pageSize: 500, search: " 特獎 "
    }));
    expect(query.get("page")).toBe("1");
    expect(query.get("pageSize")).toBe("100");
    expect(query.get("search")).toBe("特獎");
    expect(new Date(query.get("fromUtc")!).getHours()).toBe(0);
    expect(new Date(query.get("beforeUtc")!).getDate()).toBe(25);
  });

  it("rejects an unrelated result while reconciling an uncertain save", () => {
    const pending = createCoreLotteryDrawPayload("fd366ac8-0dae-4677-a3c6-515b5baf137b", participants, ["禮盒"], "ticket-1");
    const record = { ...pending, drawnAtUtc: "2026-10-24T10:00:00Z", winnerDisplayName: "Mika", prizeName: "禮盒" };
    expect(matchesPendingCoreLotteryDraw(record, pending)).toBe(true);
    expect(matchesPendingCoreLotteryDraw({ ...record, participants: record.participants.slice(1) }, pending)).toBe(false);
    expect(matchesPendingCoreLotteryDraw({ ...record, ruleSnapshot: { ...record.ruleSnapshot, prizeQueue: ["別的獎"] } }, pending)).toBe(false);
  });

  it("reconciles a reloaded result from its summary and complete paged roster", () => {
    const drawId = "fd366ac8-0dae-4677-a3c6-515b5baf137b";
    const pending = createCoreLotteryDrawPayload(drawId, [participants[0]], ["已保存獎項"], "ticket-1");
    const storage = memoryStorage();
    storage.setItem(pendingCoreLotteryDrawStorageKey, JSON.stringify(pending));
    const expected = readPendingCoreLotteryDrawId(storage);
    expect(expected?.phase).toBe("result");
    if (!expected || expected.phase !== "result") throw new Error("expected saved pending draw");

    const summary = {
      drawId,
      drawnAtUtc: "2026-10-24T10:00:00Z",
      winnerParticipantId: pending.winnerParticipantId,
      winnerDisplayName: "Mika",
      prizeName: "已保存獎項",
      participantCount: pending.participants.length,
      eligibleEntryCount: pending.ruleSnapshot.eligibleEntryCount,
      ruleSnapshot: pending.ruleSnapshot
    };
    const reconciled = { ...summary, participants: [...pending.participants] };

    expect(matchesPendingCoreLotteryDraw(reconciled, expected)).toBe(true);
  });
});
