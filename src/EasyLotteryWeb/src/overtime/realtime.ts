export interface OvertimeHubLifecycleCallbacks {
  onreconnecting(): void;
  onreconnected(): Promise<void>;
  onclose(): void;
}

export interface CoalescedRefresh {
  (): Promise<void>;
  cancel(): void;
}

export function createCoalescedRefresh(refresh: () => Promise<void>): CoalescedRefresh {
  let inFlight: Promise<void> | null = null;
  let refreshQueued = false;
  let cancelled = false;
  let rescheduleHandle: ReturnType<typeof setTimeout> | undefined;
  const request: CoalescedRefresh = () => {
    if (cancelled) return Promise.resolve();
    if (inFlight) {
      refreshQueued = true;
      return inFlight;
    }
    if (rescheduleHandle !== undefined) {
      clearTimeout(rescheduleHandle);
      rescheduleHandle = undefined;
    }
    refreshQueued = false;
    inFlight = Promise.resolve().then(async () => {
      await refresh();
      if (refreshQueued) {
        refreshQueued = false;
        await refresh();
      }
    }).finally(() => {
      inFlight = null;
      if (refreshQueued && !cancelled) {
        rescheduleHandle = setTimeout(() => {
          rescheduleHandle = undefined;
          void request();
        }, 0);
      }
    });
    return inFlight;
  };
  request.cancel = () => {
    cancelled = true;
    refreshQueued = false;
    if (rescheduleHandle !== undefined) {
      clearTimeout(rescheduleHandle);
      rescheduleHandle = undefined;
    }
  };
  return request;
}

export function createSerialActionQueue() {
  let tail = Promise.resolve();
  return function serialize<T>(action: () => Promise<T>): Promise<T> {
    const result = tail.then(action);
    tail = result.then(() => undefined, () => undefined);
    return result;
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
