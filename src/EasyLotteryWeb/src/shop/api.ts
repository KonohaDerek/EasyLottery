import { apiRequest } from "../api/client";

export type HubLinkStatus = "not_linked" | "linked" | "credentials_invalid" | "hub_unreachable";

export interface HubLinkSettings {
  hubBaseUrl: string;
  siteClientId: string;
  hasClientSecret: boolean;
  status: HubLinkStatus;
  tenantId: string | null;
  lastPublishAt: string | null;
}

export interface HubLinkWriteRequest {
  hubBaseUrl: string;
  siteClientId: string;
  clientSecret: string;
}

export async function isShopEnabled(fetcher: typeof fetch = fetch): Promise<boolean> {
  try {
    const response = await fetcher("/api/shop/status", { cache: "no-store" });
    if (!response.ok) return false;
    const payload = await response.json() as { enabled?: unknown };
    return payload.enabled === true;
  } catch {
    return false;
  }
}

export function isShopFeaturePath(path: string): boolean {
  return path === "/shop"
    || path.startsWith("/shop/")
    || path === "/system/card-hub"
    || path.startsWith("/system/card-hub/");
}

export async function loadHubLinkSettings(): Promise<HubLinkSettings> {
  const { data } = await apiRequest<HubLinkSettings>("/api/shop/hub-link", { cache: "no-store" });
  return data;
}

export async function saveHubLinkSettings(request: HubLinkWriteRequest): Promise<HubLinkSettings> {
  const { data } = await apiRequest<HubLinkSettings>("/api/shop/hub-link", {
    method: "PUT",
    body: JSON.stringify(request)
  });
  return data;
}

export async function testHubLink(request: HubLinkWriteRequest): Promise<HubLinkSettings> {
  const { data } = await apiRequest<HubLinkSettings>("/api/shop/hub-link/test", {
    method: "POST",
    body: JSON.stringify(request)
  });
  return data;
}

