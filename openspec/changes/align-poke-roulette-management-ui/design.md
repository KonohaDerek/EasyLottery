# 設計

## 統一管理頁

戳戳樂與轉盤管理頁採用與 Donate 活動相同的三段結構：

1. 頂部 Card 說明用途，提供新增及載入預設模板。
2. Scrollable Modal 負責新增與編輯。
3. Table 顯示主要摘要，操作欄提供編輯、OBS、測試 OBS、複製、匯出與刪除。

## 編輯模型

開啟編輯時建立完整深層複本，包含格子或轉盤項目；只有按下儲存才呼叫 Domain Service。取消或關閉 Dialog 不會修改列表中的原始物件。

發布狀態在 Dialog 以「啟用」Switch 呈現：啟用對應 `Published`，關閉對應 `Draft`。既有 enum 與儲存格式不變。

## OBS 操作

管理頁初始化時取得 WASM OBS session token。正式 URL 使用既有 `/obs/pokebox/{id}` 或 `/obs/roulette/{id}`；測試 URL額外帶入 `controls=1`。兩者都自動附加 session token。

## 相容性

保留既有 `/pokebox/editor/{id}`、`/roulette/editor/{id}` 與 preview 路由，避免破壞外部連結，但新的主要操作不再依賴頁面跳轉。
