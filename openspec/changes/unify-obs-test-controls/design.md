## 設計

管理頁只為每個資源簽發一枚 `control` scope token，URL 固定為 `?controls=1#sessionToken=...`。這讓開啟測試、手動觸發與 OBS 擷取使用同一個頁面，避免正式 URL 與測試 URL 的行為分歧。

戳戳樂的格子點擊在手動模式維持原本指定格子行為；隨機模式則轉送到既有 `PokeRandom` 流程。轉盤將點擊事件掛在轉盤容器，控制按鈕阻止事件冒泡，避免按鈕操作重複觸發；鍵盤 Enter／Space 也會觸發同一流程。
