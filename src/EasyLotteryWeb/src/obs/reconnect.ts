import { HubConnectionState, type HubConnection } from "@microsoft/signalr";

export async function reconnectLiveDraw(
  connection: Pick<HubConnection, "state" | "start">,
  resetRevision: () => void,
  joinAndRefresh: () => Promise<void>
): Promise<void> {
  if (connection.state === HubConnectionState.Disconnected) {
    resetRevision();
    await connection.start();
  }
  await joinAndRefresh();
}
