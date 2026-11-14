import type { HubLinkStatus } from "./api";

/** Approved zh-TW copy for the EasyLottery site-side Card Hub link page. */
export const hubLinkCopy = {
  "hub.link.title": "Card Hub 連結",
  "hub.link.lead": "將這個 EasyLottery 網站連結到 Card Hub 後，才能發布卡牌、卡包並讓 Donate 抽卡寄出兌換碼。",
  "hub.link.field.hubUrl.label": "Card Hub 網址",
  "hub.link.field.hubUrl.helper": "通常不需要修改，請使用 Card Hub 提供的網址。",
  "hub.link.field.clientId.label": "站台 Client ID",
  "hub.link.field.clientId.helper": "在 Card Hub 實況主後台的「站台」頁面可以找到。",
  "hub.link.field.clientSecret.label": "站台 Client Secret",
  "hub.link.field.clientSecret.helper": "密鑰會加密保存在此網站，儲存後不會再顯示於瀏覽器。",
  "hub.link.field.clientSecret.placeholderSaved": "已設定時可留白保留伺服器值",
  "hub.link.field.tenant.label": "實況主名稱",
  "hub.link.field.lastPublish.label": "最後發布時間",
  "hub.link.field.lastCheck.label": "最後檢查時間",
  "hub.link.save": "儲存並驗證",
  "hub.link.saving": "驗證中…",
  "hub.link.testConnection": "測試連線",
  "hub.link.testing": "測試中…",
  "hub.link.shopDisabled": "卡牌商店目前未啟用（Shop:Enabled 為 false），連結設定仍可先完成。",
  "hub.link.status.not_linked": "未連結",
  "hub.link.status.not_linked.desc": "此網站尚未設定 Card Hub 憑證。",
  "hub.link.status.not_linked.action": "請在 Card Hub 取得 Client ID 與 Secret 後填入下方欄位。",
  "hub.link.status.linked": "已連結",
  "hub.link.status.linked.desc": "已連結到 Card Hub 的「{streamerName}」，可以發布卡牌與卡包。",
  "hub.link.status.linked.action": "需要時可按「測試連線」確認狀態。",
  "hub.link.status.credentials_invalid": "憑證失效",
  "hub.link.status.credentials_invalid.desc": "Card Hub 拒絕了此網站的憑證，可能已被撤銷、輪替逾期，或實況主帳號已停用。",
  "hub.link.status.credentials_invalid.action": "請到 Card Hub 產生新的 Secret，再貼到下方並儲存。",
  "hub.link.status.hub_unreachable": "Hub 無法連線",
  "hub.link.status.hub_unreachable.desc": "目前連不上 Card Hub，這是網路或服務問題，不代表憑證有誤。",
  "hub.link.status.hub_unreachable.action": "請稍後再按「測試連線」；期間的 Donate 抽卡會排隊，恢復後自動重送。",
  "hub.link.toast.testSuccess": "連線成功，憑證有效。",
  "hub.link.toast.testInvalid": "連線失敗：憑證無效或已撤銷。",
  "hub.link.toast.testUnreachable": "連線失敗：無法連到 Card Hub，請稍後再試。",
  "hub.link.toast.testTenantDisabled": "連線成功，但此實況主帳號目前已停用，暫時無法發布或抽卡。",
  "hub.link.toast.saved": "Card Hub 連結已儲存並驗證。",
  "hub.link.toast.saveFailed": "Card Hub 連結儲存失敗。",
  "hub.site.rotate.explain": "輪替會產生新的 Secret。舊的 Secret 會繼續有效 24 小時，讓你有時間更新網站設定，之後自動失效。"
} as const;

export type HubLinkCopyKey = keyof typeof hubLinkCopy;

export function hubLinkText(key: HubLinkCopyKey, values: Record<string, string> = {}): string {
  return hubLinkCopy[key].replace(/\{([^}]+)\}/g, (placeholder, name: string) => values[name] ?? placeholder);
}

const statusCopyKeys = {
  not_linked: {
    label: "hub.link.status.not_linked",
    desc: "hub.link.status.not_linked.desc",
    action: "hub.link.status.not_linked.action"
  },
  linked: {
    label: "hub.link.status.linked",
    desc: "hub.link.status.linked.desc",
    action: "hub.link.status.linked.action"
  },
  credentials_invalid: {
    label: "hub.link.status.credentials_invalid",
    desc: "hub.link.status.credentials_invalid.desc",
    action: "hub.link.status.credentials_invalid.action"
  },
  hub_unreachable: {
    label: "hub.link.status.hub_unreachable",
    desc: "hub.link.status.hub_unreachable.desc",
    action: "hub.link.status.hub_unreachable.action"
  }
} satisfies Record<HubLinkStatus, { label: HubLinkCopyKey; desc: HubLinkCopyKey; action: HubLinkCopyKey }>;

export function getHubLinkStatusCopy(status: HubLinkStatus, streamerName = "你的實況主帳號") {
  const keys = statusCopyKeys[status];
  return {
    label: hubLinkText(keys.label),
    desc: hubLinkText(keys.desc, { streamerName }),
    action: hubLinkText(keys.action)
  };
}

const testToastKeys: Record<string, HubLinkCopyKey> = {
  linked: "hub.link.toast.testSuccess",
  credentials_invalid: "hub.link.toast.testInvalid",
  hub_unreachable: "hub.link.toast.testUnreachable",
  tenant_disabled: "hub.link.toast.testTenantDisabled"
};

export type HubLinkTestFeedback = {
  message: string;
  kind: "success" | "error" | "warning";
};

export function getHubLinkTestFeedback(status: string): HubLinkTestFeedback | null {
  if (!Object.prototype.hasOwnProperty.call(testToastKeys, status)) return null;
  const key = testToastKeys[status];
  return {
    message: hubLinkCopy[key],
    kind: status === "linked" ? "success" : status === "tenant_disabled" ? "warning" : "error"
  };
}

export function getHubLinkTestErrorFeedback(code: string): HubLinkTestFeedback {
  if (code === "tenant_disabled") return getHubLinkTestFeedback("tenant_disabled")!;
  if (code === "credentials_invalid" || code === "invalid_client") return getHubLinkTestFeedback("credentials_invalid")!;
  return getHubLinkTestFeedback("hub_unreachable")!;
}

export function formatHubLinkTimestamp(value: string | null): string {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("zh-TW", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23"
  }).format(date);
}
