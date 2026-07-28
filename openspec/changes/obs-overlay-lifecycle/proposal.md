## Why

Donate OBS 會在每次載入時重播已保存的最後一筆抽獎紀錄，讓尚未觸發新 Donate 的直播畫面出現過期結果。同時，OBS 待機或載入狀態仍輸出文字與系統背景，會遮擋 OBS 底下的直播場景。

## What Changes

- Donate 活動新增「結果顯示秒數」設定，控制新抽獎結果顯示多久後自動回到透明待機狀態。
- Donate OBS 僅顯示頁面工作階段開始後收到的新結果；既有與測試歷史紀錄不會在重新載入時重播。
- 所有 OBS 頁面的背景強制透明，並在載入、找不到設定或沒有可展示結果時不輸出待機文字或面板。
- 為設定值驗證、事件投影與 YAML 相容性補上測試，避免重播規則或設定值在保存後遺失。

## Capabilities

### New Capabilities

- `obs-overlay-lifecycle`: 控制 OBS 覆蓋層的透明待機狀態與 Donate 結果的顯示生命週期。

### Modified Capabilities

- 無。

## Impact

- 受影響專案：Domain、Application、Infrastructure、Blazor WebAssembly 與 API 測試。
- 受影響檔案類型：Donate 活動設定模型、CQRS 儲存與事件投影、YAML 正規化、OBS 版面與 Razor 元件、測試檔。
- 不刪除任何歷史抽獎紀錄；設定預設值可安全套用至既有活動。
