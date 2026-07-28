## ADDED Requirements

### Requirement: AI 恭喜文案 fallback
系統 SHALL 僅在拍立得模板明確啟用 AI 文案且 API 端已配置 provider 時請求 AI 文案。AI 未設定、失敗或回傳空白時 MUST 使用模板的 fallback 恭喜文案，且 MUST 不將秘密 key 傳送至瀏覽器。

#### Scenario: AI 無法使用
- **WHEN** 模板啟用 AI 文案但 provider 未設定或請求失敗
- **THEN** overlay 顯示模板 fallback 恭喜文案並正常呈現抽獎結果
