## Why

現有 Donate 抽獎動畫缺少能直接呈現「逐步揭曉」感的刮刮樂形式。新增刮刮樂可讓觀眾在動畫期間看到被銀漆覆蓋的結果，並在完成後獲得清楚的最終揭曉。

## What Changes

- 新增 Donate 活動的「刮刮樂」抽獎動畫選項。
- OBS 動畫階段顯示覆有銀漆的刮刮樂卡，自動刮除並露出實際獎項或銘謝惠顧文字。
- 動畫結束後繼續使用既有結果揭曉頁，顯示獎品名稱或銘謝惠顧。

## Capabilities

### New Capabilities

- `donate-scratch-card-animation`: Donate OBS 的刮刮樂抽獎場景與揭曉流程。

### Modified Capabilities

- `donate-lottery-animation-scenes`: 新增可供活動選擇的第五種 Donate 動畫場景。

## Impact

- 受影響專案：Domain 動畫列舉、Wasm 活動設定與 Donate OBS 動畫 UI。
