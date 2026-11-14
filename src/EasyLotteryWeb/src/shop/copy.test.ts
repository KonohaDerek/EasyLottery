import { describe, expect, it } from "vitest";
import {
  formatHubLinkTimestamp,
  getHubLinkStatusCopy,
  getHubLinkTestErrorFeedback,
  getHubLinkTestFeedback,
  hubLinkCopy,
  hubLinkText
} from "./copy";

const expectedStates = {
  not_linked: [
    "未連結",
    "此網站尚未設定 Card Hub 憑證。",
    "請在 Card Hub 取得 Client ID 與 Secret 後填入下方欄位。"
  ],
  linked: [
    "已連結",
    "已連結到 Card Hub 的「我的頻道」，可以發布卡牌與卡包。",
    "需要時可按「測試連線」確認狀態。"
  ],
  credentials_invalid: [
    "憑證失效",
    "Card Hub 拒絕了此網站的憑證，可能已被撤銷、輪替逾期，或實況主帳號已停用。",
    "請到 Card Hub 產生新的 Secret，再貼到下方並儲存。"
  ],
  hub_unreachable: [
    "Hub 無法連線",
    "目前連不上 Card Hub，這是網路或服務問題，不代表憑證有誤。",
    "請稍後再按「測試連線」；期間的 Donate 抽卡會排隊，恢復後自動重送。"
  ]
} as const;

describe("Card Hub link copy", () => {
  it("uses the approved page, field, helper, action, and rotation strings", () => {
    expect(hubLinkCopy["hub.link.title"]).toBe("Card Hub 連結");
    expect(hubLinkCopy["hub.link.lead"]).toBe("將這個 EasyLottery 網站連結到 Card Hub 後，才能發布卡牌、卡包並讓 Donate 抽卡寄出兌換碼。");
    expect(hubLinkCopy["hub.link.field.hubUrl.label"]).toBe("Card Hub 網址");
    expect(hubLinkCopy["hub.link.field.hubUrl.helper"]).toBe("通常不需要修改，請使用 Card Hub 提供的網址。");
    expect(hubLinkCopy["hub.link.field.clientId.label"]).toBe("站台 Client ID");
    expect(hubLinkCopy["hub.link.field.clientId.helper"]).toBe("在 Card Hub 實況主後台的「站台」頁面可以找到。");
    expect(hubLinkCopy["hub.link.field.clientSecret.label"]).toBe("站台 Client Secret");
    expect(hubLinkCopy["hub.link.field.clientSecret.helper"]).toBe("密鑰會加密保存在此網站，儲存後不會再顯示於瀏覽器。");
    expect(hubLinkCopy["hub.link.field.clientSecret.placeholderSaved"]).toBe("已設定時可留白保留伺服器值");
    expect(hubLinkCopy["hub.link.field.tenant.label"]).toBe("實況主名稱");
    expect(hubLinkCopy["hub.link.field.lastPublish.label"]).toBe("最後發布時間");
    expect(hubLinkCopy["hub.link.field.lastCheck.label"]).toBe("最後檢查時間");
    expect(hubLinkCopy["hub.link.save"]).toBe("儲存並驗證");
    expect(hubLinkCopy["hub.link.saving"]).toBe("驗證中…");
    expect(hubLinkCopy["hub.link.testConnection"]).toBe("測試連線");
    expect(hubLinkCopy["hub.link.testing"]).toBe("測試中…");
    expect(hubLinkCopy["hub.site.rotate.explain"]).toBe("輪替會產生新的 Secret。舊的 Secret 會繼續有效 24 小時，讓你有時間更新網站設定，之後自動失效。");
  });

  it.each(Object.entries(expectedStates))("provides label, description, and action for %s", (status, expected) => {
    const copy = getHubLinkStatusCopy(status as keyof typeof expectedStates, "我的頻道");
    expect([copy.label, copy.desc, copy.action]).toEqual(expected);
  });

  it("replaces the linked streamer placeholder and uses the site key spelling", () => {
    expect(hubLinkCopy["hub.link.status.not_linked"]).toBe("未連結");
    expect(hubLinkText("hub.link.status.linked.desc", { streamerName: "我的頻道" })).toBe(expectedStates.linked[1]);
  });

  it("maps test results and tenant-disabled errors to the approved feedback", () => {
    expect(getHubLinkTestFeedback("linked")).toEqual({ message: "連線成功，憑證有效。", kind: "success" });
    expect(getHubLinkTestFeedback("credentials_invalid")).toEqual({ message: "連線失敗：憑證無效或已撤銷。", kind: "error" });
    expect(getHubLinkTestFeedback("hub_unreachable")).toEqual({ message: "連線失敗：無法連到 Card Hub，請稍後再試。", kind: "error" });
    expect(getHubLinkTestFeedback("tenant_disabled")).toEqual({ message: "連線成功，但此實況主帳號目前已停用，暫時無法發布或抽卡。", kind: "warning" });
    expect(getHubLinkTestFeedback("not_linked")).toBeNull();
    expect(getHubLinkTestErrorFeedback("tenant_disabled").message).toBe("連線成功，但此實況主帳號目前已停用，暫時無法發布或抽卡。");
    expect(getHubLinkTestErrorFeedback("invalid_client").message).toBe("連線失敗：憑證無效或已撤銷。");
  });

  it("formats available timestamps for zh-TW and leaves missing or invalid values blank", () => {
    expect(formatHubLinkTimestamp(null)).toBe("—");
    expect(formatHubLinkTimestamp("invalid")).toBe("—");
    expect(formatHubLinkTimestamp("2026-11-14T13:30:00.000Z")).toMatch(/2026.*11.*14/);
  });
});
