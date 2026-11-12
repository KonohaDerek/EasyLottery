import { ApiError } from "../api/client";

export const obsRecoveryHint = "請回管理頁重新複製 OBS 網址。";

/** Translate transport failures without displaying response bodies or session details. */
export function obsErrorMessage(cause: unknown, resource: string): string {
  if (cause instanceof ApiError) {
    switch (cause.status) {
      case 401: return `OBS 工作階段無效或已過期，${obsRecoveryHint}`;
      case 403: return `OBS 權限不足，${obsRecoveryHint}`;
      case 404: return `找不到此${resource}，${obsRecoveryHint}`;
      case 429: return "OBS 請求過於頻繁，請稍候再試。";
      default: return `無法載入${resource}（HTTP ${cause.status}），請稍後重新整理；若持續發生，${obsRecoveryHint}`;
    }
  }
  if (cause instanceof TypeError) return "OBS 網路連線失敗，請確認網路連線後重新整理。";
  if (cause instanceof Error) return cause.message;
  return `無法載入${resource}，${obsRecoveryHint}`;
}
