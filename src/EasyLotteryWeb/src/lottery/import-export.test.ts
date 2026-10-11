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

  it("preserves prize CSV/JSON shape and rejects malformed or empty data without replacing state", () => {
    expect(exportPrizes(["A, B", "C"], "csv")).toBe('Prize\r\n"A, B"\r\nC');
    expect(parsePrizes('["A","B"]', "json")).toEqual(["A", "B"]);
    expect(parsePrizes("Prize\r\n\"A, B\"\r\n", "csv")).toEqual(["A, B"]);
    expect(parseParticipantImport("", "csv")).toEqual([]);
    expect(() => parsePrizes("{bad", "json")).toThrow();
  });
});
