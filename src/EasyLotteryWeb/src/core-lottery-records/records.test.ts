import { describe, expect, it } from "vitest";
import {
  buildCoreLotteryDrawHistoryQuery,
  clearPendingCoreLotteryDrawId,
  createCoreLotteryDrawId,
  createCoreLotteryDrawPayload,
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
    const prepared = createCoreLotteryDrawPreparedState(drawId, participants, ["禮盒", "小禮物"]);
    storePendingCoreLotteryDrawPreparedState(storage, prepared);
    expect(readPendingCoreLotteryDrawId(storage)).toEqual(prepared);

    const completed = createCoreLotteryDrawPayload(drawId, participants, ["禮盒", "小禮物"], "ticket-1");
    const record = {
      ...completed,
      drawnAtUtc: "2026-10-24T10:00:00Z",
      winnerDisplayName: "Mika",
      prizeName: "禮盒"
    };
    expect(matchesPreparedCoreLotteryDraw(record, prepared)).toBe(true);
    expect(matchesPreparedCoreLotteryDraw({ ...record, participants: record.participants.slice(1) }, prepared)).toBe(false);
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
});
