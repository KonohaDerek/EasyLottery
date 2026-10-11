# Vue 核心抽獎工作區遷移

## 背景

Phase 3 已遷移 Donate、活動結果、Roulette 與 PokeBox，但首頁 `/` 仍由 Blazor WebAssembly 提供。該頁包含參加者與獎項管理、抽獎、JSON/CSV 匯入匯出、YouTube 會員 CSV 及直播聊天室過濾。Vue 尚無對應 route 或核心抽獎型別。

原頁在 WASM 執行 YouTube Data API 呼叫；API 將 YouTube API key 遮罩後才回傳瀏覽器，因此直接搬移呼叫會破壞功能或把憑證放進不安全邊界。新實作必須由伺服器使用既有設定取用金鑰，瀏覽器只收到最小化訊息資料。

## 目標

- 將 `/` 的核心抽獎工作區遷移至 Vue 3，並保留舊 Blazor 頁作為可回退路徑。
- 保留抽獎、匯入匯出、會員倍率、聊天室條件與通過條件訊息之 SuperChat feed 效果。
- 將 YouTube key 留在伺服器端，使用管理員 session token 保護新 API。
- 使用資源型、ETag 保護的 DrawingRules API；不再由 Vue 寫回整份 YAML。

## 範圍

- Vue `/` route、管理員版面、抽獎頁、純函式狀態/匯入匯出/聊天室過濾邏輯與測試。
- 新增或擴充受管理員授權保護的 DrawingRules settings resource，維持既有儲存 provider 與 ETag 慣例。
- 建立伺服器端 YouTube 狀態、直播聊天室解析及訊息讀取 adapter；僅回傳 UI 所需欄位，不回傳 API key。
- 使用既有 overtime feed API 發佈符合舊行為的 SuperChat 事件。
- API 的精確 `/` Vue fallback、舊頁 `/legacy/` 回退路徑、Vitest、API/Domain 測試及 focused Playwright。

## 非目標

- 移除 Blazor WebAssembly、Blazorise 或任何未遷移 route；Phase 5 仍由 #270 管理。
- 更改抽獎規則、Payment、Domain protocol、YouTube/OAuth 設定或正式環境。
- 將名單、獎項、得獎紀錄改為伺服器持久化；現行頁面這些狀態只存在記憶體。
- 修改任何圖片或動畫資產；核心抽獎頁沒有此類資產，若將來需要則須符合使用者指定的精緻動畫風格。
- 以 Chromium 測試取代原生 OBS Browser Source 驗證。

## 驗收標準

- `/` 直接載入及重新整理 Vue；未登入時依 Vue 管理員 guard 轉到登入頁；舊 Blazor host 與 legacy fallback 保留。
- 新增/移除參加者與獎項、群組篩選、抽獎、得獎者不重複及獎項 FIFO 消耗符合原行為。
- Participant JSON/CSV 欄位與 Prize CSV 欄位相容；匯入有效資料取代清單並清除 Winners；錯誤或空內容不破壞原資料；匯出 CSV 正確處理引號/逗號/換行。
- YouTube Studio 會員 CSV 依中文欄名解析，依伺服器 DrawingRules 等級倍率展開會員；倍率設定以 ETag 保護保存，stale write 回傳衝突。
- 直播聊天室保留關鍵字包含比對、黑名單優先、白名單、大小寫不敏感 cooldown、5 秒輪詢與按時間游標；拒絕的訊息不得成為參加者或發佈 SuperChat。
- YouTube API key 不會出現在 Vue bundle、HTTP response、瀏覽器 storage、URL 或 client log；新 YouTube API 需管理員 token。
- 僅符合舊流程之 SuperChat 訊息寫入既有 overtime feed，欄位與 micros 換算相容；無真實 YouTube 呼叫的可重現測試覆蓋正常、錯誤與空狀態。
- Vue tests/typecheck/build、相關 API/Domain tests、focused Playwright、strict OpenSpec 及 diff checks 通過；驗證紀錄列出環境和未涵蓋項目。

## 測試策略

- Vitest：抽獎、標籤與群組、匯入匯出、會員倍率、關鍵字/名單/cooldown、游標去重、SuperChat mapping 與 API client 錯誤。
- API tests：未授權拒絕、DrawingRules ETag/stale update、YouTube response 不含 key、adapter 正常/無資料/失敗；測試 provider 使用 fake，不呼叫 YouTube。
- Playwright：`/` route/refresh/auth、CRUD、draw、匯入匯出、YouTube mock flow、服務錯誤提示與 legacy fallback；不連接真實 provider。
- Dev Container 中執行必要的 Vue、API、Domain 與整體回歸檢查；不做 production 或原生 OBS 操作。

## 風險、回復與觀測性

- 主要風險為抽獎分配偏差/重複得獎、CSV 相容性、聊天室輪詢重疊、API key 洩漏及 settings stale write。
- 對抽獎選取使用可注入隨機來源以便測試；聊天室 client 停止時取消 timer/request，並避免同時輪詢；Etag 衝突提示重新載入設定。
- log 僅記錄錯誤類別/狀態，不記錄 YouTube key、session token、完整直播網址或訊息內容。
- 若驗證失敗，撤掉 Vue `/` route/fallback，恢復原 Blazor root；新 API 為 additive 且不改既有路由/資料格式。無需資料遷移。
