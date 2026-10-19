import type { OvertimeSupportEvent } from "../overtime/api";
import {
  createParticipant,
  normalizeParticipantIdentity,
  participantIdentityKey,
  type Participant,
  type ParticipantIdentity
} from "./draw";

export interface ChatRules {
  keyword: string;
  whiteList: string;
  blackList: string;
  cooldownSeconds: number;
}

export interface ChatMessage {
  id: string;
  publishedAt: string;
  type: string;
  authorName: string;
  platform?: string;
  authorId?: string | null;
  displayMessage: string;
  authorImageUrl?: string;
  superChat?: {
    amountMicros?: number | string | null;
    amountDisplay?: string | null;
    currency?: string | null;
    userComment?: string | null;
  } | null;
}

export type ChatRejectReason = "author" | "keyword" | "blacklist" | "whitelist" | "cooldown";
export interface ChatPolicyResult { allowed: boolean; reason?: ChatRejectReason; }

function ruleTokens(value: string): string[] {
  return value.split(/[\r\n,;|]/).map(token => token.trim()).filter(Boolean);
}

function authorKey(value: string): string {
  return value.trim().toLocaleLowerCase();
}

function messageIdentity(message: ChatMessage): ParticipantIdentity | undefined {
  return normalizeParticipantIdentity({ platform: message.platform, accountId: message.authorId });
}

function cooldownKey(message: ChatMessage): string | undefined {
  const identityKey = participantIdentityKey(messageIdentity(message));
  return identityKey ? `account:${identityKey}` : undefined;
}

export function isChatMessageAllowed(
  message: ChatMessage,
  rules: ChatRules,
  lastAccepted: ReadonlyMap<string, number>
): ChatPolicyResult {
  const author = message.authorName.trim();
  if (!author) return { allowed: false, reason: "author" };
  const keywords = ruleTokens(rules.keyword);
  const normalizedMessage = message.displayMessage.toLocaleLowerCase();
  if (!keywords.length || !keywords.some(keyword => normalizedMessage.includes(keyword.toLocaleLowerCase()))) {
    return { allowed: false, reason: "keyword" };
  }

  const normalizedAuthor = authorKey(author);
  const blackList = ruleTokens(rules.blackList).map(authorKey);
  if (blackList.includes(normalizedAuthor)) return { allowed: false, reason: "blacklist" };
  const whiteList = ruleTokens(rules.whiteList).map(authorKey);
  if (whiteList.length && !whiteList.includes(normalizedAuthor)) return { allowed: false, reason: "whitelist" };

  const cooldownSeconds = Math.max(0, Math.trunc(rules.cooldownSeconds));
  const key = cooldownKey(message);
  const lastAt = key ? lastAccepted.get(key) : undefined;
  const publishedAt = Date.parse(message.publishedAt);
  if (!Number.isFinite(publishedAt)) return { allowed: false, reason: "author" };
  if (cooldownSeconds > 0 && lastAt !== undefined && publishedAt - lastAt < cooldownSeconds * 1000) {
    return { allowed: false, reason: "cooldown" };
  }
  return { allowed: true };
}

export interface ChatBatchResult {
  cursor: string;
  accepted: ChatMessage[];
  addedParticipants: Participant[];
  lastAccepted: Map<string, number>;
  toastMessage: string;
}

export function processChatBatch(
  messages: readonly ChatMessage[],
  cursor: string,
  rules: ChatRules,
  previousLastAccepted: ReadonlyMap<string, number>,
  participants: readonly Participant[],
  idFactory?: () => string
): ChatBatchResult {
  const initialCursor = Date.parse(cursor);
  const newMessages = messages
    .map((item, order) => ({ item, order, publishedAt: Date.parse(item.publishedAt) }))
    .filter(entry => Number.isFinite(entry.publishedAt) && entry.publishedAt > initialCursor)
    .sort((left, right) => left.publishedAt - right.publishedAt || left.order - right.order);
  if (!newMessages.length) {
    return { cursor, accepted: [], addedParticipants: [], lastAccepted: new Map(previousLastAccepted), toastMessage: "無訊息" };
  }

  let latestSeen = initialCursor;
  const accepted: ChatMessage[] = [];
  const lastAccepted = new Map(previousLastAccepted);
  const addedParticipants: Participant[] = [];
  const knownIdentities = new Set(participants
    .map(participant => participantIdentityKey(participant.identity))
    .filter((key): key is string => key !== undefined));

  for (const { item, publishedAt } of newMessages) {
    latestSeen = Math.max(latestSeen, publishedAt);
    if (!isChatMessageAllowed(item, rules, lastAccepted).allowed) continue;
    accepted.push(item);
    const key = cooldownKey(item);
    if (key) lastAccepted.set(key, publishedAt);
    const identity = messageIdentity(item);
    const identityKey = participantIdentityKey(identity);
    if (!identityKey || !knownIdentities.has(identityKey)) {
      addedParticipants.push(createParticipant({
        name: item.authorName,
        group: "聊天室",
        ...(identity ? { identity } : {})
      }, idFactory));
      if (identityKey) knownIdentities.add(identityKey);
    }
  }

  const toastMessage = accepted.length
    ? `${accepted[accepted.length - 1].authorName}:${accepted[accepted.length - 1].displayMessage}`
    : "沒有符合規則的訊息";
  return {
    cursor: new Date(latestSeen).toISOString(),
    accepted,
    addedParticipants,
    lastAccepted,
    toastMessage
  };
}

export function toOvertimeSuperChat(message: ChatMessage, now: () => Date = () => new Date()): OvertimeSupportEvent | null {
  if (!message.type.toLocaleLowerCase().includes("superchatevent") || !message.superChat) return null;
  const details = message.superChat;
  const micros = details.amountMicros == null ? null : Number(details.amountMicros);
  return {
    source: 1,
    sourceLabel: "YouTube SuperChat",
    displayName: message.authorName.trim() || "YouTube 觀眾",
    message: details.userComment?.trim() || message.displayMessage,
    amount: Number.isFinite(micros) ? (micros as number) / 1_000_000 : null,
    amountDisplay: details.amountDisplay?.trim() || "",
    currency: details.currency?.trim() || "TWD",
    avatarUrl: message.authorImageUrl?.trim() || "",
    color: "#ff7ab6",
    occurredAtUtc: now().toISOString(),
    externalId: message.id || null
  };
}
