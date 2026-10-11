import { HubConnectionState } from "@microsoft/signalr";
import { describe, expect, it, vi } from "vitest";
import { reconnectLiveDraw } from "./reconnect";

describe("manual Live Draw reconnect", () => {
  it("starts a disconnected hub, resets the revision, then rejoins and refreshes", async () => {
    const connection = { state: HubConnectionState.Disconnected, start: vi.fn().mockResolvedValue(undefined) };
    const resetRevision = vi.fn();
    const joinAndRefresh = vi.fn().mockResolvedValue(undefined);

    await reconnectLiveDraw(connection, resetRevision, joinAndRefresh);

    expect(connection.start).toHaveBeenCalledOnce();
    expect(resetRevision).toHaveBeenCalledOnce();
    expect(joinAndRefresh).toHaveBeenCalledOnce();
  });

  it("reuses a connected hub after a failed rejoin without restarting it", async () => {
    const connection = { state: HubConnectionState.Connected, start: vi.fn().mockResolvedValue(undefined) };
    const resetRevision = vi.fn();
    const joinAndRefresh = vi.fn().mockResolvedValue(undefined);

    await reconnectLiveDraw(connection, resetRevision, joinAndRefresh);

    expect(connection.start).not.toHaveBeenCalled();
    expect(resetRevision).not.toHaveBeenCalled();
    expect(joinAndRefresh).toHaveBeenCalledOnce();
  });
});
