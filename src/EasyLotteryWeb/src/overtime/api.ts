import { parse, stringify } from "yaml";
import { apiRequest, apiText } from "../api/client";

export type OvertimeSupportSource = 1 | 2 | 3 | 4;

export interface OvertimeOverlaySettings {
  isEnabled?: boolean;
  themeKey?: string;
  textAnimationKey?: string;
  showAmounts?: boolean;
  streamStartedAtUtc?: string | null;
  plannedEndAtUtc?: string | null;
  sessionState?: string;
  pausedAtUtc?: string | null;
  completedAtUtc?: string | null;
  supportMessageVisibleSeconds?: number;
  demoEcpayAmount?: number;
  enableCompletionFireworks?: boolean;
  completionFireworksStyle?: string;
  completionFireworksDurationSeconds?: number;
  completionFireworksGifUrl?: string;
  rewardRules?: Array<{
    isEnabled?: boolean;
    amountThreshold?: number;
    label?: string;
    addHours?: number;
    addMinutes?: number;
  }>;
  [key: string]: unknown;
}

export interface OvertimeSupportEvent {
  id?: string;
  source: OvertimeSupportSource;
  sourceLabel?: string;
  displayName: string;
  message?: string;
  amount?: number | null;
  amountDisplay?: string;
  currency?: string;
  avatarUrl?: string;
  color?: string;
  occurredAtUtc: string;
  sessionEndAtUtc?: string | null;
  externalId?: string | null;
}

type OvertimeConfigProjection = Record<string, unknown> & {
  overtimeOverlay?: OvertimeOverlaySettings;
};

const MaxEvents = 30;

function readProjection(yaml: string): OvertimeConfigProjection {
  const parsed = parse(yaml);
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error("加班台 OBS 設定格式無效。");
  }
  return parsed as OvertimeConfigProjection;
}

export async function readOvertimeSettings(): Promise<OvertimeOverlaySettings> {
  const { data } = await apiText("/settings");
  const projection = readProjection(data);
  if (!projection.overtimeOverlay || typeof projection.overtimeOverlay !== "object") {
    throw new Error("加班台 OBS 設定不存在。");
  }
  return projection.overtimeOverlay;
}

export async function saveOvertimeSettings(settings: OvertimeOverlaySettings): Promise<void> {
  const { data, etag } = await apiText("/settings");
  const projection = readProjection(data);
  projection.overtimeOverlay = settings;
  await apiText("/settings", {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: stringify(projection)
  });
}

export async function readOvertimeFeed(): Promise<OvertimeSupportEvent[]> {
  const { data } = await apiRequest<OvertimeSupportEvent[]>("/api/overtime-feed");
  return Array.isArray(data) ? data : [];
}

export async function saveOvertimeFeed(events: OvertimeSupportEvent[]): Promise<void> {
  await apiRequest<void>("/api/overtime-feed", { method: "PUT", body: JSON.stringify(events.slice(0, MaxEvents)) });
}

export async function appendOvertimeEvent(event: OvertimeSupportEvent): Promise<void> {
  const events = await readOvertimeFeed();
  const normalized: OvertimeSupportEvent = {
    ...event,
    id: event.id || globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`,
    sourceLabel: event.sourceLabel?.trim() || String(event.source),
    displayName: event.displayName.trim(),
    message: event.message?.trim() || "",
    amountDisplay: event.amountDisplay?.trim() || "",
    currency: event.currency?.trim() || "TWD",
    avatarUrl: event.avatarUrl?.trim() || "",
    color: event.color?.trim() || "#ff85b4",
    occurredAtUtc: event.occurredAtUtc || new Date().toISOString()
  };
  await saveOvertimeFeed([normalized, ...events].slice(0, MaxEvents));
}
