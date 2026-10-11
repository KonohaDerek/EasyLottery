# Vue Donate OBS overlay 規格

## ADDED Requirements

### Requirement: Vue 與 Blazor 可並行回復

系統 MUST 以 Vue 呈現 `/obs/donate/{activityRouteKey}`，並保留同一 Blazor overlay 的 `/legacy/obs/donate/{activityRouteKey}` 回退路徑。兩者 MUST 使用既有後端 API 與資料格式。

#### Scenario: Vue OBS route

- **WHEN** OBS 載入有效 Donate activity route key
- **THEN** API host 回傳 Vue shell，瀏覽器不啟動 Blazor WebAssembly runtime
- **AND** Vue 讀取同一活動與 draw records

#### Scenario: Legacy fallback

- **WHEN** 使用者開啟 legacy Donate OBS URL
- **THEN** 原 Blazor component 仍可載入

### Requirement: 結果按序揭露且不重播

Donate OBS MUST 將新結果依 record ID 去重、排序並逐筆揭露。首次 snapshot MUST 將最新既有 record 設為去重游標，且不回播任何既有結果；活動切換 MUST 清除先前 queue 與 reveal lifecycle，並以新活動 snapshot 建立游標。

#### Scenario: Poll discovers multiple results

- **WHEN** 一次 snapshot 包含多筆尚未看過的結果
- **THEN** 結果按 ID 遞增加入 queue 並逐筆顯示
- **AND** 後續重複 snapshot 不會再次 enqueue 同一筆

#### Scenario: Initial snapshot and activity switch

- **WHEN** overlay 第一次讀取既有 draw records
- **THEN** 不顯示任何既有結果，並只等待此 snapshot 之後的新 records
- **WHEN** route key 導向另一活動
- **THEN** 舊 queue/state 不會出現在新活動

### Requirement: OBS token 與測試 Donate 維持既有安全邊界

Vue overlay MUST 使用現有 OBS scoped session token 和 endpoint 授權；MUST 在 bootstrap 後清除 fragment token。測試 Donate MUST 只送至既有測試 endpoint，不得觸發 payment request。

#### Scenario: Token bootstrap

- **WHEN** URL fragment 包含 OBS session token
- **THEN** overlay 使用 token 呼叫現有 scoped APIs 並立刻清理 fragment
- **AND** token 不會出現在後續 history 或診斷 log

#### Scenario: Test donation

- **WHEN** 使用者送出 Donate 測試表單
- **THEN** overlay 呼叫現有 `/api/obs/donate/{publicId}/test`
- **AND** 不會呼叫 payment provider 或建立真實付款請求

### Requirement: Reveal animation 可復原且尊重 reduced motion

Overlay MUST 保留現有通知、動畫/媒體、結果、獎品圖片 fallback 與透明呈現。系統偏好或 `motion=reduce` MUST 停用非必要動態效果；webm ready/fail/end 的所有路徑都 MUST 讓 reveal lifecycle 可繼續。

#### Scenario: Animation video failure

- **WHEN** 非循環 webm 載入或播放失敗
- **THEN** reveal 不會永久等待影片事件
- **AND** 後續結果仍可依序顯示

#### Scenario: Reduced motion

- **WHEN** 系統或 OBS URL 要求 reduced motion
- **THEN** overlay 不播放非必要過場/影片效果
- **AND** 結果資訊仍可讀取
