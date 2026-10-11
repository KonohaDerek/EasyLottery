import { describe, expect, it, vi } from "vitest";
import {
  createInteractionRealtimeController,
  type InteractionConnectionStatus,
  type InteractionHubConnection,
  type InteractionRoundSnapshot
} from "./realtime";

class FakeConnection implements InteractionHubConnection {
  readonly invocations: Array<{ method: string; args: unknown[] }> = [];
  readonly handlers = new Map<string, (...args: unknown[]) => void>();
  reconnecting?: (error?: Error) => void;
  reconnected?: (connectionId?: string) => void | Promise<void>;
  closed?: (error?: Error) => void;
  startError: Error | null = null;
  invokeError: Error | null = null;
  onInvoke: (() => void) | null = null;
  stop = vi.fn(async () => undefined);

  on(methodName: string, callback: (...args: unknown[]) => void): void { this.handlers.set(methodName, callback); }
  onreconnecting(callback: (error?: Error) => void): void { this.reconnecting = callback; }
  onreconnected(callback: (connectionId?: string) => void | Promise<void>): void { this.reconnected = callback; }
  onclose(callback: (error?: Error) => void): void { this.closed = callback; }
  async start(): Promise<void> { if (this.startError) throw this.startError; }
  async invoke(method: string, ...args: unknown[]): Promise<unknown> {
    this.invocations.push({ method, args });
    this.onInvoke?.();
    if (this.invokeError) throw this.invokeError;
    return undefined;
  }
}

const sampleSnapshot: InteractionRoundSnapshot = {
  id: "be8d6b74-3f89-4cc7-8d8a-7418205bf608",
  name: "Friday vote",
  status: "Live",
  type: "Vote",
  voteOptions: ["A", "B"],
  voteCounts: { A: 3, B: 1 },
  leaderboard: []
};

function createCallbacks() {
  const statuses: InteractionConnectionStatus[] = [];
  const snapshots: Array<InteractionRoundSnapshot | null> = [];
  return { statuses, snapshots, callbacks: { onStatus: (value: InteractionConnectionStatus) => statuses.push(value), onSnapshot: (value: InteractionRoundSnapshot | null) => snapshots.push(value) } };
}

describe("interaction OBS SignalR lifecycle", () => {
  it("joins the existing hub and publishes live snapshots", async () => {
    const connection = new FakeConnection();
    const state = createCallbacks();
    const controller = createInteractionRealtimeController(sampleSnapshot.id, "scoped-read-token", state.callbacks, () => connection);

    await controller.start();
    connection.handlers.get("InteractionStateChanged")?.(sampleSnapshot);

    expect(connection.invocations).toEqual([{ method: "JoinRound", args: [sampleSnapshot.id, "scoped-read-token"] }]);
    expect(state.snapshots).toEqual([sampleSnapshot]);
    expect(state.statuses.at(-1)).toBe("connected");
    await controller.dispose();
  });

  it("rejoins the round after reconnect and reports reconnection", async () => {
    const connection = new FakeConnection();
    const state = createCallbacks();
    let currentSnapshot = sampleSnapshot;
    connection.onInvoke = () => connection.handlers.get("InteractionStateChanged")?.(currentSnapshot);
    const controller = createInteractionRealtimeController(sampleSnapshot.id, "scoped-read-token", state.callbacks, () => connection);

    await controller.start();
    currentSnapshot = { ...sampleSnapshot, voteCounts: { A: 4, B: 1 } };
    connection.reconnecting?.();
    await connection.reconnected?.("reconnected-id");

    expect(connection.invocations).toHaveLength(2);
    expect(connection.invocations[1]).toEqual(connection.invocations[0]);
    expect(state.snapshots).toEqual([sampleSnapshot, currentSnapshot]);
    expect(state.statuses).toContain("reconnecting");
    expect(state.statuses.at(-1)).toBe("connected");
    await controller.dispose();
  });

  it("does not open a hub connection without an OBS session token", async () => {
    const state = createCallbacks();
    const factory = vi.fn(() => new FakeConnection());
    const controller = createInteractionRealtimeController(sampleSnapshot.id, null, state.callbacks, factory);

    await controller.start();

    expect(factory).not.toHaveBeenCalled();
    expect(state.statuses).toEqual(["missing-token"]);
  });

  it("reports stale read credentials without exposing the token", async () => {
    const connection = new FakeConnection();
    connection.invokeError = new Error("互動回合 OBS 憑證無效。");
    const state = createCallbacks();
    const controller = createInteractionRealtimeController(sampleSnapshot.id, "secret-value", state.callbacks, () => connection);

    await controller.start();

    expect(state.statuses.at(-1)).toBe("invalid-token");
    expect(state.statuses.join(" ")).not.toContain("secret-value");
    await controller.dispose();
  });

  it("keeps a temporary join failure retryable", async () => {
    const failed = new FakeConnection();
    failed.invokeError = new Error("temporary snapshot storage failure");
    const recovered = new FakeConnection();
    recovered.onInvoke = () => recovered.handlers.get("InteractionStateChanged")?.(sampleSnapshot);
    const connections = [failed, recovered];
    const state = createCallbacks();
    const controller = createInteractionRealtimeController(sampleSnapshot.id, "scoped-read-token", state.callbacks, () => connections.shift()!);

    await controller.start();
    expect(state.statuses.at(-1)).toBe("retryable-error");

    await controller.retry();
    expect(state.statuses.at(-1)).toBe("connected");
    expect(state.snapshots).toEqual([sampleSnapshot]);
    await controller.dispose();
  });

  it("shows a not-found state for empty hub snapshots and a retryable state on close", async () => {
    const connection = new FakeConnection();
    const state = createCallbacks();
    const controller = createInteractionRealtimeController(sampleSnapshot.id, "scoped-read-token", state.callbacks, () => connection);

    connection.onInvoke = () => connection.handlers.get("InteractionStateChanged")?.(null);
    await controller.start();
    expect(state.statuses.at(-1)).toBe("not-found");
    connection.closed?.();

    expect(state.snapshots).toEqual([null]);
    expect(state.statuses).toContain("not-found");
    expect(state.statuses.at(-1)).toBe("disconnected");
    await controller.dispose();
  });
});
