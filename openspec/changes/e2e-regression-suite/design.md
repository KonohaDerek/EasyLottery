# Design

Playwright 使用 API request fixture 建立短生命週期模板，再以 read/control OBS session token 開啟正式頁與控制頁。每個測試在 finally 清理建立的資源，避免測試資料污染。瀏覽器 console、page error 與 request failure 由共用 fixture 收集，只有失敗測試寫入 output directory，沿用既有 CI artifact upload。

