import { apiRequest, type ApiResponse } from "../api/client";

export type PlatformName = "YouTube" | "Twitch" | "Discord";
export type CredentialAction = "unchanged" | "replace" | "clear";

export interface PlatformConnection {
  platform: PlatformName;
  state: "configured" | "degraded" | "unconfigured" | string;
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

export function savePlatformConnection(
  draft: PlatformConnectionDraft,
  etag: string
): Promise<ApiResponse<PlatformConnection>> {
  return apiRequest<PlatformConnection>(`/api/interactions/connections/${encodeURIComponent(draft.platform)}`, {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: JSON.stringify(buildPlatformUpdate(draft))
  });
}
