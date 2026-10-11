export interface OvertimeHubLifecycleCallbacks {
  onreconnecting(): void;
  onreconnected(): Promise<void>;
  onclose(): void;
}

export function createCoalescedRefresh(refresh: () => Promise<void>): () => Promise<void> {
  let inFlight: Promise<void> | null = null;
  let refreshQueued = false;
  return () => {
    if (inFlight) {
      refreshQueued = true;
      return inFlight;
    }
    inFlight = (async () => {
      do {
        refreshQueued = false;
        await refresh();
      } while (refreshQueued);
    })().finally(() => { inFlight = null; });
    return inFlight;
  };
}

export function createOvertimeHubLifecycleCallbacks(
  invoke: (method: string, token: string) => Promise<unknown>,
  token: string,
  setStatus: (status: string) => void,
  refresh: () => Promise<void>,
  onError: (cause: unknown) => void
): OvertimeHubLifecycleCallbacks {
  return {
    onreconnecting: () => setStatus("即時連線中斷，正在重連…"),
    onreconnected: async () => {
      try {
        await invoke("Join", token);
        setStatus("即時連線中");
        await refresh();
      } catch (cause) {
        setStatus("即時連線中斷，改用輪詢");
        onError(cause);
      }
    },
    onclose: () => setStatus("即時連線中斷，改用輪詢")
  };
}
