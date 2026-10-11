export type OvertimeSessionState = "idle" | "running" | "paused" | "completed" | "ended";

export interface OvertimeSessionSnapshot {
  sessionState?: string | null;
  plannedEndAtUtc?: string | null;
}

export interface OvertimeRewardRule {
  isEnabled?: boolean;
  amountThreshold?: number;
  addHours?: number;
  addMinutes?: number;
}

function validTimestamp(value: string | null | undefined): number | null {
  if (!value) return null;
  const timestamp = Date.parse(value);
  if (!Number.isFinite(timestamp) || new Date(timestamp).getUTCFullYear() < 2000) return null;
  return timestamp;
}

export function remainingTimeText(plannedEndAtUtc: string | null | undefined, now: Date): string {
  const end = validTimestamp(plannedEndAtUtc);
  if (end === null) return "000000000000";
  const totalSeconds = Math.max(0, Math.floor((end - now.getTime()) / 1000));
  const hours = Math.floor(totalSeconds / 3600).toString().padStart(2, "0");
  const minutes = Math.floor(totalSeconds / 60 % 60).toString().padStart(2, "0");
  const seconds = (totalSeconds % 60).toString().padStart(2, "0");
  return `${hours}:${minutes}:${seconds}`;
}

export function calculateProgressPercent(
  startedAtUtc: string | null | undefined,
  plannedEndAtUtc: string | null | undefined,
  now: Date
): number {
  const start = validTimestamp(startedAtUtc);
  const end = validTimestamp(plannedEndAtUtc);
  if (start === null || end === null || end <= start) return 0;
  return Math.min(100, Math.max(0, (now.getTime() - start) / (end - start) * 100));
}

export function extendEndForPause(
  plannedEndAtUtc: string | null | undefined,
  pausedAtUtc: string | null | undefined,
  resumedAtUtc: string
): string | null | undefined {
  const end = validTimestamp(plannedEndAtUtc);
  const paused = validTimestamp(pausedAtUtc);
  const resumed = validTimestamp(resumedAtUtc);
  if (end === null || paused === null || resumed === null || resumed <= paused) return plannedEndAtUtc;
  return new Date(end + resumed - paused).toISOString();
}

export function getDisplaySessionState(snapshot: OvertimeSessionSnapshot, now: Date): OvertimeSessionState {
  const state = snapshot.sessionState?.toLowerCase();
  if (state === "running") {
    const end = validTimestamp(snapshot.plannedEndAtUtc);
    if (end !== null && end <= now.getTime()) return "completed";
  }
  if (state === "running" || state === "paused" || state === "completed" || state === "ended") return state;
  return "idle";
}

export function isEventVisible(occurredAtUtc: string, now: Date, visibleSeconds: number): boolean {
  const occurred = validTimestamp(occurredAtUtc);
  if (occurred === null) return false;
  const age = now.getTime() - occurred;
  const visibleWindow = Math.min(60, Math.max(1, visibleSeconds));
  return age >= 0 && age <= visibleWindow * 1000;
}

export function getRewardSummary(rules: OvertimeRewardRule[] | undefined, amount: number | null | undefined): string {
  if (amount === null || amount === undefined || !rules) return "";
  const match = rules
    .filter(rule => rule.isEnabled !== false && amount >= (rule.amountThreshold ?? 0))
    .sort((left, right) =>
      (right.amountThreshold ?? 0) - (left.amountThreshold ?? 0)
      || ((right.addHours ?? 0) * 60 + (right.addMinutes ?? 0)) - ((left.addHours ?? 0) * 60 + (left.addMinutes ?? 0))
    )[0];
  if (!match) return "";
  const totalMinutes = Math.max(0, match.addHours ?? 0) * 60 + Math.max(0, match.addMinutes ?? 0);
  if (totalMinutes === 0) return "加班 +0 分鐘";
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  const duration = hours > 0 && minutes > 0
    ? `${hours} 小時 ${minutes} 分鐘`
    : hours > 0 ? `${hours} 小時` : `${minutes} 分鐘`;
  return `加班 +${duration}`;
}

export class OvertimeSessionRevision {
  private revision = 0;

  begin(): number {
    this.revision += 1;
    return this.revision;
  }

  isCurrent(revision: number): boolean {
    return revision === this.revision;
  }
}
