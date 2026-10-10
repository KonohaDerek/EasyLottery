# Vue Donate activity management

## ADDED Requirements

### Requirement: 管理員必須登入才能管理 Donate 活動

Vue Donate 活動管理 route MUST 使用既有 admin session guard。未授權時 MUST 導向登入頁，且不得先呼叫受保護的 Donate activity API。

#### Scenario: 未登入者開啟 Donate 活動管理

- **WHEN** 未登入使用者直接開啟 `/donate-activities`
- **THEN** Vue 導向 `/login` 並保留安全的 return path
- **AND** 不呼叫 `/api/donate-activities`

### Requirement: Donate 活動 CRUD 維持既有 API 與模型欄位

管理頁 MUST 使用既有 `GET /api/donate-activities`、`POST /api/donate-activities`、`PUT /api/donate-activities/{id}` 與 `DELETE /api/donate-activities/{id}`。請求 MUST 使用共用 API client 自動附加管理員 session header，並保留 activity/prize 識別、活動設定與剩餘庫存欄位。

#### Scenario: 管理員新增、編輯與刪除活動

- **WHEN** 管理員完成有效編輯器並保存新活動
- **THEN** 使用 POST 並提交既有 Donate activity JSON shape
- **WHEN** 管理員保存既有活動
- **THEN** 使用該活動 ID 的 PUT，保留其 public ID、type、prize IDs 和 remaining quantities
- **WHEN** 管理員確認刪除
- **THEN** 使用 DELETE 該活動 ID，並提示既有抽獎紀錄會保留

#### Scenario: API 讀取失敗

- **WHEN** 初次載入或重新載入 API 失敗
- **THEN** 頁面顯示錯誤及重試操作，不把錯誤呈現成空資料

### Requirement: 編輯器保留既有 Donate 活動功能

活動編輯器 MUST 可設定動畫、結果模板、AI 文案、Donate 資訊顯示、結果與動畫秒數、WebM URL／poster／循環、活動期間、啟用狀態及獎項欄位。日期 MUST 以本地時間編輯並以 UTC 儲存。獎項機率合計超過 100% 時 MUST 阻止提交。

#### Scenario: 管理員編輯活動並使用本機 OBS 資產

- **WHEN** 管理員選取符合用途的本機圖片或 WebM 資產
- **THEN** 編輯器將既有 `/api/obs-assets/{id}/content` URL 寫入相應欄位
- **AND** 儲存後保留獎項 ID 與庫存值

#### Scenario: 獎項機率超過上限

- **WHEN** 獎項機率總和大於 100%
- **THEN** 頁面顯示超限錯誤並停用保存操作

### Requirement: 測試 OBS session token 不可放在 URL query

測試 OBS 操作 MUST 透過既有 `/api/obs-sessions` 建立 Donate `read`/`control` token。管理頁 MUST 只在使用者明確操作時申請 token，並將 token 放在 OBS URL fragment，不得放入 query、記錄到 console 或持久化至瀏覽器 storage。

#### Scenario: 管理員開啟測試 OBS

- **WHEN** 管理員使用啟用活動的「開啟測試 OBS」操作
- **THEN** 以管理員 session header 發行 token
- **AND** 開啟 `/obs/donate/{publicId}?controls=1#sessionToken=...`
- **AND** query string 不含 token

### Requirement: Blazor Donate 活動頁可回退

Vue host MUST 只對 `/donate-activities` 使用 Vue shell fallback，且既有 Blazor 頁 MUST 可透過 `/legacy/donate-activities` 存取。非此 slice 路由及活動／抽獎後端 MUST 保持不變。

#### Scenario: 回退至 Blazor 活動管理

- **WHEN** 管理員從 Vue 頁使用舊版管理連結
- **THEN** 瀏覽器載入 `/legacy/donate-activities` 並呈現既有 Blazor 活動頁
