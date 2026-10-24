# 設計：Donate 抽獎紀錄與管理導覽

## 導覽資訊架構

使用共用的 Vue 導覽定義，讓桌面 drawer 與窄版 menu 使用同一組項目：

1. 抽獎與活動：抽獎工作區、Donate 活動、活動市集。
2. 直播互動：互動回合、觀眾身份綁定、加班台設定。
3. 抽獎模板：轉盤、戳戳樂。
4. 結果與紀錄：活動結果、Donate 抽獎紀錄、審計紀錄。
5. OBS 與視覺：OBS 版面、OBS 資產、音效預設、視覺樣式。
6. 平台與支付：直播平台串接、YouTube 直播設定、支付配置。
7. 系統設定與安全：管理員 Passkey、設定備份。

active route 使用最長符合路徑，根路由只精確符合 /，避免首頁連結被其他路由誤標。active route 透過 aria-current="page" 表示。hover 使用中性背景，focus-visible 使用清楚 outline，active 使用獨立色彩／側邊標記。

## Donate 查詢契約

- 管理 API：GET /api/donate-lottery-draw-records。
- 查詢參數：本地日期轉為 UTC 半開區間、activityId、outcome=all|winning|miss、search、page、pageSize。
- page 預設 1；pageSize 預設 20 並限制 1–100。
- 回傳：items、page、pageSize、totalCount、totalPages。
- 以 DrawnAtUtc 新到舊排序，ID 作穩定次序；只讀取既有 IEasyLotteryConfigStore。
- 活動名稱由目前活動清單投影；若活動已刪除，以「已刪除活動 #<ActivityId>」顯示，不刪除或重建歷史。
- DTO 提供必要紀錄欄位：紀錄 ID、活動 ID／名稱、抽獎時間、贊助者顯示名稱、金額、結果、獎項、抽獎次數。排除付款外部 ID、付款方式、捐款訊息及其他 secret。
- 搜尋只比對活動 ID／名稱、贊助者顯示名稱和獎項，不掃描私人訊息。
- 權限使用既有 ObsSessionAccess.RequireAdmin，公開與 OBS scoped token 一律不能取得完整管理紀錄。

## 核心抽獎保存契約

- 每次核心抽獎在呼叫既有前端抽選前產生 UUID `drawId`，並先確認含完整既有名單、外部身份及規則輸入的 pending payload 可暫存。伺服器保存 UUID、記錄時間、得獎籤 ID、得獎顯示名稱、獎項名稱、完整抽獎當下參與籤快照及規則快照。
- 名單只複製既有 Participant 欄位：籤 ID、lotteryGroupId、顯示名稱、既有 platform/accountId（若有）、等級、分組、標籤及明確資格狀態。資格依現有 `isWinner` 規則保留為 `eligible` 或 `already-won`；不收集 API key、token、密碼、聊天室訊息或其他新增欄位。
- 規則快照固定記錄抽選演算法版本、既有得獎者排除政策、同 lotteryGroupId 中獎後的群組政策、合格籤數、首個剩餘獎項政策及抽獎當下完整獎項佇列。所有參與籤按抽獎順序保存；winnerParticipantId 指向實際抽中籤。
- `POST /api/lottery-draw-records` 對相同 UUID／相同完整結果與快照冪等回傳既有紀錄，不新增第二筆；相同 UUID 但任一得獎者、規則、名單、身份或名單順序不同即回 409。
- `GET /api/lottery-draw-records/{drawId}/summary` 是保存回應不明時的對帳摘要；用 `/participants?page=…&pageSize=100` 逐頁取得名單，完整比對 UUID、結果、規則與名單。網路錯誤或 401/403 只可重新查詢。僅在摘要明確 404 後，主持人可選擇用本機保留的原結果與原 UUID 再送一次；不可產生新 UUID 或重新抽選。單筆查詢與保存回應只傳摘要。
- pending 時鎖定抽獎、名單及獎項異動。前端只在 API 確認完整快照相同後更新為已揭曉；歷史頁讀取伺服器持久紀錄。
- 管理歷史列表只回摘要（不含 participant roster 或外部帳號）；按記錄時間新到舊排序，UUID 作穩定次序，每頁最多 100 筆。管理員單筆摘要及參與名單查詢都用既有 `RequireAdmin`；名單另以最多 100 筆分頁回傳。匿名及 OBS scoped session 不得取得完整名單。
- 不回填核心結果歷史功能啟用前未保存的舊抽獎結果。
- 抽選前以瀏覽器儲存容量預檢完整 pending snapshot；若瀏覽器配額不足，抽選不會開始。API 接受單筆完整保存請求至 16 MiB；超過上限時保留 pending 結果、鎖定不重抽並提示管理員處理。

## 前端狀態

Vue 紀錄頁以明確「查詢」操作送出篩選，頁碼變更重新查詢。顯示尚未載入、載入中、錯誤（含重試）、無紀錄、無符合條件、列表與分頁。日期欄位採瀏覽器本地日界線，轉成 UTC 半開區間，UI 顯示當地時間。

## 測試策略

- Vitest：路由分群、唯一 active path、根路由、巢狀模板路由及紀錄篩選 query builder。
- API integration：UUID 與完整 roster/rules 快照冪等、任一身份／規則差異衝突、GET 對帳、匿名／OBS token 拒絕、管理員允許、摘要與 roster 分頁、YAML 與 SQLite roundtrip、DTO 不包含額外 secret。
- Playwright：桌面／390px 窄版群組展開、鍵盤操作、active/hover 分離、直接載入與重新整理、核心保存成功／保存不明鎖定並 GET 對帳、紀錄空／錯誤／載入／分頁狀態。
- 測試使用人工合成紀錄，不連正式儲存、不呼叫支付或抽獎。
