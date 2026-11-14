import { describe, expect, it } from "vitest";
import { highlights } from "./highlights";

describe("bounded public capability copy", () => {
  it("distinguishes record types without blanket persistence promises", () => {
    const copy = highlights.map(item => item.description).join(" ");
    for (const type of ["操作稽核", "戳戳樂／轉盤活動結果", "Donate 抽獎紀錄"]) {
      expect(copy).toContain(type);
    }
    expect(copy).toContain("依各功能的記錄操作與查詢頁面");
    expect(copy).not.toMatch(/抽獎結果與稽核紀錄分離保存|所有.*保存|重啟.*查|核心抽獎.*尚未/);
  });
});
