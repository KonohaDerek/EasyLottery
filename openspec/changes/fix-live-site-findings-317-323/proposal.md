## Why

#317–#323 發現未知路由、Passkey 帳號辨識、公開頁與 OBS 錯誤狀態缺陷。使用者要求直接處理；原 live suite 的預期失敗不能當作產品通過。

## What Changes

- 未知 API/hub 回 JSON404，未知頁面回 Vue404；保留 legacy 路由及 settings API。
- Login options 使用穩定誘餌避免帳號辨識，驗證錯誤統一，註冊資訊限定受信來源。
- 改善公開手機導覽、h1/ARIA、能力範圍文案、favicon 及 robots。
- OBS 無可顯示內容時提供可行動錯誤，正常閒置維持透明。

## Capabilities

### New Capabilities

- `live-findings-remediation`: 路由、Passkey、公開頁與 OBS 回歸要求。

### Modified Capabilities

None.

## Impact

影響 API、Vue、Wasm 靜態檔與測試。非目標：新增核心歷史、正式部署/操作、清理任何既有資料。main 已含 PR314，不將未部署等同未實作。
