## Why

Donate OBS 現在在收到結果後立即揭曉，沒有先呈現贊助互動，也無法讓抽獎動畫成為有節奏的直播事件。不同抽獎類型需要能清楚表達各自機制的完整演出，而不是只在結果旁播放裝飾動畫。

## What Changes

- Donate 活動新增可設定的抽獎動畫秒數，控制揭曉前的演出時間。
- Donate OBS 改為「贊助通知 → 動畫演出 → 獎項揭曉 → 結果顯示」的狀態流程。
- 實作一番賞撕開票券、扭蛋機與 Q 版角色轉把手、日式滾筒掉彩球、塞錢箱掉詩籤四種動畫。
- 依獎項資料決定日式滾筒的球色：最大獎為金色，最低機率或數量最多的獎項為白色，其餘依相對機率映射為藍、綠、黃、紅、銅或銀。

## Capabilities

### New Capabilities

- `donate-draw-reveal-sequence`: 管理 Donate OBS 的通知、抽獎演出與結果揭曉流程。
- `donate-lottery-animation-scenes`: 提供四種依抽獎類型呈現的可重播 CSS 動畫場景。

### Modified Capabilities

- 無。

## Impact

- 受影響專案：Domain、Application、Infrastructure、Blazor WebAssembly 與 API 測試。
- 受影響檔案類型：Donate 活動模型、CQRS 與事件投影、YAML 正規化、活動編輯對話框、OBS Razor 元件與測試檔。
- 不變更抽獎機率或獎項選擇邏輯；現有中獎結果為唯一揭曉依據。
