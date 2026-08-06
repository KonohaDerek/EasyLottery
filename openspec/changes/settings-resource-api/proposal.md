# 設定資源 API

## Why

整份 `/settings` YAML 讀寫會讓不同設定頁互相覆蓋，也不利於 API 授權、版本衝突與未來切換資料庫。

## 背景

目前部分設定頁仍透過 `/settings` 整份 YAML 讀寫，容易在多個瀏覽器同時編輯時互相覆蓋，也讓 API 資源邊界不清楚。

## 目標

將 OBS 版面、音效、視覺樣式、付款與加班台設定拆成獨立 REST 資源。舊 `/settings` YAML 端點保留作為匯入、匯出及 OBS 相容投影，但不再是管理頁的主要寫入路徑。

## 範圍

- 影響 `EasyLotteryApplication` 的 CQRS、`EasyLotteryInfrastructure` 的 YAML repository、`EasyLotteryAPI` 的 endpoints 與 `EasyLotteryWasm` 的設定 client/page。
- 保留既有 YAML 檔案格式與舊端點相容性。

## 非目標

- 本變更不新增資料庫 provider，也不改變 Donate 活動或抽獎結果資源。

## 目標

- 以 MediatR Query/Command 統一讀寫流程。
- 以 repository section update 只修改目標設定區段。
- 以 ETag/If-Match 偵測並拒絕覆蓋式版本衝突。
- 付款資源遮罩敏感金鑰並保留未修改的密鑰。

## 風險與驗收

- 舊版 OBS 投影仍使用相容端點，因此 migration 期間不會中斷既有 OBS URL。
- API 回歸測試驗證五個資源、ETag 衝突、敏感值遮罩與相容端點標記。
