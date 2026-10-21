import { describe, expect, it } from "vitest";
import { isChatMessageAllowed, processChatBatch, toOvertimeSuperChat, type ChatMessage } from "./chat";

const message = (values: Partial<ChatMessage> = {}): ChatMessage => {
  const authorName = values.authorName ?? "Alice";
  return {
    id: "message-1",
    publishedAt: "2026-10-11T10:00:00.000Z",
    type: "TextMessageEvent",
    platform: "youtube",
    authorId: `channel-${authorName.toLowerCase()}`,
    authorName,
    displayMessage: "hello keyword",
    ...values
  };
};

const rules = { keyword: "keyword, join", whiteList: "Alice, Bob", blackList: "", cooldownSeconds: 0 };

describe("lottery live chat policy", () => {
  it("matches any keyword case-insensitively, then applies blacklist before whitelist", () => {
    expect(isChatMessageAllowed(message(), rules, new Map()).allowed).toBe(true);
    expect(isChatMessageAllowed(message({ displayMessage: "no match" }), rules, new Map()).reason).toBe("keyword");
    expect(isChatMessageAllowed(message({ authorName: "Mallory" }), rules, new Map()).reason).toBe("whitelist");
    expect(isChatMessageAllowed(message(), { ...rules, blackList: "alice" }, new Map()).reason).toBe("blacklist");
    expect(isChatMessageAllowed(message({ authorName: " " }), rules, new Map()).reason).toBe("author");
  });

  it("uses account cooldown and permits a message at the exact boundary", () => {
    const key = "account:" + JSON.stringify(["youtube", "channel-alice"]);
    const accepted = new Map([[key, new Date("2026-10-11T10:00:00.000Z").getTime()]]);
    const policy = { ...rules, cooldownSeconds: 30 };
    expect(isChatMessageAllowed(message({ publishedAt: "2026-10-11T10:00:29.999Z" }), policy, accepted).reason).toBe("cooldown");
    expect(isChatMessageAllowed(message({ publishedAt: "2026-10-11T10:00:30.000Z" }), policy, accepted).allowed).toBe(true);
  });

  it("advances past rejected messages, sorts new messages, and adds each eligible author once", () => {
    const result = processChatBatch([
      message({ id: "later", publishedAt: "2026-10-11T10:00:02.000Z", authorName: "Bob" }),
      message({ id: "rejected", publishedAt: "2026-10-11T10:00:01.000Z", displayMessage: "no match" }),
      message({ id: "earlier", publishedAt: "2026-10-11T10:00:00.500Z", authorName: "Alice" })
    ], "2026-10-11T09:59:59.000Z", rules, new Map(), []);

    expect(result.cursor).toBe("2026-10-11T10:00:02.000Z");
    expect(result.accepted.map(item => item.id)).toEqual(["earlier", "later"]);
    expect(result.addedParticipants.map(item => item.name)).toEqual(["Alice", "Bob"]);
  });

  it("deduplicates the same platform account across renamed display names, but separates same-name accounts", () => {
    const identityRules = { ...rules, whiteList: "Alice, Alice VT" };
    const result = processChatBatch([
      message({ id: "first", authorId: "UC-one", authorName: "Alice", publishedAt: "2026-10-11T10:00:00.000Z" }),
      message({ id: "case-change", authorId: "UC-one", authorName: "aLiCe", publishedAt: "2026-10-11T10:00:00.500Z" }),
      message({ id: "renamed", authorId: "UC-one", authorName: "Alice VT", publishedAt: "2026-10-11T10:00:01.000Z" }),
      message({ id: "homonym", authorId: "UC-two", authorName: "Alice", publishedAt: "2026-10-11T10:00:02.000Z" })
    ], "2026-10-11T09:59:59.000Z", identityRules, new Map(), []);

    expect(result.addedParticipants).toMatchObject([
      { name: "Alice", identity: { platform: "youtube", accountId: "UC-one" } },
      { name: "Alice", identity: { platform: "youtube", accountId: "UC-two" } }
    ]);
    expect(result.accepted).toHaveLength(4);
  });

  it("does not merge a stable chat account into a same-name manual participant without identity", () => {
    const manual = [{
      id: "manual",
      lotteryGroupId: "manual",
      name: "Alice",
      level: "",
      group: "手動",
      tags: [],
      isWinner: false
    }];
    const result = processChatBatch([
      message({ id: "chat", authorId: "UC-one", authorName: "Alice" })
    ], "2026-10-11T09:59:59.000Z", rules, new Map(), manual);

    expect(result.addedParticipants).toHaveLength(1);
    expect(result.addedParticipants[0].identity).toEqual({ platform: "youtube", accountId: "UC-one" });
  });

  it("keeps identical account ID strings separate across platforms", () => {
    const identityRules = { ...rules, whiteList: "Viewer" };
    const existing = [{
      id: "youtube",
      lotteryGroupId: "youtube",
      name: "Viewer",
      identity: { platform: "youtube", accountId: "same-id" },
      level: "",
      group: "",
      tags: [],
      isWinner: false
    }];
    const result = processChatBatch([
      message({ id: "other-platform", platform: "twitch", authorId: "same-id", authorName: "Viewer" })
    ], "2026-10-11T09:59:59.000Z", identityRules, new Map(), existing);

    expect(result.addedParticipants).toMatchObject([
      { name: "Viewer", identity: { platform: "twitch", accountId: "same-id" } }
    ]);
  });

  it("does not infer participant identity from a name when the stable author ID is missing", () => {
    const policy = { ...rules, whiteList: "Guest", cooldownSeconds: 30 };
    const result = processChatBatch([
      message({ id: "guest-1", authorId: undefined, authorName: "Guest", publishedAt: "2026-10-11T10:00:00.000Z" }),
      message({ id: "guest-2", authorId: undefined, authorName: "Guest", publishedAt: "2026-10-11T10:00:01.000Z" })
    ], "2026-10-11T09:59:59.000Z", policy, new Map(), []);

    expect(result.accepted).toHaveLength(2);
    expect(result.addedParticipants).toHaveLength(2);
    expect(result.addedParticipants.every(item => item.identity === undefined)).toBe(true);
    expect(result.lastAccepted.size).toBe(0);
  });

  it("applies cooldown by stable account across renames without blocking a same-name account", () => {
    const policy = { ...rules, whiteList: "Alice, Alice after rename", cooldownSeconds: 30 };
    const first = processChatBatch([
      message({ id: "accepted", authorId: "UC-one", authorName: "Alice", publishedAt: "2026-10-11T10:00:00.000Z" })
    ], "2026-10-11T09:59:59.000Z", policy, new Map(), []);

    expect(isChatMessageAllowed(message({
      id: "renamed",
      authorId: "UC-one",
      authorName: "Alice after rename",
      publishedAt: "2026-10-11T10:00:01.000Z"
    }), policy, first.lastAccepted).reason).toBe("cooldown");
    expect(isChatMessageAllowed(message({
      id: "homonym",
      authorId: "UC-two",
      authorName: "Alice",
      publishedAt: "2026-10-11T10:00:01.000Z"
    }), policy, first.lastAccepted).allowed).toBe(true);
  });

  it("maps only SuperChat events using safe display fallbacks and micros conversion", () => {
    expect(toOvertimeSuperChat(message({
      type: "SuperChatEvent",
      superChat: { amountMicros: 12500000, amountDisplay: "NT$12.50", currency: "TWD", userComment: "Thanks!" },
      authorImageUrl: "https://img.example/alice.png"
    }))).toMatchObject({ source: 1, sourceLabel: "YouTube SuperChat", displayName: "Alice", message: "Thanks!", amount: 12.5, currency: "TWD" });
    expect(toOvertimeSuperChat(message())).toBeNull();
  });
});
