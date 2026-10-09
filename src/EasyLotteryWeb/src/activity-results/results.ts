export type ActivityResultType = 0 | 1;
export type ActivityTypeFilter = "all" | "pokebox" | "roulette";

export interface ActivityResultItem {
  order: number;
  name: string;
  description: string;
  imageUrl: string;
  color: string;
  resultedAtUtc: string | null;
}

export interface ActivityResultRecord {
  id: number;
  activityType: ActivityResultType;
  activityName: string;
  activityDateUtc: string;
  summary: string;
  items: ActivityResultItem[];
}

export interface ActivityResultFilters {
  search?: string;
  type?: ActivityTypeFilter;
  startDate?: string;
  endDate?: string;
}

export function filterActivityResults(
  records: readonly ActivityResultRecord[],
  filters: ActivityResultFilters = {}
): ActivityResultRecord[] {
  const search = filters.search?.trim().toLowerCase() ?? "";
  const filtered = records.filter(record => {
    if (filters.type === "pokebox" && record.activityType !== 0) return false;
    if (filters.type === "roulette" && record.activityType !== 1) return false;

    const date = localDateKey(record.activityDateUtc);
    if (filters.startDate && (!date || date < filters.startDate)) return false;
    if (filters.endDate && (!date || date > filters.endDate)) return false;

    if (!search) return true;
    const typeName = record.activityType === 0 ? "pokebox" : "roulette";
    return [record.activityName, record.summary, typeName,
      ...record.items.flatMap(item => [item.name, item.description])]
      .some(value => String(value ?? "").toLowerCase().includes(search));
  });

  return filtered.sort((left, right) =>
    Date.parse(right.activityDateUtc) - Date.parse(left.activityDateUtc) || right.id - left.id
  );
}

export function serializeActivityResultsCsv(records: readonly ActivityResultRecord[]): string {
  const header = "活動 ID,活動類型,活動名稱,活動時間 UTC,摘要,結果順序,獎項,說明,結果時間 UTC";
  const rows = records.flatMap(record => [...record.items]
    .sort((left, right) => left.order - right.order)
    .map(item => [
      record.id,
      record.activityType === 0 ? "PokeBox" : "Roulette",
      record.activityName,
      toIsoUtc(record.activityDateUtc),
      record.summary,
      item.order,
      item.name,
      item.description,
      item.resultedAtUtc ? toIsoUtc(item.resultedAtUtc) : ""
    ].map(csvCell).join(",")));

  return `\uFEFF${header}\r\n${rows.length ? `${rows.join("\r\n")}\r\n` : ""}`;
}

function localDateKey(value: string): string | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function toIsoUtc(value: string): string {
  const utcValue = value.match(/^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d+))?Z$/);
  if (utcValue) return `${utcValue[1]}.${(utcValue[2] ?? "").padEnd(7, "0").slice(0, 7)}Z`;

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : `${date.toISOString().slice(0, -1)}0000Z`;
}

function csvCell(value: string | number): string {
  return `"${String(value).replaceAll('"', '""')}"`;
}
