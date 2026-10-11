import { describe, expect, it, vi } from "vitest";
import { createCoalescedRefresh, createOvertimeHubLifecycleCallbacks } from "./realtime";

describe("coalesced refresh", () => {
  it("serializes overlapping requests and performs one follow-up refresh", async () => {
    let resolveFirst!: () => void;
    const first = new Promise<void>(resolve => { resolveFirst = resolve; });
    let active = 0;
    let maximumActive = 0;
    const refresh = vi.fn(async () => {
      active += 1;
      maximumActive = Math.max(maximumActive, active);
      if (refresh.mock.calls.length === 1) await first;
      active -= 1;
    });
    const request = createCoalescedRefresh(refresh);

    const initial = request();
    await Promise.resolve();
    const eventRefresh = request();
    const pollRefresh = request();
    expect(refresh).toHaveBeenCalledTimes(1);
    resolveFirst();
    await Promise.all([initial, eventRefresh, pollRefresh]);

    expect(refresh).toHaveBeenCalledTimes(2);
    expect(maximumActive).toBe(1);
  });
});

describe("overtime SignalR reconnect lifecycle", () => {
  it("re-joins the scoped hub and refreshes the authoritative snapshot after reconnect", async () => {
    const calls: string[] = [];
    const statuses: string[] = [];
    const refresh = vi.fn(async () => { calls.push("refresh"); });
    const onError = vi.fn();
    const callbacks = createOvertimeHubLifecycleCallbacks(
      async (method, token) => { calls.push(`${method}:${token}`); },
      "obs-read-token",
      status => statuses.push(status),
      refresh,
      onError
    );

    callbacks.onreconnecting();
    await callbacks.onreconnected();
    callbacks.onclose();

    expect(calls).toEqual(["Join:obs-read-token", "refresh"]);
    expect(statuses).toEqual(["即時連線中斷，正在重連…", "即時連線中", "即時連線中斷，改用輪詢"]);
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(onError).not.toHaveBeenCalled();
  });

  it("surfaces a failed re-join and does not refresh from an unjoined connection", async () => {
    const failure = new Error("join rejected");
    const onError = vi.fn();
    const refresh = vi.fn(async () => undefined);
    const callbacks = createOvertimeHubLifecycleCallbacks(
      async () => { throw failure; },
      "obs-read-token",
      () => undefined,
      refresh,
      onError
    );

    await callbacks.onreconnected();

    expect(refresh).not.toHaveBeenCalled();
    expect(onError).toHaveBeenCalledWith(failure);
  });
});
