export interface OvertimeHubLifecycleCallbacks {
  onreconnecting(): void;
  onreconnected(): Promise<void>;
  onclose(): void;
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
