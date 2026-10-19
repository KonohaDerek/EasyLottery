import { createId, createParticipant, type Participant } from "./draw";

export type DataFormat = "json" | "csv";

export interface YoutubeMember {
  name: string;
  level: string;
  accountId?: string;
}

interface LotteryParticipantExport {
  Name: string;
  Level: string;
  Group: string;
  LotteryGroupId: string;
  Tags: string[];
  IsWinner: boolean;
  Platform?: string;
  AccountId?: string;
}

export function parseCsvRows(content: string): string[][] {
  const input = content.replace(/^\uFEFF/, "");
  const rows: string[][] = [];
  let row: string[] = [];
  let value = "";
  let quoted = false;

  for (let index = 0; index < input.length; index++) {
    const character = input[index];
    if (quoted) {
      if (character === '"' && input[index + 1] === '"') {
        value += '"';
        index++;
      } else if (character === '"') {
        quoted = false;
      } else {
        value += character;
      }
      continue;
    }

    if (character === '"' && value.length === 0) {
      quoted = true;
    } else if (character === ",") {
      row.push(value);
      value = "";
    } else if (character === "\n" || character === "\r") {
      if (character === "\r" && input[index + 1] === "\n") index++;
      row.push(value);
      if (row.some(cell => cell.length > 0)) rows.push(row);
      row = [];
      value = "";
    } else {
      value += character;
    }
  }

  if (quoted) throw new SyntaxError("CSV 引號未成對。");
  if (value.length > 0 || row.length > 0) {
    row.push(value);
    if (row.some(cell => cell.length > 0)) rows.push(row);
  }
  return rows;
}

function columnIndex(headers: readonly string[], names: readonly string[]): number {
  const normalized = headers.map(header => header.trim().toLocaleLowerCase());
  for (const name of names) {
    const index = normalized.indexOf(name.toLocaleLowerCase());
    if (index >= 0) return index;
  }
  return -1;
}

function cell(row: readonly string[], index: number): string {
  return index < 0 ? "" : row[index]?.trim() ?? "";
}

function parseBoolean(value: unknown): boolean {
  if (typeof value === "boolean") return value;
  return typeof value === "string" && ["true", "1", "yes"].includes(value.trim().toLocaleLowerCase());
}

function exportRows(participants: readonly Participant[]): LotteryParticipantExport[] {
  return participants.map(participant => ({
    Name: participant.name,
    Level: participant.level,
    Group: participant.group,
    LotteryGroupId: participant.lotteryGroupId,
    Tags: [...participant.tags],
    IsWinner: participant.isWinner,
    ...(participant.identity ? {
      Platform: participant.identity.platform,
      AccountId: participant.identity.accountId
    } : {})
  }));
}

function escapeCsv(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}

function serializeCsv(headers: readonly string[], rows: readonly (readonly string[])[]): string {
  return [headers, ...rows].map(row => row.map(escapeCsv).join(",")).join("\r\n");
}

export function parseParticipantImport(content: string, format: DataFormat, idFactory: () => string = createId): Participant[] {
  if (!content.trim()) return [];
  if (format === "json") {
    const parsed: unknown = JSON.parse(content);
    if (!Array.isArray(parsed)) throw new TypeError("參加者 JSON 必須是陣列。");
    return parsed
      .filter((value): value is Record<string, unknown> => Boolean(value) && typeof value === "object" && !Array.isArray(value))
      .map(value => {
        const rawTags = value.Tags ?? value.tags;
        const inputTags = Array.isArray(rawTags) ? rawTags : typeof rawTags === "string" ? rawTags : [];
        const platform = value.Platform ?? value.platform;
        const accountId = value.AccountId ?? value.accountId;
        return createParticipant({
          name: String(value.Name ?? value.name ?? ""),
          level: String(value.Level ?? value.level ?? ""),
          group: String(value.Group ?? value.group ?? ""),
          identity: {
            platform: typeof platform === "string" ? platform : "",
            accountId: typeof accountId === "string" ? accountId : ""
          },
          lotteryGroupId: typeof (value.LotteryGroupId ?? value.lotteryGroupId) === "string"
            ? String(value.LotteryGroupId ?? value.lotteryGroupId)
            : undefined,
          tags: inputTags as string[],
          isWinner: parseBoolean(value.IsWinner ?? value.isWinner)
        }, idFactory);
      })
      .filter(participant => participant.name.length > 0);
  }

  const [headers = [], ...rows] = parseCsvRows(content);
  const nameColumn = columnIndex(headers, ["name"]);
  if (nameColumn < 0) return [];
  const levelColumn = columnIndex(headers, ["level"]);
  const groupColumn = columnIndex(headers, ["group"]);
  const lotteryGroupColumn = columnIndex(headers, ["lotterygroupid"]);
  const platformColumn = columnIndex(headers, ["platform"]);
  const accountIdColumn = columnIndex(headers, ["accountid"]);
  const tagsColumn = columnIndex(headers, ["tags"]);
  const winnerColumn = columnIndex(headers, ["iswinner"]);
  return rows.map(row => createParticipant({
    name: cell(row, nameColumn),
    level: cell(row, levelColumn),
    group: cell(row, groupColumn),
    identity: { platform: cell(row, platformColumn), accountId: cell(row, accountIdColumn) },
    lotteryGroupId: cell(row, lotteryGroupColumn) || undefined,
    tags: cell(row, tagsColumn).split(/[,，\n\r;|]/),
    isWinner: parseBoolean(cell(row, winnerColumn))
  }, idFactory)).filter(participant => participant.name.length > 0);
}

