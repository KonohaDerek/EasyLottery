import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, setSessionTokenFallback } from "../api/client";
import { appendOvertimeEvent, readOvertimeFeed, readOvertimeSettings, readOvertimeSnapshot, saveOvertimeSettings } from "./api";

afterEach(() => {
  vi.unstubAllGlobals();
  setSessionTokenFallback(null);
});

describe("overtime OBS API adapter", () => {
  it("reads the overtime overlay from the existing YAML projection", async () => {
    setSessionTokenFallback("obs-read-token");
    const fetchMock = vi.fn().mockResolvedValue(new Response("configVersion: 1\novertimeOverlay:\n  sessionState: running\n", {
      status: 200,
      headers: { "content-type": "text/yaml" }
    }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(readOvertimeSettings()).resolves.toMatchObject({ sessionState: "running" });
    expect(fetchMock).toHaveBeenCalledWith("/settings", expect.objectContaining({ headers: expect.any(Headers) }));
    expect((fetchMock.mock.calls[0][1]?.headers as Headers).get("X-EasyLottery-Session-Token")).toBe("obs-read-token");
  });

  it("waits for both snapshot requests to settle before returning a read error", async () => {
    let resolveSettings!: (response: Response) => void;
    const settingsResponse = new Promise<Response>(resolve => { resolveSettings = resolve; });
    const fetchMock = vi.fn((input: RequestInfo | URL) => input.toString() === "/settings"
      ? settingsResponse
      : Promise.resolve(new Response("unavailable", { status: 503 })));
    vi.stubGlobal("fetch", fetchMock);

    let rejected = false;
    const snapshot = readOvertimeSnapshot().catch(cause => {
      rejected = true;
      throw cause;
    });
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(rejected).toBe(false);
    resolveSettings(new Response("overtimeOverlay:\n  sessionState: running\n", {
      status: 200,
      headers: { "content-type": "text/yaml" }
    }));
    await expect(snapshot).rejects.toMatchObject({ status: 503 });
    expect(rejected).toBe(true);
  });

  it("merges a settings patch into the latest projection instead of replacing newer fields", async () => {
    setSessionTokenFallback("obs-control-token");
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("configVersion: 1\nobsLayout:\n  activeLayoutKey: stage-spotlight\novertimeOverlay:\n  sessionState: idle\n  plannedEndAtUtc: '2026-10-11T15:00:00Z'\n  showAmounts: true\n", {
        status: 200,
        headers: { "content-type": "text/yaml", etag: 'W/"settings-1"' }
      }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await saveOvertimeSettings({ sessionState: "paused", pausedAtUtc: "2026-10-11T12:30:00Z" });

    const [url, request] = fetchMock.mock.calls[1];
    expect(url).toBe("/settings");
    expect(request.method).toBe("PUT");
    const headers = new Headers(request.headers);
    expect(headers.get("X-EasyLottery-Session-Token")).toBe("obs-control-token");
    expect(headers.get("If-Match")).toBe('W/"settings-1"');
    expect(headers.get("Content-Type")).toContain("yaml");
    expect(request.body).toContain("activeLayoutKey: stage-spotlight");
    expect(request.body).toContain("sessionState: paused");
    expect(request.body).toContain("plannedEndAtUtc: 2026-10-11T15:00:00Z");
    expect(request.body).toContain("showAmounts: true");
  });

  it("allows scoped OBS writes when the projection has no ETag", async () => {
    setSessionTokenFallback("obs-control-token");
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("overtimeOverlay:\n  sessionState: idle\n", { status: 200, headers: { "content-type": "text/yaml" } }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await saveOvertimeSettings({ sessionState: "running" });

    expect(new Headers(fetchMock.mock.calls[1][1]?.headers).has("If-Match")).toBe(false);
  });

  it("does not issue a write when a server-state guard rejects a stale action", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response("overtimeOverlay:\n  sessionState: completed\n", {
      status: 200,
      headers: { "content-type": "text/yaml" }
    }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(saveOvertimeSettings(current => current.sessionState === "running"
      ? { sessionState: "paused" }
      : null)).resolves.toBe(false);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("appends a normalized feed item and keeps the newest 30 events", async () => {
    setSessionTokenFallback("obs-control-token");
    const existing = Array.from({ length: 30 }, (_, index) => ({ id: String(index), source: 3, displayName: `Guest ${index}` }));
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(existing), { status: 200, headers: { "content-type": "application/json" } }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetchMock);

    await appendOvertimeEvent({ source: 3, sourceLabel: "加班開始", displayName: "加班開始", message: "加班台已開始。", occurredAtUtc: "2026-10-11T12:00:00.000Z" });

    const [url, request] = fetchMock.mock.calls[1];
    expect(url).toBe("/api/overtime-feed");
    expect(request.method).toBe("PUT");
    const saved = JSON.parse(request.body);
    expect(saved).toHaveLength(30);
    expect(saved[0]).toMatchObject({ source: 3, sourceLabel: "加班開始", displayName: "加班開始", currency: "TWD" });
    expect(saved.some((item: { id: string }) => item.id === "29")).toBe(false);
  });

  it("normalizes the existing feed endpoint's PascalCase JSON response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify([{
      Id: "event-1",
      Source: 2,
      SourceLabel: "ECPay Donate",
      DisplayName: "Guest",
      Message: "Thanks",
      Amount: 100,
      AmountDisplay: "NT$100",
      Currency: "TWD",
      AvatarUrl: "",
      Color: "#ff85b4",
      OccurredAtUtc: "2026-10-11T12:00:00.000Z",
      SessionEndAtUtc: null,
      ExternalId: "external-1"
    }]), { status: 200, headers: { "content-type": "application/json" } })));

    await expect(readOvertimeFeed()).resolves.toMatchObject([{
      id: "event-1",
      source: 2,
      sourceLabel: "ECPay Donate",
      displayName: "Guest",
      message: "Thanks",
      amount: 100,
      occurredAtUtc: "2026-10-11T12:00:00.000Z"
    }]);
  });

  it("surfaces a scoped API denial without including the token in the error", async () => {
    setSessionTokenFallback("do-not-leak-this-token");
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("forbidden", { status: 403 })));

    await expect(saveOvertimeSettings({ sessionState: "running" })).rejects.toMatchObject({
      name: "ApiError",
      status: 403
    } satisfies Partial<ApiError>);
    await expect(readOvertimeSettings()).rejects.not.toThrow("do-not-leak-this-token");
  });
});
