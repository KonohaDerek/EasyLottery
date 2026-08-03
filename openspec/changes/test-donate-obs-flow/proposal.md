## Why

Donate OBS 近來新增了通知開關、結果生命週期與五種動畫，但流程狀態仍直接寫在 Razor 頁面，缺少可重複執行的自動驗證。這讓視覺或輪詢調整容易造成待機、動畫順序或清除時機回歸。

## What Changes

- 將 Donate OBS 的結果生命週期抽成可測的純狀態流程。
- 將動畫類型到 OBS 呈現 class／key 的對照抽成可測目錄。
- 新增測試涵蓋透明待機、通知開關、階段時長、五種動畫、銘謝惠顧與結果自動清除。

## Capabilities

### New Capabilities

- `donate-obs-flow-verification`: Donate OBS 結果流程與動畫選擇的自動驗證。

### Modified Capabilities

- 無。

## Impact

- 受影響專案：Domain 的 Donate OBS 狀態流程、Wasm Donate OBS 頁面及 API／Domain 測試專案。
