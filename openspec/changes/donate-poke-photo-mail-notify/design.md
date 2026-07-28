## Context

系統已具備綠界、藍新、oen.tw、Twitch 小奇點與 YouTube SuperChat 的設定模型及 UI 啟用限制，也已有 SMTP 結果通知與 Donate 的拍立得活動類型。現有拍立得顯示仍與 Donate OBS 頁面耦合，且 OpenAI Key 尚未形成可測試的文案提供者流程。

## Goals / Non-Goals

**Goals:**

- 維持來源僅在有效設定存在時可啟用的規則。
- 保持 SMTP 不完整時不寄信且不影響抽獎流程。
- 將拍立得的背景、標題與恭喜文案整理為可選模板資料，而非散落在 overlay 樣式。
- 在 AI 未設定、請求失敗或回傳空白時，穩定使用模板 fallback 文案。

**Non-Goals:**

- 不新增尚未有 callback 實作的斗內平台實際 webhook 整合。
- 不在 WASM 直接呼叫外部 AI API 或暴露 OpenAI Key。
- 不把 SMTP 密碼或 AI Key 回傳至瀏覽器。

## Decisions

### 設定可用性作為單一啟用條件

來源是否可啟用以目前環境的連線設定是否完整為準，UI disabled 僅是輔助；資料正規化在儲存時再次強制關閉未設定來源。這避免匯入 YAML 或手動 API 請求繞過 UI。

### 模板資料與 Donate 活動分離

拍立得模板以獨立設定模型保存，活動只選擇模板 key。overlay 依 key 讀取背景、標題與 fallback 文案，找不到模板時回退到內建預設，讓既有活動不需資料遷移即可正常顯示。

### AI 文案在 API 邊界處理並保有 deterministic fallback

AI provider 為 API 端可替換介面；只在模板啟用且伺服器已有安全保存的 key 時呼叫。任何設定、網路或回覆錯誤都回傳模板 fallback，抽獎結果與 overlay 不等待或依賴外部服務成功。

## Risks / Trade-offs

- [AI 呼叫延遲或失敗] → 非同步取得，失敗時立即使用模板 fallback。
- [舊活動沒有模板 key] → 預設模板 key 與正規化確保相容。
- [SMTP 設定錯誤] → 寄信失敗僅記錄 warning，不回滾已完成的抽獎。
- [來源規則在多處重複] → 以 model 的 `HasConfiguration` 與儲存時正規化為單一事實來源。

## Migration Plan

1. 加入內建拍立得模板與空白安全預設值。
2. 舊 Donate 活動讀取時自動選用預設模板。
3. 將 AI key 僅保留於 API 的秘密設定，WASM 只讀取是否可使用 AI 的旗標。
4. 可隨時停用模板的 AI 文案，系統立即回退成模板文字。

## Open Questions

- 首版採用的 AI 模型與速率限制，將在 API provider 實作時以部署設定決定。
