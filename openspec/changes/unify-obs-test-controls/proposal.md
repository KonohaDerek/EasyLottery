## Why

活動列表同時提供正式 OBS URL 與測試 OBS URL，但兩者用途重疊；正式 URL 缺少控制介面，實況主無法先在同一個畫面測試，再直接擷取需要的區塊。戳戳樂與轉盤也必須操作額外按鈕，降低 OBS 測試流程的直覺性。

## What Changes

- Donate、戳戳樂與轉盤列表只保留「開啟測試 OBS」按鈕。
- 測試 URL 統一使用 `controls=1` 與 control scope token。
- 戳戳樂隨機模式可直接點擊格子觸發隨機揭露。
- 轉盤可直接點擊轉盤區域（或使用鍵盤 Enter／Space）開始倒數抽獎。

## Non-Goals

- 不變更抽獎機率、動畫、結果記錄或 OBS token 的資源 scope 驗證。
- 不移除預覽路由；預覽路由仍導向同一個控制模式 OBS 畫面。
