import { afterEach, describe, expect, it, vi } from "vitest";
import { apiRequestWithToken, setSessionTokenFallback } from "../api/client";

afterEach(async () => {
  const { clearOvertimeToken } = await import("./api");
  clearOvertimeToken();
  setSessionTokenFallback(null);
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("lottery API", () => {
  it("marks live chat messages as YouTube when the server DTO omits its source field", async () => {
    setSessionTokenFallback("admin-token");
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([{
      id: "message-1",
      authorId: "UC-author",
      authorName: "Alice",
      type: "TextMessageEvent",
      displayMessage: "hello",
      publishedAt: "2026-10-11T10:00:00Z"
    }]), { status: 200, headers: { "Content-Type": "application/json" } })));

    const { listYouTubeLiveChatMessages } = await import("./api");
    await expect(listYouTubeLiveChatMessages("chat-123")).resolves.toMatchObject([
      { id: "message-1", platform: "youtube", authorId: "UC-author" }
    ]);
  });

  it("keeps DrawingRules rates positive and stable", async () => {
    const { normalizeLevelRates } = await import("./api");
    expect(normalizeLevelRates({ " 白金 ": 0, "黃金": 2.9, "": 7, "新手": "bad" }))
      .toEqual({ "白金": 1, "黃金": 2, "新手": 1 });
  });

  it("refuses to write DrawingRules without a specific ETag", async () => {
    const request = vi.fn();
    vi.stubGlobal("fetch", request);
    const { writeDrawingRules } = await import("./api");
    await expect(writeDrawingRules({ Gold: 2 }, "")).rejects.toThrow("載入目前抽獎倍率後才能儲存");
    await expect(writeDrawingRules({ Gold: 2 }, "*")).rejects.toThrow("載入目前抽獎倍率後才能儲存");
    expect(request).not.toHaveBeenCalled();
  });

  it("uses the scoped overtime token only for the overtime feed", async () => {
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-token" });
    const calls: Array<{ path: string; token: string | null; method: string }> = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input), "https://example.test").pathname;
      const token = new Headers(init?.headers).get("X-EasyLottery-Session-Token");
      calls.push({ path, token, method: init?.method ?? "GET" });
      if (path === "/api/obs-sessions") {
        expect(token).toBe("admin-token");
        expect(JSON.parse(String(init?.body)).scopes).toEqual(["control"]);
        return new Response(JSON.stringify({ token: "overtime-token", expiresAtUtc: "2099-01-01T00:00:00Z" }), {
          status: 200, headers: { "Content-Type": "application/json" }
        });
      }
      if (path === "/api/overtime-feed/superchat" && init?.method === "POST") {
        expect(token).toBe("overtime-token");
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected request: ${path}`);
    }));

    const { publishSuperChatToOvertime } = await import("./api");
    const { toOvertimeSuperChat } = await import("./chat");
    const message = {
      id: "superchat-1", type: "SuperChatEvent", authorName: "Viewer", displayMessage: "hello",
      publishedAt: "2026-10-11T10:00:00Z", superChat: { amountMicros: "2000000", currency: "TWD" }
    };
    const event = toOvertimeSuperChat(message)!;
    await publishSuperChatToOvertime(message, event);

    expect(calls.map(call => [call.path, call.method])).toEqual([
      ["/api/obs-sessions", "POST"],
      ["/api/overtime-feed/superchat", "POST"]
    ]);
  });

  it("clears a cached scoped token on logout", async () => {
    const { clearOvertimeToken, publishSuperChatToOvertime } = await import("./api");
    clearOvertimeToken();
    const calls: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input), "https://example.test").pathname;
      calls.push(path);
      if (path === "/api/obs-sessions") return new Response(JSON.stringify({ token: `token-${calls.length}`, expiresAtUtc: "2099-01-01T00:00:00Z" }), {
        status: 200, headers: { "Content-Type": "application/json" }
      });
      return new Response(null, { status: 204 });
    }));
    const message = { id: "event", type: "SuperChatEvent", authorName: "Viewer", displayMessage: "hello", publishedAt: "2026-10-11T10:00:00Z" };
    const event = { source: 1, externalId: "event" } as import("../overtime/api").OvertimeSupportEvent;
    await publishSuperChatToOvertime(message, event);
    clearOvertimeToken();
    await publishSuperChatToOvertime(message, event);
    expect(calls.filter(path => path === "/api/obs-sessions")).toHaveLength(2);
  });

  it("does not cache or use a scoped token issued after logout", async () => {
    setSessionTokenFallback("admin-token");
    let finishFirstIssue!: (response: Response) => void;
    let markFirstIssueStarted!: () => void;
    const firstIssueStarted = new Promise<void>(resolve => { markFirstIssueStarted = resolve; });
    let issues = 0;
    let feedPosts = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input), "https://example.test").pathname;
      if (path === "/api/obs-sessions") {
        issues++;
        if (issues === 1) {
          markFirstIssueStarted();
          return await new Promise<Response>(resolve => { finishFirstIssue = resolve; });
        }
        return new Response(JSON.stringify({ token: "fresh-overtime-token", expiresAtUtc: "2099-01-01T00:00:00Z" }), {
          status: 200, headers: { "Content-Type": "application/json" }
        });
      }
      if (path === "/api/overtime-feed/superchat") {
        feedPosts++;
        return new Response(null, { status: 204 });
      }
      throw new Error(`Unexpected request: ${path}`);
    }));

    const { clearOvertimeToken, publishSuperChatToOvertime } = await import("./api");
    const message = { id: "event", type: "SuperChatEvent", authorName: "Viewer", displayMessage: "hello", publishedAt: "2026-10-11T10:00:00Z" };
    const event = { source: 1, externalId: "event" } as import("../overtime/api").OvertimeSupportEvent;
    const pending = publishSuperChatToOvertime(message, event);
    await firstIssueStarted;
    clearOvertimeToken();
    finishFirstIssue(new Response(JSON.stringify({ token: "stale-overtime-token", expiresAtUtc: "2099-01-01T00:00:00Z" }), {
      status: 200, headers: { "Content-Type": "application/json" }
    }));
    await pending;

    expect(feedPosts).toBe(0);
    await publishSuperChatToOvertime(message, event);
    expect(issues).toBe(2);
    expect(feedPosts).toBe(1);
  });
});
