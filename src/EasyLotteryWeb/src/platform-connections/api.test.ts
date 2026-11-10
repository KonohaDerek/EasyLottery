import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import {
  buildPlatformUpdate,
  loadPlatformConnections,
  platformStatusColor,
  platformStatusLabel,
  savePlatformConnection,
  savedPlatformStatus,
  toPlatformDraft,
  type PlatformConnectionDraft
} from "./api";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

const unchangedYoutube: PlatformConnectionDraft = {
  platform: "YouTube", state: "adapter_unavailable", configured: true, channelScope: "channel-1",
  credentialValue: "", credentialAction: "unchanged"
};

describe("platform connection API", () => {
  it("never copies the server's masked secret into editable form state", () => {
    const draft = toPlatformDraft({
      platform: "YouTube", state: "adapter_unavailable", configured: true, channelScope: "channel-1",
      apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__", accessToken: null
    });
    expect(draft.credentialValue).toBe("");
    expect(JSON.stringify(draft)).not.toContain("__EASYLOTTERY_SECRET_UNCHANGED__");
    expect(buildPlatformUpdate(draft)).toEqual({ apiKey: null, accessToken: null, channelScope: "channel-1", degrade: false });
  });

  it("sends replacement and explicit clear values only for the platform credential field", () => {
    expect(buildPlatformUpdate({ ...unchangedYoutube, credentialValue: "new-key", credentialAction: "replace" }))
      .toEqual({ apiKey: "new-key", accessToken: null, channelScope: "channel-1", degrade: false });
    expect(buildPlatformUpdate({ ...unchangedYoutube, credentialAction: "clear" }))
      .toEqual({ apiKey: "", accessToken: null, channelScope: "channel-1", degrade: false });
    expect(buildPlatformUpdate({ ...unchangedYoutube, platform: "Twitch", credentialValue: "token", credentialAction: "replace" }))
      .toEqual({ apiKey: null, accessToken: "token", channelScope: "channel-1", degrade: false });
  });

  it("loads with the admin session header and returns the no-store ETag", async () => {
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-token" });
    vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      expect(new Headers(init?.headers).get("X-EasyLottery-Session-Token")).toBe("admin-token");
      return new Response("[]", { status: 200, headers: { "Content-Type": "application/json", ETag: '"v4"' } });
    }));
    await expect(loadPlatformConnections()).resolves.toMatchObject({ data: [], etag: '"v4"' });
  });

  it("uses If-Match and preserves unchanged credentials with null fields", async () => {
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-token" });
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      expect(new URL(String(input), "https://example.test").pathname).toBe("/api/interactions/connections/YouTube");
      expect(init?.method).toBe("PUT");
      const headers = new Headers(init?.headers);
      expect(headers.get("If-Match")).toBe('"v4"');
      expect(headers.get("X-EasyLottery-Session-Token")).toBe("admin-token");
      expect(JSON.parse(String(init?.body))).toEqual({ apiKey: null, accessToken: null, channelScope: "channel-1", degrade: false });
      return new Response(JSON.stringify({ platform: "YouTube", state: "adapter_unavailable", configured: true, channelScope: "channel-1", apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__", accessToken: null }), {
        status: 200, headers: { "Content-Type": "application/json", ETag: '"v5"' }
      });
    }));
    await expect(savePlatformConnection(unchangedYoutube, '"v4"')).resolves.toMatchObject({ etag: '"v5"' });
  });

  it("surfaces stale ETag conflicts without claiming success", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ error: "版本已變更" }), {
      status: 409, headers: { "Content-Type": "application/json" }
    })));
    await expect(savePlatformConnection(unchangedYoutube, '"stale"')).rejects.toBeInstanceOf(ApiError);
    await expect(savePlatformConnection(unchangedYoutube, '"stale"')).rejects.toMatchObject({ status: 409 });
  });

  it("uses explicit text and distinct visual states without inferring connectivity from configuration", () => {
    expect(platformStatusLabel("unconfigured")).toBe("尚未設定憑證");
    expect(platformStatusLabel("adapter_unavailable")).toBe("adapter 尚未實作");
    expect(platformStatusLabel("unverified")).toBe("已設定，尚未驗證連線");
    expect(platformStatusLabel("failed")).toBe("連線驗證失敗");
    expect(platformStatusLabel("connected")).toBe("已驗證連線");
    expect(platformStatusLabel("unexpected")).toBe("連線狀態未知");
    expect(platformStatusColor("connected")).toBe("success");
    expect(platformStatusColor("failed")).toBe("error");
    expect(platformStatusColor("unverified")).toBe("warning");
    expect(platformStatusColor("adapter_unavailable")).toBe("info");
    expect(platformStatusColor("unconfigured")).toBe("default");
    expect(platformStatusColor("unexpected")).toBe("default");
  });

  it("takes the saved platform's fresh status from a full-list save response", () => {
    const list = [
      { platform: "YouTube" as const, state: "adapter_unavailable", configured: true, channelScope: "c", apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__", accessToken: null },
      { platform: "Twitch" as const, state: "unconfigured", configured: false, channelScope: "", apiKey: null, accessToken: null }
    ];
    expect(savedPlatformStatus("Twitch", list)).toMatchObject({ state: "unconfigured", configured: false });
    expect(savedPlatformStatus("YouTube", list[0])).toMatchObject({ state: "adapter_unavailable", configured: true });
    expect(savedPlatformStatus("Discord", list)).toBeNull();
    expect(savedPlatformStatus("Discord", null)).toBeNull();
  });

  it("returns only the saved platform status when the server answers with the full list", async () => {
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-token" });
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify([
      { platform: "YouTube", state: "adapter_unavailable", configured: true, channelScope: "channel-1", apiKey: "__EASYLOTTERY_SECRET_UNCHANGED__", accessToken: null },
      { platform: "Twitch", state: "failed", configured: true, channelScope: "", apiKey: null, accessToken: "__EASYLOTTERY_SECRET_UNCHANGED__" }
    ]), { status: 200, headers: { "Content-Type": "application/json", ETag: '"v6"' } })));
    await expect(savePlatformConnection(unchangedYoutube, '"v5"'))
      .resolves.toMatchObject({ etag: '"v6"', data: { platform: "YouTube", state: "adapter_unavailable", configured: true } });
  });
});
