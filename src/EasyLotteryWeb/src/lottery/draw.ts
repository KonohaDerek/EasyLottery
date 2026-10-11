export interface Participant {
  id: string;
  lotteryGroupId: string;
  name: string;
  level: string;
  group: string;
  tags: string[];
  isWinner: boolean;
}

export interface Winner {
  member: Participant;
  prize: string;
}

export interface DrawResult {
  participants: Participant[];
  prizes: string[];
  winner: Winner | null;
  reason?: "no-prizes" | "no-eligible-participants";
}

export function createId(): string {
  if (typeof globalThis.crypto?.randomUUID === "function") return globalThis.crypto.randomUUID();
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export function normalizeTags(tags: unknown): string[] {
  const values = Array.isArray(tags) ? tags : typeof tags === "string" ? tags.split(/[,，\n\r;|]/) : [];
  const result: string[] = [];
  const seen = new Set<string>();
  for (const value of values) {
    if (typeof value !== "string") continue;
    const tag = value.trim();
    const key = tag.toLocaleLowerCase();
    if (!tag || seen.has(key)) continue;
    seen.add(key);
    result.push(tag);
  }
  return result;
}

export function createParticipant(
  value: Partial<Participant> & Pick<Participant, "name">,
  idFactory: () => string = createId
): Participant {
  const id = idFactory();
  return {
    id,
    lotteryGroupId: value.lotteryGroupId?.trim() || id,
    name: value.name.trim(),
    level: value.level?.trim() ?? "",
    group: value.group?.trim() ?? "",
    tags: normalizeTags(value.tags),
    isWinner: Boolean(value.isWinner)
  };
}

export function participantGroup(participant: Pick<Participant, "group">): string {
  return participant.group.trim() || "未分組";
}

export function participantGroups(participants: readonly Participant[]): string[] {
  const groups = new Map<string, string>();
  for (const participant of participants) {
    const group = participantGroup(participant);
    const key = group.toLocaleLowerCase();
    if (!groups.has(key)) groups.set(key, group);
  }
  return [...groups.values()].sort((left, right) => left < right ? -1 : left > right ? 1 : 0);
}

export function filterParticipantsByGroup(participants: readonly Participant[], group: string): Participant[] {
  if (!group) return [...participants];
  const selected = group.toLocaleLowerCase();
  return participants.filter(participant => participantGroup(participant).toLocaleLowerCase() === selected);
}

export function secureRandomIndex(length: number, source: Crypto = globalThis.crypto): number {
  if (!Number.isSafeInteger(length) || length < 1) throw new RangeError("抽獎候選數量必須大於 0。");
  if (!source?.getRandomValues) throw new Error("此瀏覽器無法提供安全亂數，請改用支援 Web Crypto 的瀏覽器。");

  const range = 0x1_0000_0000;
  const limit = Math.floor(range / length) * length;
  const buffer = new Uint32Array(1);
  let value: number;
  do {
    source.getRandomValues(buffer);
    value = buffer[0];
  } while (value >= limit);
  return value % length;
}

export function drawLottery(
  participants: readonly Participant[],
  prizes: readonly string[],
  chooseIndex: (length: number) => number = secureRandomIndex
): DrawResult {
  if (!prizes.length) return { participants: [...participants], prizes: [...prizes], winner: null, reason: "no-prizes" };
  const eligible = participants.filter(participant => !participant.isWinner);
  if (!eligible.length) return { participants: [...participants], prizes: [...prizes], winner: null, reason: "no-eligible-participants" };

  const index = chooseIndex(eligible.length);
  if (!Number.isInteger(index) || index < 0 || index >= eligible.length) throw new RangeError("抽獎亂數索引超出候選範圍。");
  const selected = eligible[index];
  const member = { ...selected, tags: [...selected.tags] };
  const participantsAfterDraw = participants.map(participant => participant.lotteryGroupId === selected.lotteryGroupId
    ? { ...participant, tags: [...participant.tags], isWinner: true }
    : { ...participant, tags: [...participant.tags] });

  return {
    participants: participantsAfterDraw,
    prizes: prizes.slice(1),
    winner: { member, prize: prizes[0] }
  };
}

export function participantLabel(participant: Participant): string {
  const name = participant.level ? `(${participant.level})${participant.name}` : participant.name;
  const group = participantGroup(participant);
  return group === "未分組" ? name : `[${group}] ${name}`;
}
