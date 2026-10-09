import { apiRequest, type ApiResponse } from "./client";

export interface ProviderSettings {
  name?: string;
  isEnabled?: boolean;
  environment?: string;
  [key: string]: unknown;
}

export interface PaymentSettings {
  donationIntegration: Record<string, ProviderSettings>;
  publicCallback: Record<string, unknown>;
  mailDelivery: Record<string, unknown>;
  youTube: Record<string, unknown>;
  enableYouTubeSuperChat: boolean;
  resultNotificationEmail: string;
  [key: string]: unknown;
}

export function emptyPaymentSettings(): PaymentSettings {
  return {
    donationIntegration: {},
    publicCallback: {},
    mailDelivery: {},
    youTube: {},
    enableYouTubeSuperChat: false,
    resultNotificationEmail: ""
  };
}

export function loadPaymentSettings(): Promise<ApiResponse<PaymentSettings>> {
  return apiRequest<PaymentSettings>("/api/settings/payments");
}

export function savePaymentSettings(value: PaymentSettings, etag: string): Promise<ApiResponse<PaymentSettings>> {
  return apiRequest<PaymentSettings>("/api/settings/payments", {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: JSON.stringify(value)
  });
}
