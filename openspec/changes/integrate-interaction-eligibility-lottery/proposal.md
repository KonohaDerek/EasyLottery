# 將互動合格快照接入核心抽獎

## 背景

互動回合已有簽到、穩定 AudienceProfile 身份與 eligibility preview/import；核心抽獎目前仍由手動名單、檔案或 YouTube 會員 CSV 建立候選者，兩者沒有串接。顯示名稱會變更，也不適合作為身份鍵。抽獎歷史已有不可變參加者快照，需要擴充來源欄位才能事後追溯。

## 目標

- 管理員可選擇互動回合、預覽合格人數與匯入差異，並明確確認匯入。
- 同一 round/snapshot 重複匯入具冪等性；以 AudienceProfile ID 去重，不以名稱比對。
- 匯入來源及 Profile ID 隨參加者進入鎖定 pool 與抽獎歷史，後續身份合併、解除、改名或新簽到不改寫既有抽獎證據。
- 抽獎前可鎖定整份候選 pool；鎖定後不得由手動名單修改、匯入或聊天輪詢靜默改變抽獎資格，需明確解鎖並再次確認新 pool。
- 手動參加者保留原有抽獎行為；同名不代表同一身份。只有可確認的穩定 profile identity 才去重，衝突要清楚呈現。

## 範圍

- OpenSpec change `integrate-interaction-eligibility-lottery`。
- Vue 核心抽獎頁、互動回合/Eligibility API 用戶端、participant 與 draw snapshot 型別、必要的 API preview 投影，以及 .NET/Vitest/Playwright 驗證。
- 鎖定 pool 的分頁重載恢復使用 tab-scoped sessionStorage；只保存必要名單快照，不包含連線憑證。
- 抽獎歷史保存 round ID、eligibility snapshot ID、AudienceProfile ID 及抽獎當下顯示名稱/可用外部帳號識別；歷史 API 維持管理員授權。

## 非目標

- 不改互動 connector、OAuth、正式平台串接、原生 OBS 或公開 OBS overlay。
- 不把顯示名稱當主鍵，不在歷史結果中套用身份合併/解除的回溯修改。
- 不改手動抽獎、會員 CSV 權重、既有得獎排除、抽獎演算法或結果持久化協定。
- 不部署、不合併、不處理正式資料。

## 驗收

以 GitHub Issue #305 的六組驗收條件為準；包含預覽與明確匯入、冪等與手動衝突、pool 鎖定、歷史來源追溯與私有欄位、空/失效/錯誤/重複操作/重載/身份合併解除，以及手動抽獎回歸。

## 風險與回滾

- 新增的歷史來源欄位皆 nullable，舊 YAML/JSON 歷史載入時維持 null；不需資料搬遷。回滾時舊版忽略新增欄位。
- sessionStorage 只用於同一分頁恢復暫存或鎖定 pool；格式無效或版本不符時顯示可恢復錯誤，不得自動解鎖或抽獎。
- 如 API/DTO 或識別資料範圍無法以管理員授權隔離，縮小功能並記錄，不可改用名稱猜測身份。

## 可觀測性

- UI 顯示所選 round 名稱/ID、snapshot 版本、匯入新增/重複/衝突數、鎖定 pool 時間與 hash 短碼。
- API 只輸出彙總或管理員限定的來源欄位；不記錄連線憑證、聊天內容或不必要的外部身份資料。
- 驗證紀錄包含最終 SHA、命令、通過/失敗/未執行項目及 E2E host 限制。
