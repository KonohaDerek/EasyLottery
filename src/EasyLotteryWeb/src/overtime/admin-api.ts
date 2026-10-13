import { apiRequest, type ApiResponse } from "../api/client";

export interface OvertimeRewardRule {
  isEnabled: boolean;
  amountThreshold: number;
  addHours: number;
  addMinutes: number;
  label: string;
}

export interface OvertimeSettings {
  isEnabled: boolean;
  title: string;
  subtitle: string;
  themeKey: string;
  maxVisibleItems: number;
  showAmounts: boolean;
  showAvatars: boolean;
  demoSuperChatAmount: number;
  demoEcpayAmount: number;
  messageTemplateKey: string;
  textAnimationKey: string;
  streamStartedAtUtc: string | null;
  plannedEndAtUtc: string | null;
  sessionState: string;
  pausedAtUtc: string | null;
  completedAtUtc: string | null;
  enableCompletionFireworks: boolean;
  completionFireworksStyle: string;
  completionFireworksDurationSeconds: number;
  completionFireworksGifUrl: string;
  supportMessageVisibleSeconds: number;
  rewardRules: OvertimeRewardRule[];
  [key: string]: unknown;
}

export interface ScopedTokenResponse {
  token: string;
  expiresAtUtc: string;
}

export function mergeOvertimeSettings(
  latest: OvertimeSettings,
  draft: OvertimeSettings,
  options: { preserveLatestPlannedEnd?: boolean } = {}
): OvertimeSettings {
  return {
    ...latest,
    isEnabled: draft.isEnabled,
    title: draft.title,
    subtitle: draft.subtitle,
    themeKey: draft.themeKey,
    maxVisibleItems: draft.maxVisibleItems,
    showAmounts: draft.showAmounts,
    showAvatars: draft.showAvatars,
    demoSuperChatAmount: draft.demoSuperChatAmount,
    demoEcpayAmount: draft.demoEcpayAmount,
    messageTemplateKey: draft.messageTemplateKey,
    textAnimationKey: draft.textAnimationKey,
    plannedEndAtUtc: options.preserveLatestPlannedEnd ? latest.plannedEndAtUtc : draft.plannedEndAtUtc,
    enableCompletionFireworks: draft.enableCompletionFireworks,
    completionFireworksStyle: draft.completionFireworksStyle,
    completionFireworksDurationSeconds: draft.completionFireworksDurationSeconds,
    completionFireworksGifUrl: draft.completionFireworksGifUrl,
    supportMessageVisibleSeconds: draft.supportMessageVisibleSeconds,
    rewardRules: draft.rewardRules.map(rule => ({ ...rule }))
  };
}

export async function loadOvertimeSettings(): Promise<ApiResponse<OvertimeSettings>> {
  return apiRequest<OvertimeSettings>("/api/settings/overtime");
}

async function putOvertimeSettings(value: OvertimeSettings, etag: string): Promise<ApiResponse<OvertimeSettings>> {
  return apiRequest<OvertimeSettings>("/api/settings/overtime", {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: JSON.stringify(value)
  });
}

export function saveOvertimeSnapshot(
  latest: ApiResponse<OvertimeSettings>,
  value: OvertimeSettings
): Promise<ApiResponse<OvertimeSettings>> {
  return putOvertimeSettings(value, latest.etag);
}

export async function saveOvertimeSettings(
  draft: OvertimeSettings,
  options: { preserveLatestPlannedEnd?: boolean } = {}
): Promise<ApiResponse<OvertimeSettings>> {
  const latest = await loadOvertimeSettings();
  return saveOvertimeSnapshot(latest, mergeOvertimeSettings(latest.data, draft, options));
}

export async function updateOvertimePlannedEnd(plannedEndAtUtc: string | null): Promise<ApiResponse<OvertimeSettings>> {
  const latest = await loadOvertimeSettings();
  return saveOvertimeSnapshot(latest, { ...latest.data, plannedEndAtUtc });
}

export function clearOvertimeFeed(): Promise<ApiResponse<void>> {
  return apiRequest<void>("/api/overtime-feed", { method: "DELETE" });
}

export function extendPlannedEndForReward(
  settings: OvertimeSettings,
  amount: number,
  now = new Date()
): string | null {
  if (settings.sessionState.toLowerCase() !== "running") return settings.plannedEndAtUtc;
  const rule = settings.rewardRules
    .filter(item => item.isEnabled && amount >= item.amountThreshold)
    .sort((left, right) => right.amountThreshold - left.amountThreshold
      || (right.addHours * 60 + right.addMinutes) - (left.addHours * 60 + left.addMinutes))[0];
  if (!rule) return settings.plannedEndAtUtc;
  const addedMs = (Math.max(0, rule.addHours) * 60 + Math.max(0, rule.addMinutes)) * 60_000;
  if (addedMs <= 0) return settings.plannedEndAtUtc;
  const existing = settings.plannedEndAtUtc ? Date.parse(settings.plannedEndAtUtc) : Number.NaN;
  const baseline = Number.isFinite(existing) ? Math.max(existing, now.getTime()) : now.getTime();
  return new Date(baseline + addedMs).toISOString();
}

export function createOvertimePreviewToken(): Promise<ApiResponse<ScopedTokenResponse>> {
  return apiRequest<ScopedTokenResponse>("/api/obs-sessions", {
    method: "POST",
    body: JSON.stringify({ resourceKind: "overtime", resourceId: "default", scopes: ["read", "control"] })
  });
}

export function buildOvertimePreviewUrl(origin: string, token: string): string {
  const url = new URL("/obs/overtime?controls=1", origin);
  url.hash = new URLSearchParams({ sessionToken: token }).toString();
  return url.toString();
}
