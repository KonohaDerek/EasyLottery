import { apiRequest, apiRequestWithToken } from "../api/client";
import type { OvertimeSupportEvent } from "../overtime/api";
import type { ChatMessage } from "./chat";

export interface DrawingRuleSettings {
  levelRates: Record<string, number>;
}

export interface YouTubeCredentialStatus {
  configured: boolean;
}

interface SessionTokenResponse {
  token: string;
  expiresAtUtc: string;
}

interface CachedToken extends SessionTokenResponse {
  generation: number;
}

let overtimeToken: CachedToken | null = null;
let overtimeTokenGeneration = 0;

export async function readDrawingRules(): Promise<{ settings: DrawingRuleSettings; etag: string }> {
  const response = await apiRequest<DrawingRuleSettings>("/api/settings/drawing-rules");
  return {
    settings: { levelRates: normalizeLevelRates(response.data?.levelRates) },
    etag: response.etag
  };
}

export async function writeDrawingRules(levelRates: Record<string, number>, etag: string): Promise<string> {
  if (!etag || etag.trim() === "*") throw new Error("載入目前抽獎倍率後才能儲存。");
  const response = await apiRequest<DrawingRuleSettings>("/api/settings/drawing-rules", {
    method: "PUT",
    headers: etag ? { "If-Match": etag } : undefined,
    body: JSON.stringify({ levelRates: normalizeLevelRates(levelRates) })
  });
  return response.etag;
}

export function normalizeLevelRates(value: unknown): Record<string, number> {
  if (!value || typeof value !== "object" || Array.isArray(value)) return {};
  const result: Record<string, number> = {};
  for (const [rawLevel, rawRate] of Object.entries(value)) {
    const level = rawLevel.trim();
    if (!level) continue;
    const rate = Number(rawRate);
    result[level] = Number.isFinite(rate) ? Math.max(1, Math.trunc(rate)) : 1;
  }
  return result;
}

export async function getYouTubeCredentialStatus(): Promise<YouTubeCredentialStatus> {
  const response = await apiRequest<YouTubeCredentialStatus>("/api/lottery/youtube/status");
  return response.data;
}

export async function resolveYouTubeLiveChat(liveUrl: string, signal?: AbortSignal): Promise<string> {
  const response = await apiRequest<{ chatId: string }>("/api/lottery/youtube/live-chat/resolve", {
    method: "POST",
    signal,
    body: JSON.stringify({ url: liveUrl })
  });
  return response.data.chatId;
}

export async function listYouTubeLiveChatMessages(chatId: string, signal?: AbortSignal): Promise<ChatMessage[]> {
  const response = await apiRequest<ChatMessage[]>(`/api/lottery/youtube/live-chat/${encodeURIComponent(chatId)}/messages`, { signal });
  return Array.isArray(response.data) ? response.data : [];
}

export async function publishSuperChatToOvertime(message: ChatMessage, event: OvertimeSupportEvent, signal?: AbortSignal): Promise<void> {
  if (!event.externalId) return;
  if (signal?.aborted) return;
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      const token = await getOvertimeControlToken(signal);
      if (token.generation !== overtimeTokenGeneration || signal?.aborted) return;
      await apiRequestWithToken<void>("/api/overtime-feed/superchat", token.token, {
        method: "POST",
        signal,
        body: JSON.stringify({ ...event, occurredAtUtc: message.publishedAt })
      });
      return;
    } catch (error) {
      if (signal?.aborted || isAbortError(error)) return;
      if (attempt === 0 && isUnauthorized(error)) {
        overtimeToken = null;
        continue;
      }
      throw error;
    }
  }
}

export function clearOvertimeToken(): void {
  overtimeToken = null;
  overtimeTokenGeneration++;
}

async function getOvertimeControlToken(signal?: AbortSignal): Promise<CachedToken> {
  if (overtimeToken && overtimeToken.generation === overtimeTokenGeneration
    && Date.parse(overtimeToken.expiresAtUtc) > Date.now() + 30_000) return overtimeToken;
  const generation = overtimeTokenGeneration;
  const response = await apiRequest<SessionTokenResponse>("/api/obs-sessions", {
    method: "POST",
    signal,
    body: JSON.stringify({ resourceKind: "overtime", resourceId: "default", scopes: ["control"] })
  });
  if (signal?.aborted || generation !== overtimeTokenGeneration) {
    const error = new Error("Overtime session ended before its token was issued.");
    error.name = "AbortError";
    throw error;
  }
  overtimeToken = { ...response.data, generation };
  return overtimeToken;
}

function isUnauthorized(error: unknown): boolean {
  return Boolean(error && typeof error === "object" && "status" in error && error.status === 401);
}

function isAbortError(error: unknown): boolean {
  return error instanceof Error && error.name === "AbortError";
}
