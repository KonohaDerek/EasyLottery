## ADDED Requirements

### Requirement: 可選 WebM Donate 動畫

Donate 活動設定 SHALL 支援啟用 WebM 動畫、WebM URL、poster URL 與循環播放設定；未啟用或未設定 URL 時 SHALL 使用既有 CSS 動畫。

#### Scenario: 使用 WebM 動畫

- **WHEN** 活動啟用 WebM 且 URL 可播放
- **THEN** OBS 動畫場景播放 WebM，贊助者與獎品文字仍由 HTML 顯示

#### Scenario: WebM 載入失敗

- **WHEN** WebM 載入發生錯誤
- **THEN** OBS 場景切換至該活動原本的 CSS 動畫，且抽獎流程繼續

### Requirement: WebM 播放與結果生命週期同步

非循環 WebM 播放完成後 SHALL 進入結果階段；循環 WebM 或無法取得播放完成事件時 SHALL 依活動設定的動畫秒數結束，結果停留秒數仍 SHALL 有效。

#### Scenario: 非循環影片結束

- **WHEN** WebM 在設定秒數前播放完畢
- **THEN** 系統立即顯示結果，不等待下一次輪詢

#### Scenario: reduced motion

- **WHEN** URL 包含 `motion=reduce` 或瀏覽器偏好 reduced motion
- **THEN** 系統不播放 WebM，改用靜態／CSS fallback

### Requirement: WebM OBS 資產

OBS 資產庫 SHALL 提供 WebM 影片用途，且 Video 資產只接受 `video/webm` MIME 類型。

#### Scenario: 上傳 WebM

- **WHEN** 使用者以 WebM 用途上傳 `video/webm`
- **THEN** 系統保存資產並提供可供 OBS 瀏覽器來源讀取的 URL

#### Scenario: 拒絕其他影片格式

- **WHEN** 使用者以 WebM 用途上傳非 `video/webm` 檔案
- **THEN** 系統拒絕上傳並回傳用途與 MIME 不相容錯誤
