import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/client";
import {
  buildOvertimePreviewUrl,
  createOvertimePreviewToken,
  extendPlannedEndForReward,
  loadOvertimeSettings,
  mergeOvertimeSettings,
  saveOvertimeSettings,
  type OvertimeSettings
} from "./admin-api";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

function settings(overrides: Partial<OvertimeSettings> = {}): OvertimeSettings {
  return {
    isEnabled: true, title: "加班台", subtitle: "Live support", themeKey: "festival-stage", maxVisibleItems: 8,
    showAmounts: true, showAvatars: true, demoSuperChatAmount: 1000, demoEcpayAmount: 2000,
    messageTemplateKey: "default", textAnimationKey: "slide-in", streamStartedAtUtc: null,
    plannedEndAtUtc: null, sessionState: "idle", pausedAtUtc: null, completedAtUtc: null,
    enableCompletionFireworks: true, completionFireworksStyle: "classic", completionFireworksDurationSeconds: 6,
    completionFireworksGifUrl: "", supportMessageVisibleSeconds: 8,
    rewardRules: [{ isEnabled: true, amountThreshold: 100, addHours: 0, addMinutes: 10, label: "入門加班" }], ...overrides
  };
}

describe("overtime admin settings API", () => {
  it("merges editable fields onto the newest server snapshot and preserves OBS session state", () => {
    const latest = settings({
      title: "Updated on server", sessionState: "paused", streamStartedAtUtc: "2026-10-12T10:00:00Z",
      pausedAtUtc: "2026-10-12T10:30:00Z", completedAtUtc: "2026-10-12T11:00:00Z", vendorExtension: "preserved"
    });
    const draft = settings({ title: "New title", sessionState: "idle", streamStartedAtUtc: null, pausedAtUtc: null, completedAtUtc: null });
    const merged = mergeOvertimeSettings(latest, draft);
    expect(merged).toMatchObject({
      title: "New title", sessionState: "paused", streamStartedAtUtc: "2026-10-12T10:00:00Z",
      pausedAtUtc: "2026-10-12T10:30:00Z", completedAtUtc: "2026-10-12T11:00:00Z", vendorExtension: "preserved"
    });
    expect(merged.rewardRules).not.toBe(draft.rewardRules);
    expect(merged.rewardRules[0]).not.toBe(draft.rewardRules[0]);
  });

  it("preserves a newer server planned end when the administrator left that field untouched", () => {
    const latest = settings({ plannedEndAtUtc: "2026-10-12T13:00:00Z" });
    const draft = settings({ title: "New title", plannedEndAtUtc: "2026-10-12T12:00:00Z" });
    expect(mergeOvertimeSettings(latest, draft, { preserveLatestPlannedEnd: true }).plannedEndAtUtc)
      .toBe("2026-10-12T13:00:00Z");
    expect(mergeOvertimeSettings(latest, { ...draft, plannedEndAtUtc: "2026-10-12T14:00:00Z" }).plannedEndAtUtc)
      .toBe("2026-10-12T14:00:00Z");
  });

  it("re-reads settings before save and sends the latest ETag", async () => {
    const latest = settings({ sessionState: "running", streamStartedAtUtc: "2026-10-12T08:00:00Z" });
    const draft = settings({ title: "Updated", sessionState: "idle", streamStartedAtUtc: null });
    let calls = 0;
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-token" });
    vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      calls++;
      expect(new Headers(init?.headers).get("X-EasyLottery-Session-Token")).toBe("admin-token");
      if (calls === 1) return new Response(JSON.stringify(latest), { status: 200, headers: { "Content-Type": "application/json", ETag: '"latest"' } });
      expect(init?.method).toBe("PUT");
      expect(new Headers(init?.headers).get("If-Match")).toBe('"latest"');
      expect(JSON.parse(String(init?.body))).toMatchObject({ title: "Updated", sessionState: "running", streamStartedAtUtc: "2026-10-12T08:00:00Z" });
      return new Response(JSON.stringify(latest), { status: 200, headers: { "Content-Type": "application/json", ETag: '"saved"' } });
    }));
    await expect(saveOvertimeSettings(draft)).resolves.toMatchObject({ etag: '"saved"' });
    expect(calls).toBe(2);
  });

  it("does not revert a server-side planned-end extension when saving an unrelated field", async () => {
    const latest = settings({ plannedEndAtUtc: "2026-10-12T13:00:00Z" });
    const draft = settings({ title: "Updated", plannedEndAtUtc: "2026-10-12T12:00:00Z" });
    let calls = 0;
    vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      calls++;
      if (calls === 1) return new Response(JSON.stringify(latest), { status: 200, headers: { "Content-Type": "application/json", ETag: '"latest"' } });
      expect(JSON.parse(String(init?.body))).toMatchObject({ title: "Updated", plannedEndAtUtc: "2026-10-12T13:00:00Z" });
      return new Response(JSON.stringify(latest), { status: 200, headers: { "Content-Type": "application/json", ETag: '"saved"' } });
    }));

    await saveOvertimeSettings(draft, { preserveLatestPlannedEnd: true });
    expect(calls).toBe(2);
  });

  it("surfaces stale-save conflicts and does not report success", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({ error: "版本已變更" }), {
      status: 409, headers: { "Content-Type": "application/json" }
    })));
    await expect(saveOvertimeSettings(settings())).rejects.toBeInstanceOf(ApiError);
  });

  it("extends only the best enabled reward rule while the session is running", () => {
    const current = settings({
      sessionState: "running", plannedEndAtUtc: "2026-10-12T12:00:00.000Z",
      rewardRules: [
        { isEnabled: true, amountThreshold: 100, addHours: 0, addMinutes: 5, label: "low" },
        { isEnabled: true, amountThreshold: 500, addHours: 1, addMinutes: 0, label: "high" },
        { isEnabled: false, amountThreshold: 1000, addHours: 9, addMinutes: 0, label: "disabled" }
      ]
    });
    expect(extendPlannedEndForReward(current, 600, new Date("2026-10-12T10:00:00.000Z")))
      .toBe("2026-10-12T13:00:00.000Z");
    expect(extendPlannedEndForReward({ ...current, sessionState: "paused" }, 600))
      .toBe(current.plannedEndAtUtc);
  });

  it("requests a narrowly scoped overtime preview token and keeps it in the URL fragment", async () => {
    vi.stubGlobal("sessionStorage", { getItem: () => "admin-token" });
    vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) => {
      expect(init?.method).toBe("POST");
      expect(JSON.parse(String(init?.body))).toEqual({ resourceKind: "overtime", resourceId: "default", scopes: ["read", "control"] });
      return new Response(JSON.stringify({ token: "scoped-secret", expiresAtUtc: "2026-10-12T12:00:00Z" }), { status: 200, headers: { "Content-Type": "application/json" } });
    }));
    await expect(createOvertimePreviewToken()).resolves.toMatchObject({ data: { token: "scoped-secret" } });
    const url = new URL(buildOvertimePreviewUrl("https://example.test", "scoped-secret"));
    expect(url.pathname).toBe("/obs/overtime");
    expect(url.search).toBe("?controls=1");
    expect(url.hash).toBe("#sessionToken=scoped-secret");
  });
});
