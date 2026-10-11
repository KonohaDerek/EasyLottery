import { describe, expect, it } from "vitest";
import {
  OvertimeSessionRevision,
  calculateProgressPercent,
  extendEndForPause,
  getDisplaySessionState,
  isEventVisible,
  remainingTimeText
} from "./overlay";

describe("overtime OBS display helpers", () => {
  const now = new Date("2026-10-11T12:00:00.000Z");

  it("formats the remaining countdown and clamps it at zero", () => {
    expect(remainingTimeText("2026-10-11T13:01:01.000Z", now)).toBe("01:01:01");
    expect(remainingTimeText("2026-10-11T11:59:59.000Z", now)).toBe("00:00:00");
    expect(remainingTimeText(null, now)).toBe("000000000000");
  });

  it("calculates bounded progress only for valid session intervals", () => {
    expect(calculateProgressPercent("2026-10-11T11:00:00.000Z", "2026-10-11T13:00:00.000Z", now)).toBe(50);
    expect(calculateProgressPercent("2026-10-11T11:00:00.000Z", "2026-10-11T12:00:00.000Z", now)).toBe(100);
    expect(calculateProgressPercent("2026-10-11T13:00:00.000Z", "2026-10-11T12:00:00.000Z", now)).toBe(0);
    expect(calculateProgressPercent(null, null, now)).toBe(0);
  });

  it("extends the planned end by the paused duration on resume", () => {
    expect(extendEndForPause("2026-10-11T13:00:00.000Z", "2026-10-11T12:00:00.000Z", "2026-10-11T12:15:00.000Z"))
      .toBe("2026-10-11T13:15:00.000Z");
    expect(extendEndForPause("2026-10-11T13:00:00.000Z", null, "2026-10-11T12:15:00.000Z"))
      .toBe("2026-10-11T13:00:00.000Z");
  });

  it("shows an expired running session as completed without requiring a write", () => {
    expect(getDisplaySessionState({ sessionState: "running", plannedEndAtUtc: "2026-10-11T11:59:00.000Z" }, now)).toBe("completed");
    expect(getDisplaySessionState({ sessionState: "paused", plannedEndAtUtc: "2026-10-11T11:59:00.000Z" }, now)).toBe("paused");
    expect(getDisplaySessionState({ sessionState: "unknown" }, now)).toBe("idle");
  });

  it("limits support-message visibility to the configured 1–60 second window", () => {
    expect(isEventVisible("2026-10-11T11:59:52.000Z", now, 8)).toBe(true);
    expect(isEventVisible("2026-10-11T11:59:51.999Z", now, 8)).toBe(false);
    expect(isEventVisible("2026-10-11T12:00:01.000Z", now, 8)).toBe(false);
    expect(isEventVisible("2026-10-11T11:59:00.000Z", now, 500)).toBe(true);
  });

  it("rejects an older settings/feed snapshot after a newer request begins", () => {
    const revision = new OvertimeSessionRevision();
    const oldRequest = revision.begin();
    const newRequest = revision.begin();
    expect(revision.isCurrent(newRequest)).toBe(true);
    expect(revision.isCurrent(oldRequest)).toBe(false);
  });
});
