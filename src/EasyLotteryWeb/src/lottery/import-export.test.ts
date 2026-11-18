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

  describe("interaction provenance and channel scope", () => {
    const source = {
      roundId: "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0001",
      snapshotId: "7c1f4bde-21f5-4e1e-9d4e-5a1f0c1a0002",
      audienceProfileId: "aaaaaaaa-0000-4000-8000-000000000001"
    };
    const imported = [
      {
        id: "ticket-1", lotteryGroupId: "viewer-group", name: "Mika",
        identity: { platform: "youtube", accountId: "UC-synthetic-mika", channelScope: "synthetic-channel" },
        source, level: "Gold", group: "Members", tags: ["a"], isWinner: true
      },
      {
        id: "ticket-2", lotteryGroupId: "viewer-group", name: "Mika",
        identity: { platform: "youtube", accountId: "UC-synthetic-mika", channelScope: "synthetic-channel" },
        source, level: "Gold", group: "Members", tags: ["a"], isWinner: true
      },
      { id: "ticket-3", lotteryGroupId: "manual", name: "Manual", level: "", group: "", tags: [], isWinner: false }
    ];
    const sequence = (prefix: string) => { let id = 0; return () => `${prefix}-${++id}`; };

    it.each(["json", "csv"] as const)("round-trips source and identity.channelScope through %s with fresh IDs and shared groups", format => {
      const exported = exportParticipants(imported, format);
      const restored = parseParticipantImport(exported, format, sequence("fresh"));

      expect(exported).not.toContain("ticket-1");
      expect(restored).toEqual([
        { id: "fresh-1", lotteryGroupId: "viewer-group", name: "Mika", identity: { platform: "youtube", accountId: "UC-synthetic-mika", channelScope: "synthetic-channel" }, source, level: "Gold", group: "Members", tags: ["a"], isWinner: true },
        { id: "fresh-2", lotteryGroupId: "viewer-group", name: "Mika", identity: { platform: "youtube", accountId: "UC-synthetic-mika", channelScope: "synthetic-channel" }, source, level: "Gold", group: "Members", tags: ["a"], isWinner: true },
        { id: "fresh-3", lotteryGroupId: "manual", name: "Manual", level: "", group: "", tags: [], isWinner: false }
      ]);
      // A second round-trip is stable.
      expect(parseParticipantImport(exportParticipants(restored, format), format, sequence("fresh"))).toEqual(restored);
    });

    it("uses explicit flat scalar columns in CSV and JSON", () => {
      const [header, firstRow, , manualRow] = exportParticipants(imported, "csv").split("\r\n");
      expect(header).toBe("Name,Level,Group,LotteryGroupId,Tags,IsWinner,Platform,AccountId,ChannelScope,SourceRoundId,SourceSnapshotId,SourceAudienceProfileId");
      expect(firstRow.endsWith(`,youtube,UC-synthetic-mika,synthetic-channel,${source.roundId},${source.snapshotId},${source.audienceProfileId}`)).toBe(true);
      expect(manualRow).toBe("Manual,,,manual,,false,,,,,,");
      const json = JSON.parse(exportParticipants(imported, "json"));
      expect(json[0]).toMatchObject({ ChannelScope: "synthetic-channel", SourceRoundId: source.roundId, SourceSnapshotId: source.snapshotId, SourceAudienceProfileId: source.audienceProfileId });
      expect(json[0]).not.toHaveProperty("Source");
      expect(json[2]).not.toHaveProperty("ChannelScope");
      expect(json[2]).not.toHaveProperty("SourceRoundId");
    });

    it.each([
      ["json", '[{"Name":"Legacy","Platform":"youtube","AccountId":"UC-legacy","LotteryGroupId":"g","IsWinner":true}]'],
      ["csv", "Name,Level,Group,LotteryGroupId,Tags,IsWinner,Platform,AccountId\r\nLegacy,,,g,,true,youtube,UC-legacy"]
    ] as const)("still imports legacy %s without source or channel scope", (format, content) => {
      const [legacy] = parseParticipantImport(content, format, () => "legacy-id");
      expect(legacy).toEqual({ id: "legacy-id", lotteryGroupId: "g", name: "Legacy", identity: { platform: "youtube", accountId: "UC-legacy" }, level: "", group: "", tags: [], isWinner: true });
    });

    it.each(["json", "csv"] as const)("drops partial, malformed or non-scalar provenance from %s instead of trusting it", format => {
      const rows = [
        { Name: "Partial", SourceRoundId: source.roundId, SourceSnapshotId: source.snapshotId },
        { Name: "Malformed", SourceRoundId: "not-a-uuid", SourceSnapshotId: source.snapshotId, SourceAudienceProfileId: source.audienceProfileId },
        { Name: "Scope only", ChannelScope: "synthetic-channel" }
      ];
      const content = format === "json"
        ? JSON.stringify([...rows, { Name: "Nested", Source: source, SourceRoundId: { value: source.roundId }, SourceSnapshotId: source.snapshotId, SourceAudienceProfileId: source.audienceProfileId, Platform: "youtube", AccountId: "UC-x", ChannelScope: 7 }])
        : ["Name,SourceRoundId,SourceSnapshotId,SourceAudienceProfileId,ChannelScope",
          ...rows.map(row => [row.Name, row.SourceRoundId ?? "", row.SourceSnapshotId ?? "", row.SourceAudienceProfileId ?? "", row.ChannelScope ?? ""].join(","))].join("\r\n");
      const parsed = parseParticipantImport(content, format, () => "id");

      expect(parsed.every(participant => participant.source === undefined)).toBe(true);
      // Channel scope alone never forms an identity without platform and account.
      expect(parsed.find(participant => participant.name === "Scope only")?.identity).toBeUndefined();
      if (format === "json") expect(parsed.find(participant => participant.name === "Nested")?.identity).toEqual({ platform: "youtube", accountId: "UC-x" });
    });

    it("normalizes provenance UUID case on import", () => {
      const [row] = parseParticipantImport(JSON.stringify([{ Name: "Upper", SourceRoundId: source.roundId.toUpperCase(), SourceSnapshotId: source.snapshotId, SourceAudienceProfileId: source.audienceProfileId.toUpperCase() }]), "json", () => "id");
      expect(row.source).toEqual(source);
    });
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
