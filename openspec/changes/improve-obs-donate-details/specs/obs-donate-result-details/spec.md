## ADDED Requirements

### Requirement: Donate OBS 完整贊助明細
系統 SHALL 在 Donate OBS 顯示中獎或銘謝惠顧結果時，呈現贊助者名稱、格式化贊助金額、付款方式與非空白的使用者留言；付款方式與留言 MUST 使用可辨識的中文標籤。

#### Scenario: 有完整 Donate 明細的結果
- **WHEN** Donate 抽獎結果包含贊助者名稱、金額、付款方式與留言
- **THEN** OBS 結果畫面會同時顯示四項資訊，且付款方式與留言分別具有「付款方式」與「留言」標籤

#### Scenario: 未提供選填 Donate 明細
- **WHEN** Donate 抽獎結果的付款方式或留言為空白
- **THEN** OBS 結果畫面不會顯示對應的空白欄位，且仍會顯示贊助者名稱與金額

### Requirement: Donate OBS 長文字可讀性
系統 SHALL 讓 Donate OBS 的贊助者名稱、付款方式與留言在長文字、連續英數字與多行文字下換行顯示；重要 Donate 資訊 MUST NOT 因 CSS 裁切、固定行數或省略號而遺失。

#### Scenario: 顯示長留言
- **WHEN** Donate 抽獎結果的留言超過單行寬度或含有換行字元
- **THEN** 留言會保留所有文字並在可用寬度內換行顯示

#### Scenario: 窄畫布中的 Donate 明細
- **WHEN** Donate OBS 在窄於桌面標準的畫布寬度顯示
- **THEN** 贊助明細會維持可讀字級並在容器內重新排版，而不會水平溢出或裁切重要內容

### Requirement: Donate OBS 資訊優先的響應式版面
系統 SHALL 在有獎品圖片與抽獎動畫時，為 Donate 明細保留可見空間；圖片與動畫 MUST 依畫布大小縮放，且使用者啟用減少動態效果時，循環動畫 MUST 停止。

#### Scenario: 有獎品圖片的 Donate 結果
- **WHEN** Donate 抽獎結果包含獎品圖片與贊助留言
- **THEN** 畫面會顯示圖片、結果與完整贊助明細，且圖片不會以固定高度遮蔽明細

#### Scenario: 使用者偏好減少動態效果
- **WHEN** 瀏覽器回報 `prefers-reduced-motion: reduce`
- **THEN** Donate OBS 的重複抽獎與最大獎動畫會停止
