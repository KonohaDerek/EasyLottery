## Why

戳戳樂與轉盤編輯表單包含多欄設定、資產 URL 與格子內容，預設 Bootstrap Dialog 寬度不足，會造成欄位被壓縮與輸入困難。Donate、戳戳樂、轉盤及資產選擇器應採用一致的寬版 Dialog。

## What Changes

- 管理編輯 Dialog 在桌面視窗使用 75vw 寬度。
- 使用 Blazorise ExtraLarge 尺寸，並直接覆寫外層 `.modal-dialog` 的寬度。
- 小螢幕改用保留 1rem 邊距的可用寬度，保留垂直捲動。

## Scope

受影響頁面為 Donate 活動、戳戳樂、轉盤，以及共用 OBS 資產選擇器；不變更表單欄位或資料模型。
