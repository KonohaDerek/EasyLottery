import { apiRequest, type ApiResponse } from "../api/client";

export type PlatformName = "YouTube" | "Twitch" | "Discord";
export type CredentialAction = "unchanged" | "replace" | "clear";
export type PlatformConnectionState = "unconfigured" | "adapter_unavailable" | "unverified" | "connected" | "failed" | string;

export interface PlatformConnection {
  platform: PlatformName;
  state: PlatformConnectionState;
  configured: boolean;
  channelScope: string;
  apiKey: string | null;
  accessToken: string | null;
}

export interface PlatformConnectionDraft {
  platform: PlatformName;
  state: string;
  configured: boolean;
  channelScope: string;
  credentialValue: string;
  credentialAction: CredentialAction;
}

export interface PlatformConnectionUpdate {
  apiKey: string | null;
  accessToken: string | null;
  channelScope: string;
  degrade: boolean;
}

export function platformStatusLabel(state: PlatformConnectionState): string {
  switch (state) {
    case "unconfigured": return "尚未設定憑證";
    case "adapter_unavailable": return "adapter 尚未實作";
    case "unverified": return "已設定，尚未驗證連線";
    case "connected": return "已驗證連線";
    case "failed": return "連線驗證失敗";
    default: return "連線狀態未知";
  }
}

export function platformStatusColor(state: PlatformConnectionState): string {
  switch (state) {
    case "connected": return "success";
    case "failed": return "error";
    case "unverified": return "warning";
    case "adapter_unavailable": return "info";
    default: return "default";
  }
}

export function toPlatformDraft(connection: PlatformConnection): PlatformConnectionDraft {
  return {
    platform: connection.platform,
    state: connection.state,
    configured: connection.configured,
    channelScope: connection.channelScope ?? "",
    credentialValue: "",
    credentialAction: "unchanged"
  };
}

export function buildPlatformUpdate(draft: PlatformConnectionDraft): PlatformConnectionUpdate {
  const credential = draft.credentialAction === "clear"
    ? ""
    : draft.credentialAction === "replace"
      ? draft.credentialValue
      : null;
  return {
    apiKey: draft.platform === "YouTube" ? credential : null,
    accessToken: draft.platform === "YouTube" ? null : credential,
    channelScope: draft.channelScope,
    degrade: false
  };
}

export async function loadPlatformConnections(): Promise<ApiResponse<PlatformConnection[]>> {
  return apiRequest<PlatformConnection[]>("/api/interactions/connections");
}

// The current server returns one platform status; also accept a full list for compatible
// implementations. Return null when the saved platform is missing so callers show an
// unknown status instead of reusing a pre-save state.
export function savedPlatformStatus(
  platform: PlatformName,
  payload: PlatformConnection | PlatformConnection[] | null
): PlatformConnection | null {
  const saved = Array.isArray(payload)
    ? payload.find(connection => connection.platform === platform)
    : payload?.platform === platform ? payload : undefined;
  return saved ?? null;
}

export async function savePlatformConnection(
  draft: PlatformConnectionDraft,
  etag: string
): Promise<ApiResponse<PlatformConnection | null>> {
  const response = await apiRequest<PlatformConnection | PlatformConnection[] | null>(`/api/interactions/connections/${encodeURIComponent(draft.platform)}`, {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: JSON.stringify(buildPlatformUpdate(draft))
  });
  return { data: savedPlatformStatus(draft.platform, response.data), etag: response.etag };
}
