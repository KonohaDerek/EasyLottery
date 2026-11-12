import { describe, expect, it } from "vitest";
import { ApiError } from "../api/client";
import { obsErrorMessage, obsRecoveryHint } from "./errors";

describe("OBS actionable errors", () => {
  it.each([
    [401, "工作階段無效或已過期"],
    [403, "權限不足"],
    [404, "找不到此轉盤活動"],
    [429, "請稍候再試"],
    [500, "請稍後重新整理"],
  ])("maps HTTP %i without leaking the server response", (status, expected) => {
    const message = obsErrorMessage(new ApiError("private response body", status), "轉盤活動");
    expect(message).toContain(expected);
    expect(message).not.toContain("private response body");
    expect(message).not.toMatch(/^API 回傳 HTTP/);
    if (status !== 429) expect(message).toContain(obsRecoveryHint);
  });

  it.each(["轉盤活動", "戳戳樂活動", " Donate 活動", "加班台 OBS"])("names the missing %s", resource => {
    expect(obsErrorMessage(new ApiError("", 404), resource)).toBe(`找不到此${resource}，${obsRecoveryHint}`);
  });

  it("provides a network recovery step", () => {
    expect(obsErrorMessage(new TypeError("Failed to fetch"), "Donate")).toContain("請確認網路連線後重新整理");
  });

  it("preserves meaningful domain errors and handles unknown failures", () => {
    expect(obsErrorMessage(new Error("目前狀態不能執行此操作。"), "Donate")).toBe("目前狀態不能執行此操作。");
    expect(obsErrorMessage(null, "Donate")).toContain(obsRecoveryHint);
  });
});
