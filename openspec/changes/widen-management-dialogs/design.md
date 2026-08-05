## 設計

Blazorise 的 `ModalContent` 內層寬度無法突破 Bootstrap 外層 `.modal-dialog` 的預設 max-width，因此在 `<Modal>` 加上共用 class 與 `ModalSize.ExtraLarge`，再由全域 CSS 直接套用 `.modal-dialog`：桌面為 `75vw`，小於 768px 時為 `calc(100vw - 1rem)`。

資產選擇器沿用相同規則，避免在寬版表單中開啟第二個窄 Dialog。
