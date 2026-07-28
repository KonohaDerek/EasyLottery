## Why

目前 Donate OBS 的中獎結果只以分散的文字列呈現贊助者、金額、付款方式與留言；付款方式的字體太小、留言容易落在視窗可視範圍外，長內容也沒有明確的排版規則。直播畫面需要在不同 OBS 尺寸下即時清楚呈現完整贊助資訊，讓觀眾與實況主都能辨識這次互動的來源與內容。

## What Changes

- 將 Donate OBS 的贊助資訊改為結構化資訊卡，清楚呈現贊助者、金額、付款方式與留言。
- 對長名稱、付款方式與多行留言採用可換行的排版，避免使用截斷或隱藏內容。
- 為獎品圖片、抽獎動畫與贊助資訊定義可在常見 16:9 OBS 畫布與窄視窗共存的響應式空間配置。
- 透過建置、既有測試套件與手動驗證步驟，確認 Donate 中獎記錄包含的留言與付款方式能正確渲染。

## Capabilities

### New Capabilities

- `obs-donate-result-details`: 在 Donate OBS 結果覆蓋層完整且可讀地顯示贊助明細與長留言。

### Modified Capabilities

- 無。

## Impact

- 受影響專案：Blazor WebAssembly 前端與其元件測試。
- 受影響檔案類型：Donate OBS Razor 元件、元件樣式與測試檔。
- 不調整 Donate 抽獎規則、REST API 或 YAML 資料格式；既有的 `DonationMessage` 與 `PaymentMethod` 欄位直接用於畫面呈現。
