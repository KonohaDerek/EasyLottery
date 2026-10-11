import { HubConnectionBuilder, type HubConnection } from "@microsoft/signalr";

export interface InteractionRoundSnapshot {
  id: string;
  name: string;
  status: string;
  type: string;
  voteOptions: string[];
  voteCounts: Record<string, number>;
  leaderboard: Array<{ audienceProfileId: string; displayName: string; points: number }>;
}

export type InteractionConnectionStatus = "connecting" | "connected" | "reconnecting" | "disconnected" | "retryable-error" | "missing-token" | "invalid-token" | "not-found";

export interface InteractionHubConnection {
  on(methodName: string, newMethod: (...args: unknown[]) => void): void;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void | Promise<void>): void;
  onclose(callback: (error?: Error) => void): void;
  start(): Promise<void>;
  invoke(methodName: string, ...args: unknown[]): Promise<unknown>;
  stop(): Promise<void>;
}

export interface InteractionRealtimeCallbacks {
  onSnapshot(snapshot: InteractionRoundSnapshot | null): void;
  onStatus(status: InteractionConnectionStatus): void;
}

export interface InteractionRealtimeController {
  start(): Promise<void>;
  retry(): Promise<void>;
  dispose(): Promise<void>;
}

type ConnectionFactory = () => InteractionHubConnection;

function createHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl("/hubs/interactions")
    .withAutomaticReconnect()
    .build();
}

export function createInteractionRealtimeController(
  roundId: string,
  sessionToken: string | null,
  callbacks: InteractionRealtimeCallbacks,
  createConnection: ConnectionFactory = createHubConnection
): InteractionRealtimeController {
  let connection: InteractionHubConnection | null = null;
  let attempt = 0;
  let disposed = false;
  let missingRound = false;

  const isCurrent = (candidate: number): boolean => !disposed && candidate === attempt;

  async function join(current: InteractionHubConnection, candidate: number): Promise<void> {
    if (!sessionToken) {
      if (isCurrent(candidate)) callbacks.onStatus("missing-token");
      return;
    }
    try {
      await current.invoke("JoinRound", roundId, sessionToken);
      if (isCurrent(candidate) && !missingRound) callbacks.onStatus("connected");
    } catch (cause) {
      // Only the hub's known credential rejection is terminal; keep other invoke failures retryable.
      const message = cause instanceof Error ? cause.message : "";
      if (isCurrent(candidate)) callbacks.onStatus(message.includes("互動回合 OBS 憑證無效。") ? "invalid-token" : "retryable-error");
    }
  }

  async function start(): Promise<void> {
    const candidate = ++attempt;
    if (!roundId || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(roundId)) {
      callbacks.onStatus("not-found");
      return;
    }
    if (!sessionToken) {
      callbacks.onStatus("missing-token");
      return;
    }

    callbacks.onStatus("connecting");
    missingRound = false;
    const next = createConnection();
    connection = next;
    next.on("InteractionStateChanged", (...args: unknown[]) => {
      if (!isCurrent(candidate)) return;
      const snapshot = (args[0] ?? null) as InteractionRoundSnapshot | null;
      callbacks.onSnapshot(snapshot);
      missingRound = snapshot === null;
      callbacks.onStatus(missingRound ? "not-found" : "connected");
    });
    next.onreconnecting(() => {
      if (isCurrent(candidate)) callbacks.onStatus("reconnecting");
    });
    next.onreconnected(async () => {
      if (!isCurrent(candidate)) return;
      callbacks.onStatus("reconnecting");
      await join(next, candidate);
    });
    next.onclose(() => {
      if (isCurrent(candidate)) callbacks.onStatus("disconnected");
    });

    try {
      await next.start();
      if (isCurrent(candidate)) await join(next, candidate);
    } catch {
      if (isCurrent(candidate)) callbacks.onStatus("disconnected");
    }
  }

  return {
    start,
    async retry() {
      const previous = connection;
      connection = null;
      if (previous) await previous.stop().catch(() => undefined);
      if (!disposed) await start();
    },
    async dispose() {
      disposed = true;
      attempt++;
      const previous = connection;
      connection = null;
      if (previous) await previous.stop().catch(() => undefined);
    }
  };
}
