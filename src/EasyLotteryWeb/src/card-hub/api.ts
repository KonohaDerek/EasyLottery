import { apiRequest } from "../api/client";

export type CardHubLinkStatus = "not_linked" | "linked" | "credentials_invalid" | "unreachable";

export interface CardHubLinkSettings {
  status: CardHubLinkStatus;
  hubUrl: string;
  siteClientId: string;
  hasSecret: boolean;
  tenantId: string | null;
  tenantName: string | null;
  lastCheckedAtUtc: string | null;
  lastPublishAtUtc: string | null;
}

export interface CardHubLinkUpdate {
  hubUrl: string;
  siteClientId: string;
  clientSecret: string;
}

export interface ShopFeatureStatus {
  enabled: boolean;
}

export async function loadCardHubLink(): Promise<CardHubLinkSettings> {
  return (await apiRequest<CardHubLinkSettings>("/api/settings/card-hub")).data;
}

export async function saveCardHubLink(value: CardHubLinkUpdate): Promise<CardHubLinkSettings> {
  return (await apiRequest<CardHubLinkSettings>("/api/settings/card-hub", {
    method: "PUT",
    body: JSON.stringify(value)
  })).data;
}

export async function testCardHubLink(): Promise<CardHubLinkSettings> {
  return (await apiRequest<CardHubLinkSettings>("/api/settings/card-hub/test", { method: "POST" })).data;
}

export async function unlinkCardHub(): Promise<void> {
  await apiRequest<void>("/api/settings/card-hub", { method: "DELETE" });
}

export async function loadShopFeatureStatus(): Promise<ShopFeatureStatus> {
  const response = await fetch("/api/shop/feature-status", { cache: "no-store" });
  if (!response.ok) return { enabled: false };
  const status: unknown = await response.json().catch(() => null);
  return status && typeof status === "object" && "enabled" in status && status.enabled === true
    ? { enabled: true }
    : { enabled: false };
}
