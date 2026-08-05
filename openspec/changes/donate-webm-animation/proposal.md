## Why

Donate 目前的 CSS 動畫容易維護動態獎品資料，但複雜角色動作與美術效果受限。需要支援透明 WebM 素材，同時保留 CSS fallback，避免 OBS 載入失敗時中斷抽獎結果流程。

## What Changes

- Donate 活動可設定 WebM 動畫 URL、poster、循環播放與啟用狀態。
- OBS 動畫場景優先播放 WebM，載入失敗、未設定或 reduced-motion 時使用既有 CSS 動畫。
- WebM 動態播放只負責視覺效果；贊助者、留言、獎品名稱與結果仍由 HTML 呈現。
- WebM 播放結束後可立即進入結果階段；循環 WebM 仍由動畫秒數控制。
- OBS 資產庫新增 `Video` 類型並限制為 `video/webm`。

## Non-Goals

- 不將抽獎機率或結果判定移到影片。
- 不移除既有五種 CSS 動畫。
- 不把贊助者與獎品文字硬編碼進影片。
