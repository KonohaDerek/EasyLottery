import { describe, expect, it } from "vitest";
import { drawLottery, type Participant } from "./draw";
import {
  advanceLockedPool,
  applyConfirmedWinner,
  clearLockedPool,
  createLockedPool,
  lockedPoolStorageKey,
  lockedPoolVersion,
  readLockedPool,
  storeLockedPool
} from "./locked-pool";

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

const source = {
  roundId: "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0001",
  snapshotId: "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0002",
  audienceProfileId: "aaaaaaaa-0000-4000-8000-000000000001"
};

const participants: Participant[] = [
  { id: "ticket-1", lotteryGroupId: "interaction-profile:a", name: "Mika", identity: { platform: "youtube", accountId: "UC-a" }, source, level: "", group: "", tags: [], isWinner: false },
  { id: "ticket-2", lotteryGroupId: "member", name: "Ren", level: "Gold", group: "YT會員", tags: ["vip"], isWinner: false },
  { id: "ticket-3", lotteryGroupId: "member", name: "Ren", level: "Gold", group: "YT會員", tags: ["vip"], isWinner: false }
];
const lockTime = new Date("2026-11-10T08:00:00.000Z");

describe("locked pool snapshot", () => {
  it("restores the exact pool, provenance and lock hash after a reload", () => {
    const storage = memoryStorage();
    const pool = createLockedPool(participants, ["Prize A", "Prize B"], [], lockTime);
    storeLockedPool(storage, pool);

    const restored = readLockedPool(storage);

    expect(restored).toEqual({ status: "valid", pool });
    if (restored.status !== "valid") throw new Error("expected a valid pool");
    expect(restored.pool.participants[0].source).toEqual(source);
    expect(restored.pool.lockedAtUtc).toBe(lockTime.toISOString());
    expect(restored.pool.lockHash).toMatch(/^[\da-f]{16}$/);
    expect(storage.getItem(lockedPoolStorageKey)).not.toContain("session-token");
  });

  it("is a defensive copy; later edits of the live list do not change the lock", () => {
    const live = participants.map(participant => ({ ...participant, tags: [...participant.tags] }));
    const pool = createLockedPool(live, ["Prize"], [], lockTime);
    live[1].name = "Changed";
    live[1].tags.push("new");

    expect(pool.participants[1].name).toBe("Ren");
    expect(pool.participants[1].tags).toEqual(["vip"]);
  });

  it("rejects corrupt JSON, unsupported versions, tampered content and invalid provenance", () => {
    const storage = memoryStorage();
    const pool = createLockedPool(participants, ["Prize"], [], lockTime);

    storage.setItem(lockedPoolStorageKey, "{not json");
    expect(readLockedPool(storage).status).toBe("invalid");

    storage.setItem(lockedPoolStorageKey, JSON.stringify({ ...pool, schemaVersion: 2 }));
    expect(readLockedPool(storage)).toMatchObject({ status: "invalid", reason: expect.stringContaining("版本") });

    const tampered = JSON.parse(JSON.stringify(pool));
    tampered.participants[1].isWinner = true;
    storage.setItem(lockedPoolStorageKey, JSON.stringify(tampered));
    expect(readLockedPool(storage)).toMatchObject({ status: "invalid", reason: expect.stringContaining("完整性") });

    const extraPrize = JSON.parse(JSON.stringify(pool));
    extraPrize.prizes.push("Injected");
    storage.setItem(lockedPoolStorageKey, JSON.stringify(extraPrize));
    expect(readLockedPool(storage).status).toBe("invalid");

    const badSource = JSON.parse(JSON.stringify(pool));
    badSource.participants[0].source.audienceProfileId = "Mika";
    storage.setItem(lockedPoolStorageKey, JSON.stringify(badSource));
    expect(readLockedPool(storage)).toMatchObject({ status: "invalid", reason: expect.stringContaining("互動來源") });

    clearLockedPool(storage);
    expect(readLockedPool(storage)).toEqual({ status: "none" });
  });

  it("carries identity channel scope through lock and reload; scope-less identities still validate", () => {
    const storage = memoryStorage();
    const scoped: Participant[] = [{ ...participants[0], identity: { platform: "youtube", channelScope: "channel-a", accountId: "UC-a" } }, participants[1]];
    const scopedPool = createLockedPool(scoped, ["Prize"], [], lockTime);
    storeLockedPool(storage, scopedPool);

    const restored = readLockedPool(storage);
    if (restored.status !== "valid") throw new Error("expected a valid pool");
    expect(restored.pool.participants[0].identity).toEqual({ platform: "youtube", channelScope: "channel-a", accountId: "UC-a" });

    const unscopedPool = createLockedPool([participants[0], participants[1]], ["Prize"], [], lockTime);
    expect(unscopedPool.lockHash).not.toBe(scopedPool.lockHash);
    // A pool serialized before channelScope existed has no such key and must still validate.
    storeLockedPool(storage, unscopedPool);
    expect(readLockedPool(storage)).toEqual({ status: "valid", pool: unscopedPool });
  });

  it("exposes the lock hash and lock time as the draw pool version", () => {
    const pool = createLockedPool(participants, ["Prize"], [], lockTime);

    expect(lockedPoolVersion(pool)).toEqual({ lockHash: pool.lockHash, lockedAtUtc: lockTime.toISOString() });
  });

  it("refuses to lock an empty pool or duplicate ticket IDs", () => {
    expect(() => createLockedPool([], ["Prize"], [])).toThrow("沒有參加者");
    expect(() => createLockedPool([participants[0], participants[0]], ["Prize"], [])).toThrow("重複");
  });

  it("advances after a confirmed draw while keeping lock time and lock hash", () => {
    const storage = memoryStorage();
    const pool = createLockedPool(participants, ["Prize A", "Prize B"], [], lockTime);
    const drawn = drawLottery(pool.participants, pool.prizes, () => 1);
    if (!drawn.winner) throw new Error("expected a winner");
    const advanced = advanceLockedPool(pool, drawn.participants, drawn.prizes, [{ ...drawn.winner, drawId: "draw-1" }]);
    storeLockedPool(storage, advanced);

    const restored = readLockedPool(storage);
    if (restored.status !== "valid") throw new Error("expected a valid pool");
    expect(restored.pool.lockHash).toBe(pool.lockHash);
    expect(restored.pool.lockedAtUtc).toBe(pool.lockedAtUtc);
    expect(restored.pool.hash).not.toBe(pool.hash);
    expect(restored.pool.prizes).toEqual(["Prize B"]);
    expect(restored.pool.participants.filter(participant => participant.isWinner).map(participant => participant.id)).toEqual(["ticket-2", "ticket-3"]);
    expect(restored.pool.winners[0].drawId).toBe("draw-1");
  });

  it("applies a server-confirmed winner exactly like drawLottery and rejects stale winners", () => {
    const viaDraw = drawLottery(participants, ["Prize A", "Prize B"], () => 0);
    const viaConfirm = applyConfirmedWinner(participants, ["Prize A", "Prize B"], "ticket-1");

    expect(viaConfirm).toEqual({ participants: viaDraw.participants, prizes: viaDraw.prizes });
    expect(applyConfirmedWinner(viaDraw.participants, viaDraw.prizes, "ticket-1")).toBeNull();
    expect(applyConfirmedWinner(participants, [], "ticket-1")).toBeNull();
    expect(applyConfirmedWinner(participants, ["Prize"], "missing")).toBeNull();
  });
});