export function parsePrizes(content: string, format: DataFormat): string[] {
  if (!content.trim()) return [];
  if (format === "json") {
    const parsed: unknown = JSON.parse(content);
    if (!Array.isArray(parsed) || parsed.some(item => typeof item !== "string")) {
      throw new TypeError("獎項 JSON 必須是字串陣列。");
    }
    return parsed.map(prize => prize.trim()).filter(Boolean);
  }

  const [headers = [], ...rows] = parseCsvRows(content);
  const prizeColumn = columnIndex(headers, ["prize"]);
  return prizeColumn < 0 ? [] : rows.map(row => cell(row, prizeColumn)).filter(Boolean);
}

export function exportParticipants(participants: readonly Participant[], format: DataFormat): string {
  const rows = exportRows(participants);
  if (format === "json") return JSON.stringify(rows, null, 2);
  return serializeCsv(
    ["Name", "Level", "Group", "LotteryGroupId", "Tags", "IsWinner", "Platform", "AccountId"],
    rows.map(row => [row.Name, row.Level, row.Group, row.LotteryGroupId, row.Tags.join("|"), String(row.IsWinner).toLowerCase(), row.Platform ?? "", row.AccountId ?? ""])
  );
}

export function exportPrizes(prizes: readonly string[], format: DataFormat): string {
  if (format === "json") return JSON.stringify([...prizes], null, 2);
  return serializeCsv(["Prize"], prizes.map(prize => [prize]));
}

export function parseYoutubeMembers(content: string): YoutubeMember[] {
  const [headers = [], ...rows] = parseCsvRows(content);
  const nameColumn = columnIndex(headers, ["會員", "name"]);
  const levelColumn = columnIndex(headers, ["目前級別", "level"]);
  const profileColumn = columnIndex(headers, ["連結到個人資料", "profile link", "profile url"]);
  if (nameColumn < 0) throw new TypeError("找不到 YouTube 會員欄位「會員」。");
  return rows.map(row => {
    const accountId = youtubeChannelId(cell(row, profileColumn));
    return {
      name: cell(row, nameColumn),
      level: cell(row, levelColumn),
      ...(accountId ? { accountId } : {})
    };
  })
    .filter(member => member.name.length > 0);
}

function youtubeChannelId(profileUrl: string): string | undefined {
  if (!profileUrl) return undefined;
  try {
    const url = new URL(profileUrl);
    const allowedHosts = ["youtube.com", "www.youtube.com", "m.youtube.com"];
    if (url.protocol !== "https:" || !allowedHosts.includes(url.hostname)
      || url.username || url.password || (url.port && url.port !== "443")) return undefined;
    return url.pathname.match(/^\/channel\/(UC[A-Za-z0-9_-]{22})\/?$/)?.[1];
  } catch {
    return undefined;
  }
}

export function expandYoutubeMembers(
  members: readonly YoutubeMember[],
  currentRates: Readonly<Record<string, number>>,
  idFactory: () => string = createId
): { participants: Participant[]; levelRates: Record<string, number> } {
  const levelRates = { ...currentRates };
  const rateKeys = new Map(Object.keys(levelRates).map(key => [key.toLocaleLowerCase(), key]));
  const participants: Participant[] = [];

  for (const member of members) {
    const level = member.level.trim();
    const key = rateKeys.get(level.toLocaleLowerCase());
    if (!key && level) {
      levelRates[level] = 1;
      rateKeys.set(level.toLocaleLowerCase(), level);
    }
    const configured = key ? Number(levelRates[key]) : 1;
    const count = Number.isFinite(configured) ? Math.max(0, Math.trunc(configured)) : 1;
    const lotteryGroupId = idFactory();
    for (let index = 0; index < count; index++) {
      participants.push({
        id: idFactory(),
        lotteryGroupId,
        name: member.name.trim(),
        ...(member.accountId ? { identity: { platform: "youtube", accountId: member.accountId } } : {}),
        level,
        group: "YT會員",
        tags: [],
        isWinner: false
      });
    }
  }
  return { participants, levelRates };
}
