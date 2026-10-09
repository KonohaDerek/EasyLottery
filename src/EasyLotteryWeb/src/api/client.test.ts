import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, apiRequest } from "./client";

afterEach(() => vi.restoreAllMocks());

describe("API client", () => {
  it("passes the session token and returns ETag", async () => {
    const storage = new Map([["easy-lottery.session-token", "test-token"]]);
    vi.stubGlobal("sessionStorage", { getItem: (key: string) => storage.get(key) ?? null });
    vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      expect(new Headers(init?.headers).get("X-EasyLottery-Session-Token")).toBe("test-token");
      return new Response(JSON.stringify({ ok: true }), { status: 200, headers: { ETag: '"v2"', "Content-Type": "application/json" } });
    }));

    const response = await apiRequest<{ ok: boolean }>("/api/settings/test");
    expect(response.data).toEqual({ ok: true });
    expect(response.etag).toBe('"v2"');
  });

  it("exposes structured API errors without logging request data", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ error: "設定版本已變更", code: "etag_conflict" }), { status: 409, headers: { "Content-Type": "application/json" } })));

    await expect(apiRequest("/api/settings/test", { method: "PUT", body: JSON.stringify({ secret: "not logged" }) }))
      .rejects.toMatchObject({ status: 409, code: "etag_conflict", message: "設定版本已變更" } satisfies Partial<ApiError>);
  });
});
