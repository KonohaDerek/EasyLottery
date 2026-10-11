import { afterEach, describe, expect, it, vi } from "vitest";
import { apiRequest, setSessionTokenFallback } from "../api/client";
import { bootstrapObsSessionToken, extractObsSessionToken, readObsSessionToken } from "./session-token";

afterEach(() => {
  vi.unstubAllGlobals();
  setSessionTokenFallback(null);
});

describe("OBS session token bootstrap", () => {
  it("extracts a fragment token and preserves controls query parameters", () => {
    const result = extractObsSessionToken("https://example.test/obs/roulette/abc?controls=1#sessionToken=secret");
    expect(result).toEqual({
      token: "secret",
      cleanedUrl: "/obs/roulette/abc?controls=1",
      foundToken: true
    });
  });

  it("prefers a query token and removes credentials from both URL parts", () => {
    const result = extractObsSessionToken("https://example.test/obs/roulette/abc?sessionToken=query&motion=reduce#sessionToken=fragment&quality=low");
    expect(result.token).toBe("query");
    expect(result.cleanedUrl).toBe("/obs/roulette/abc?motion=reduce#quality=low");
  });

  it("does not consume tokens outside OBS routes", () => {
    expect(extractObsSessionToken("https://example.test/login?sessionToken=secret")).toEqual({
      token: null,
      cleanedUrl: "/login?sessionToken=secret",
      foundToken: false
    });
  });

  it("scrubs the URL and keeps using the token when storage is unavailable", async () => {
    const replaceState = vi.fn();
    vi.stubGlobal("window", {
      location: { href: "https://example.test/obs/roulette/abc?controls=1#sessionToken=secret" },
      history: { state: null, replaceState },
      sessionStorage: {
        setItem: () => { throw new DOMException("Storage disabled", "SecurityError"); },
        getItem: () => "previous-token"
      }
    });
    vi.stubGlobal("document", { title: "OBS" });
    vi.stubGlobal("sessionStorage", {
      setItem: () => { throw new DOMException("Storage disabled", "SecurityError"); },
      getItem: () => "previous-token"
    });
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: { get: (name: string) => name.toLowerCase() === "content-type" ? "application/json" : null },
      json: async () => ({ accepted: true })
    });
    vi.stubGlobal("fetch", fetchMock);

    expect(() => bootstrapObsSessionToken()).not.toThrow();
    expect(replaceState).toHaveBeenCalledWith(null, "OBS", "/obs/roulette/abc?controls=1");
    expect(readObsSessionToken()).toBe("secret");
    await apiRequest("/api/test");
    expect(new Headers(fetchMock.mock.calls[0][1].headers).get("X-EasyLottery-Session-Token")).toBe("secret");
  });
});
