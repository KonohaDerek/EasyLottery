## Context

Donate 活動表單同時顯示多個欄位與獎項表格；預設 Bootstrap Dialog 寬度無法提供足夠的橫向閱讀空間。

## Goals / Non-Goals

**Goals:** 在桌面視窗提供至少 75% 寬度的編輯區域，並讓小螢幕使用可用的全寬範圍。

**Non-Goals:** 不調整欄位內容、資料模型或其他頁面的 Dialog。

## Decisions

- 為此 Dialog 加上專屬 CSS class，覆寫 Bootstrap `.modal-dialog` 的寬度與最大寬度，避免影響其他 Dialog。
- 桌面使用 `75vw`；小於平板斷點時改為保留 1rem 視窗邊距的寬度，防止橫向溢出。

## Risks / Trade-offs

- [不同 Bootstrap 版本的預設 max-width] → 以專屬選擇器同時覆寫 `width` 與 `max-width`。
