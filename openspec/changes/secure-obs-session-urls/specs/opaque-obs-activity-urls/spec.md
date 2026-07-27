## ADDED Requirements

### Requirement: Donate 活動具有公開 GUID 識別碼
每個 Donate 抽獎活動 SHALL 具有非空的 `PublicId` GUID，並且 repository MUST 將其保存至活動資料。系統讀取沒有 `PublicId` 的舊活動時 MUST 補齊一個 GUID。

#### Scenario: 建立新活動
- **WHEN** 使用者建立 Donate 活動
- **THEN** 活動在儲存前具有非空 GUID `PublicId`

#### Scenario: 讀取既有 YAML 活動
- **WHEN** repository 載入缺少 `PublicId` 的活動資料
- **THEN** 載入的活動具有新產生的非空 GUID `PublicId`

### Requirement: 新 Donate OBS URL 使用 GUID
Donate 活動列表 SHALL 以活動的 `PublicId` 產生 OBS URL，並以 GUID 尋找活動。系統 MUST 繼續接受既有的整數活動 ID 路由，供歷史 OBS 連結使用。

#### Scenario: 複製新 OBS URL
- **WHEN** 使用者從已啟用 Donate 活動取得 OBS URL
- **THEN** URL 路徑包含活動的 GUID `PublicId` 而非遞增整數 ID

#### Scenario: 開啟舊 OBS URL
- **WHEN** 使用者使用含整數活動 ID 的既有 Donate OBS URL
- **THEN** 系統仍可找到對應活動並顯示該活動的 overlay
