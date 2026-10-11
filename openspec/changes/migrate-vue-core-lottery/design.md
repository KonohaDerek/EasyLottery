# 設計

## 路由與登入

- Vue Router 將 `/` 設為 `requiresAuth`，透過既有 AdminLayout/session store 保護管理員工作區。
- API host 僅為精確 `/` 回傳 Vue shell；Blazor 舊首頁保留 `/legacy` route，通用 fallback 仍供未遷移頁使用。

## Vue 核心模組

- `src/lottery/draw.ts` 持有 Participant/Winner 型別及純抽獎、篩選、標籤正規化函式；抽獎只從未得獎參加者選一位，使用第一項獎項並標記得獎。
- `src/lottery/import-export.ts` 處理 JSON、CSV 與 YouTube Studio CSV；CSV parser/serializer 保留 RFC 4180 引號、逗號與換行處理，並支援舊 Participant/Prize 欄位名稱。
- `src/lottery/chat.ts` 對齊 `ChatRoomCatch.IsMessageAllowed` 規則；純邏輯接受 clock 與 cooldown map，方便測試。
- `LotteryHomeView.vue` 組合 API clients、表單與清單；資料仍是頁面記憶體狀態，不建立伺服器抽獎 session。

## DrawingRules API

- 新增 `/api/settings/drawing-rules` GET/PUT，經 `ObsSessionAccess.RequireAdmin` 驗證，沿用 `ISettingsSectionRepository`、現有兩種儲存 provider 及 ETag。瀏覽器只交換 `DrawingRuleSettings`；不 round-trip 整份 YAML 或 secrets。
- 將 `DrawingRules.LevelRates` 的缺省等級倍率設為 1，避免改變現有等級權重；PUT 要求具體 If-Match，缺少或 wildcard 回傳 428，stale ETag 回傳 409。SQLite 在立即交易中完成 ETag 檢查與寫入，避免併發請求以同一版本都通過；舊版整份設定的 load-edit-save 若未修改 DrawingRules，保存時會保留更新後的 DrawingRules。SQLite `/settings` 相容端點也在同一立即交易中重驗 ETag、合併並保存。

## YouTube server boundary

- 新增伺服器 adapter 注入既有 `YouTubeServiceHelper`，其 `SystemSettingsService` 由 server-side `IEasyLotteryConfigStore` 讀取未遮罩設定；不要在 Vue 使用 Google Data API 或回傳 key。
- 端點包含管理員授權的 credential status、直播 URL→liveChatId resolve、liveChatId→訊息列表。請求需驗證欄位長度/格式、套用 `sensitive` rate limit。
- 回應僅含 id、publishedAt、type、author、display text、avatar URL、SuperChat amount/display/currency/comment 等 UI 欄位。錯誤回應不包含原始 URL/key。
- Vue 每 5 秒輪詢；用 `AbortController` 與 session generation 取消進行中的解析／讀取及防止過期連線更新狀態，單一輪詢鎖避免重複處理。

## 聊天、得獎與 SuperChat

- 保留舊過濾順序：作者有效、關鍵字子字串、黑名單排除、非空白名單允許、同帳號 cooldown。被拒訊息仍推進最後已觀察發佈時間，避免每輪重處理。
- 相同規則允許的作者只新增一筆「聊天室」參加者；不移除原有同名處理差異。
- 通過規則且屬 SuperChat 的訊息映射為現有 `OvertimeSupportEvent`，AmountMicros / 1,000,000；透過 `/api/obs-sessions` 以管理員 session 簽發僅含 overtime `control` scope 的短期 token，呼叫專用 append endpoint。伺服器在既有 feed mutation transaction 中依 YouTube message ID 去重並保留其他事件，不讀取或整份回寫 feed。舊版整份 feed PUT 也以原子 mutation 合併仍存在的 SuperChat，避免舊讀取快照清除新事件；滿額時優先保留此次傳入的新事件，並移除最舊的既有事件以維持 30 筆上限。
- Vue 登出時清除快取的短期 overtime token；DrawingRules 初始讀取未取得具體 ETag 時停用寫入與自動保存。
- 畫面不新增圖片、GIF 或影片；抽獎揭曉使用細緻 CSS 動效，並支援 `prefers-reduced-motion`。

## 回復與驗證

- 回復只需移除 Vue root route/fallback，仍保留 Blazor `Home.razor`；DrawingRules 與 YouTube API 為 additive，不需資料轉換。
- Vitest 覆蓋純邏輯，API tests 使用 fake YouTube adapter，Playwright mock 所有 YouTube 呼叫。測試不得讀取或印出任何真實 API key。
- log/error 僅帶 operation 與狀態，不含聊天內容、key、token 或原始直播 URL。
