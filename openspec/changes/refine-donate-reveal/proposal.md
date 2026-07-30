## Why

Donate OBS 已能先呈現贊助通知並執行抽獎動畫，但揭曉階段重複顯示贊助資訊與獎項名稱，造成版面過大且資訊焦點不清。直播主若已使用其他 Donate 擷取工具，也需要能略過本系統的贊助通知。

## What Changes

- 新增每個 Donate 活動的「顯示 Donate 資訊」設定，預設開啟；關閉時 OBS 直接從動畫開始。
- 揭曉階段只顯示獎項、祝賀文字與最大獎提示，不再重複贊助者、金額、付款方式與留言。
- 調整無獎品圖片時的結果卡尺寸與標題呈現，避免同一獎項名稱重複出現並維持各螢幕尺寸可讀性。
- 將新設定納入 CQRS、事件投影與 YAML 快照保存。

## Capabilities

### New Capabilities

- `donate-notification-visibility`: Donate 活動可控制 OBS 是否顯示贊助通知。

### Modified Capabilities

- `donate-draw-reveal-sequence`: 調整 Donate OBS 分階段流程與結果揭曉資訊範圍。

## Impact

- 受影響專案：Domain 活動模型、Application 規則、Infrastructure 事件投影、Wasm 活動設定與 Donate OBS、API 事件來源測試。
- REST API 的活動 payload 將增加布林欄位；既有 YAML 未設定該欄位時維持顯示贊助通知的預設行為。
