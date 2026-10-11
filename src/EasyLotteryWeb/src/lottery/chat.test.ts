import { describe, expect, it } from "vitest";
import { isChatMessageAllowed, processChatBatch, toOvertimeSuperChat, type ChatMessage } from "./chat";

const message = (values: Partial<ChatMessage> = {}): ChatMessage => ({
  id: "message-1",
  publishedAt: "2026-10-11T10:00:00.000Z",
  type: "TextMessageEvent",
  authorName: "Alice",
  displayMessage: "hello keyword",
  ...values
});

const rules = { keyword: "keyword, join", whiteList: "Alice, Bob", blackList: "", cooldownSeconds: 0 };

describe("lottery live chat policy", () => {
  it("matches any keyword case-insensitively, then applies blacklist before whitelist", () => {
    expect(isChatMessageAllowed(message(), rules, new Map()).allowed).toBe(true);
    expect(isChatMessageAllowed(message({ displayMessage: "no match" }), rules, new Map()).reason).toBe("keyword");
    expect(isChatMessageAllowed(message({ authorName: "Mallory" }), rules, new Map()).reason).toBe("whitelist");
    expect(isChatMessageAllowed(message(), { ...rules, blackList: "alice" }, new Map()).reason).toBe("blacklist");
    expect(isChatMessageAllowed(message({ authorName: " " }), rules, new Map()).reason).toBe("author");
  });

  it("uses case-insensitive cooldown and permits a message at the exact boundary", () => {
    const accepted = new Map([["alice", new Date("2026-10-11T10:00:00.000Z").getTime()]]);
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

  it("maps only SuperChat events using safe display fallbacks and micros conversion", () => {
    expect(toOvertimeSuperChat(message({
      type: "SuperChatEvent",
      superChat: { amountMicros: 12500000, amountDisplay: "NT$12.50", currency: "TWD", userComment: "Thanks!" },
      authorImageUrl: "https://img.example/alice.png"
    }))).toMatchObject({ source: 1, sourceLabel: "YouTube SuperChat", displayName: "Alice", message: "Thanks!", amount: 12.5, currency: "TWD" });
    expect(toOvertimeSuperChat(message())).toBeNull();
  });
});
