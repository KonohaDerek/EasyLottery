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

export type OvertimeSettingsUpdate = Partial<OvertimeOverlaySettings>
  | ((current: OvertimeOverlaySettings) => Partial<OvertimeOverlaySettings> | null);

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

export async function saveOvertimeSettings(update: OvertimeSettingsUpdate): Promise<boolean> {
  const { data, etag } = await apiText("/settings");
  const projection = readProjection(data);
  const current = projection.overtimeOverlay;
  if (!current || typeof current !== "object") throw new Error("加班台 OBS 設定不存在。");
  const patch = typeof update === "function" ? update(current) : update;
  if (patch === null) return false;
  projection.overtimeOverlay = { ...current, ...patch };
  await apiText("/settings", {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: stringify(projection)
  });
  return true;
}

export async function readOvertimeFeed(): Promise<OvertimeSupportEvent[]> {
  const { data } = await apiRequest<OvertimeSupportEvent[]>("/api/overtime-feed");
  return Array.isArray(data)
    ? data.map(normalizeOvertimeEvent).filter((event): event is OvertimeSupportEvent => event !== null)
    : [];
}

export async function readOvertimeSnapshot(): Promise<{ settings: OvertimeOverlaySettings; events: OvertimeSupportEvent[] }> {
  const [settings, events] = await Promise.allSettled([readOvertimeSettings(), readOvertimeFeed()]);
  if (settings.status === "rejected") throw settings.reason;
  if (events.status === "rejected") throw events.reason;
  return { settings: settings.value, events: events.value };
}

function normalizeOvertimeEvent(value: unknown): OvertimeSupportEvent | null {
  if (!value || typeof value !== "object" || Array.isArray(value)) return null;
  const item = value as Record<string, unknown>;
  const field = (camel: string, pascal: string) => Object.hasOwn(item, camel) ? item[camel] : item[pascal];
  const source = Number(field("source", "Source"));
  if (source !== 1 && source !== 2 && source !== 3 && source !== 4) return null;
  const stringValue = (camel: string, pascal: string, fallback = "") => {
    const result = field(camel, pascal);
    return typeof result === "string" ? result : fallback;
  };
  const amount = field("amount", "Amount");
  return {
    id: stringValue("id", "Id"),
    source,
    sourceLabel: stringValue("sourceLabel", "SourceLabel"),
    displayName: stringValue("displayName", "DisplayName"),
    message: stringValue("message", "Message"),
    amount: typeof amount === "number" ? amount : null,
    amountDisplay: stringValue("amountDisplay", "AmountDisplay"),
    currency: stringValue("currency", "Currency", "TWD"),
    avatarUrl: stringValue("avatarUrl", "AvatarUrl"),
    color: stringValue("color", "Color", "#ff85b4"),
    occurredAtUtc: stringValue("occurredAtUtc", "OccurredAtUtc"),
    sessionEndAtUtc: stringValue("sessionEndAtUtc", "SessionEndAtUtc") || null,
    externalId: stringValue("externalId", "ExternalId") || null
  };
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
