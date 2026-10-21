import { describe, expect, it } from "vitest";
import {
  exportParticipants,
  exportPrizes,
  expandYoutubeMembers,
  parseParticipantImport,
  parsePrizes,
  parseYoutubeMembers
} from "./import-export";

describe("lottery imports and exports", () => {
  it("round-trips participant CSV fields and preserves shared lottery groups", () => {
    const participants = [{
      id: "internal-id",
      lotteryGroupId: "draw-id",
      name: "Ada, \"Ace\"\nChen",
      level: "銀牌",
      group: "VIP",
      tags: ["A區", "高倍率"],
      isWinner: true
    }];
    const csv = exportParticipants(participants, "csv");

    expect(csv).toContain('"Ada, ""Ace""\nChen"');
    expect(csv).not.toContain("internal-id");
    expect(parseParticipantImport(csv, "csv", () => "new-id")).toMatchObject([{
      id: "new-id",
      lotteryGroupId: "draw-id",
      name: "Ada, \"Ace\"\nChen",
      level: "銀牌",
      group: "VIP",
      tags: ["A區", "高倍率"],
      isWinner: true
    }]);
  });

  it.each(["json", "csv"] as const)("preserves weighted-member group identity through %s round-trip", format => {
    const { participants } = expandYoutubeMembers([{ name: "Alice", level: "Gold" }], { Gold: 3 }, (() => {
      let id = 0;
      return () => `generated-${++id}`;
    })());
    const imported = parseParticipantImport(exportParticipants(participants, format), format, (() => {
      let id = 0;
      return () => `imported-${++id}`;
    })());

    expect(imported).toHaveLength(3);
    expect(new Set(imported.map(item => item.lotteryGroupId))).toEqual(new Set([participants[0].lotteryGroupId]));
    expect(new Set(imported.map(item => item.id)).size).toBe(3);
  });

  it("imports JSON case-insensitively and replaces internal identifiers", () => {
    expect(parseParticipantImport(JSON.stringify([{ Name: " Ada ", Group: " ", Tags: "x|X, y", IsWinner: "TRUE" }]), "json", () => "fresh"))
      .toMatchObject([{ id: "fresh", lotteryGroupId: "fresh", name: "Ada", group: "", tags: ["x", "y"], isWinner: true }]);
  });

  it("does not infer an account identity from non-string legacy JSON fields", () => {
    const imported = parseParticipantImport(
      JSON.stringify([{ Name: "Legacy", Platform: "youtube", AccountId: 42 }]),
      "json",
      () => "legacy-id"
    );
    expect(imported[0].identity).toBeUndefined();
  });

  it.each(["json", "csv"] as const)("round-trips optional platform identity through %s while accepting legacy records", format => {
    const participants = [{
      id: "internal-id",
      lotteryGroupId: "draw-id",
      name: "Alice",
      identity: { platform: "YouTube", accountId: "UC1234567890123456789012" },
      level: "",
      group: "",
      tags: [],
      isWinner: false
    }];
    const imported = parseParticipantImport(exportParticipants(participants, format), format, () => "new-id");

    expect(imported).toMatchObject([{
      id: "new-id",
      name: "Alice",
      identity: { platform: "youtube", accountId: "UC1234567890123456789012" }
    }]);
    const legacy = parseParticipantImport(
      format === "json" ? '[{"Name":"Legacy"}]' : "Name,Level,Group,LotteryGroupId,Tags,IsWinner\r\nLegacy,,,,,false",
      format,
      () => "legacy-id"
    );
    expect(legacy[0].name).toBe("Legacy");
    expect(legacy[0].identity).toBeUndefined();
  });

  it("imports YouTube Studio member CSV by Chinese headings and defaults new level weights to one", () => {
    const members = parseYoutubeMembers("\uFEFF會員,連結到個人資料,目前級別\r\nAlice,https://youtube.test/a,銀牌\r\nBob,https://youtube.test/b,\r\n");
    expect(members).toEqual([{ name: "Alice", level: "銀牌" }, { name: "Bob", level: "" }]);
    expect(expandYoutubeMembers(members, { "銀牌": 3 }, () => "ticket")).toMatchObject({
      levelRates: { "銀牌": 3 },
      participants: [
        { name: "Alice", level: "銀牌", group: "YT會員", lotteryGroupId: "ticket" },
        { name: "Alice", level: "銀牌", group: "YT會員", lotteryGroupId: "ticket" },
        { name: "Alice", level: "銀牌", group: "YT會員", lotteryGroupId: "ticket" },
        { name: "Bob", level: "", group: "YT會員" }
      ]
    });
  });

  it("imports only canonical YouTube channel IDs from Studio profile links", () => {
    const channelId = "UC1234567890123456789012";
    const members = parseYoutubeMembers([
      "會員,連結到個人資料,目前級別",
      `Alice,https://www.youtube.com/channel/${channelId},Gold`,
      "Handle,https://www.youtube.com/@mutable,Gold",
      "External,https://example.test/channel/UC1234567890123456789012,Gold"
    ].join("\n"));
    const expanded = expandYoutubeMembers(members, { Gold: 2 }, (() => {
      let id = 0;
      return () => `id-${++id}`;
    })());

    expect(members).toEqual([
      { name: "Alice", level: "Gold", accountId: channelId },
      { name: "Handle", level: "Gold" },
      { name: "External", level: "Gold" }
    ]);
    expect(expanded.participants.slice(0, 2).map(item => item.identity)).toEqual([
      { platform: "youtube", accountId: channelId },
      { platform: "youtube", accountId: channelId }
    ]);
    expect(expanded.participants.slice(2).every(item => item.identity === undefined)).toBe(true);
  });

  it("preserves prize CSV/JSON shape and rejects malformed or empty data without replacing state", () => {
    expect(exportPrizes(["A, B", "C"], "csv")).toBe('Prize\r\n"A, B"\r\nC');
    expect(parsePrizes('["A","B"]', "json")).toEqual(["A", "B"]);
    expect(parsePrizes("Prize\r\n\"A, B\"\r\n", "csv")).toEqual(["A, B"]);
    expect(parseParticipantImport("", "csv")).toEqual([]);
    expect(() => parsePrizes("{bad", "json")).toThrow();
  });
});
