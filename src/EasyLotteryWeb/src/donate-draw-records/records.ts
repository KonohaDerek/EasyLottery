export type DonateDrawOutcomeFilter = "all" | "winning" | "miss";

export interface DonateDrawHistoryFilters {
  activityId: string;
  beforeDate: string;
  fromDate: string;
  outcome: DonateDrawOutcomeFilter;
  page: number;
  pageSize: number;
  search: string;
}

export function buildDonateDrawHistoryQuery(filters: DonateDrawHistoryFilters): string {
  const params = new URLSearchParams();
  params.set("page", String(Math.max(1, Math.trunc(filters.page))));
  params.set("pageSize", String(Math.max(1, Math.min(100, Math.trunc(filters.pageSize)))));
  if (filters.activityId) params.set("activityId", filters.activityId);
  if (filters.outcome !== "all") params.set("outcome", filters.outcome);
  if (filters.search.trim()) params.set("search", filters.search.trim());

  const fromUtc = localDateBoundaryUtc(filters.fromDate, 0);
  const beforeUtc = localDateBoundaryUtc(filters.beforeDate, 1);
  if (fromUtc) params.set("fromUtc", fromUtc);
  if (beforeUtc) params.set("beforeUtc", beforeUtc);
  return params.toString();
}

function localDateBoundaryUtc(dateValue: string, dayOffset: number): string | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dateValue);
  if (!match) return null;
  const [, year, month, day] = match;
  const date = new Date(Number(year), Number(month) - 1, Number(day));
  if (date.getFullYear() !== Number(year) || date.getMonth() !== Number(month) - 1 || date.getDate() !== Number(day)) return null;
  date.setDate(date.getDate() + dayOffset);
  return date.toISOString();
}
