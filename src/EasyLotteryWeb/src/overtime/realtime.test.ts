import { describe, expect, it, vi } from "vitest";
import { createCoalescedRefresh, createOvertimeHubLifecycleCallbacks, createSerialActionQueue } from "./realtime";

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

  it("lets awaiting actions finish while continuous polling schedules later refreshes", async () => {
    let active = 0;
    let maximumActive = 0;
    let request!: () => Promise<void>;
    const refresh = vi.fn(async () => {
      active += 1;
      maximumActive = Math.max(maximumActive, active);
      await new Promise(resolve => setTimeout(resolve, 1));
      if (refresh.mock.calls.length < 8) void request();
      active -= 1;
    });
    request = createCoalescedRefresh(refresh);

    let callsWhenFirstPromiseResolved = 0;
    await Promise.race([
      request().then(() => { callsWhenFirstPromiseResolved = refresh.mock.calls.length; }),
      new Promise((_, reject) => setTimeout(() => reject(new Error("refresh did not settle")), 100))
    ]);
    await new Promise(resolve => setTimeout(resolve, 30));

    expect(callsWhenFirstPromiseResolved).toBeLessThanOrEqual(2);
    expect(refresh).toHaveBeenCalledTimes(8);
    expect(maximumActive).toBe(1);
  });

  it("cancels a queued background refresh and ignores future requests after unmount", async () => {
    let request!: ReturnType<typeof createCoalescedRefresh>;
    const refresh = vi.fn(async () => {
      if (refresh.mock.calls.length <= 2) void request();
    });
    request = createCoalescedRefresh(refresh);

    await request();
    expect(refresh).toHaveBeenCalledTimes(2);
    request.cancel();
    await new Promise(resolve => setTimeout(resolve, 10));
    await request();

    expect(refresh).toHaveBeenCalledTimes(2);
  });
});

describe("serialized overtime settings actions", () => {
  it("keeps separate writes in order and continues after a rejected write", async () => {
    let releaseFirst!: () => void;
    const firstGate = new Promise<void>(resolve => { releaseFirst = resolve; });
    const order: string[] = [];
    const serialize = createSerialActionQueue();
    const first = serialize(async () => {
      order.push("completion:start");
      await firstGate;
      order.push("completion:end");
    });
    const second = serialize(async () => { order.push("manual:end"); });

    await Promise.resolve();
    expect(order).toEqual(["completion:start"]);
    releaseFirst();
    await Promise.all([first, second]);
    expect(order).toEqual(["completion:start", "completion:end", "manual:end"]);

    const failed = serialize(async () => { throw new Error("temporary write failure"); });
    const afterFailure = serialize(async () => { order.push("retry"); });
    await expect(failed).rejects.toThrow("temporary write failure");
    await afterFailure;
    expect(order.at(-1)).toBe("retry");
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
