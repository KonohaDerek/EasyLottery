# E2E 回歸測試套件

## Why

抽獎模板、OBS 控制頁與 Donate 通知流程跨越 WASM、API 與 SignalR，單元測試無法捕捉頁面互動、共享狀態及動畫狀態回復問題。需要在 CI 中以瀏覽器驗證實況主實際操作的關鍵流程。

## Goals

- 覆蓋 Donate、戳戳樂與轉盤的正式 OBS／控制頁互動。
- 驗證滑鼠、鍵盤、測試通知、結果重設與共享狀態同步。
- 在 CI 失敗時保留 trace、截圖、影片與 console/page error 記錄。

## Scope

- Playwright E2E 測試及其共用測試 fixture。
- CI 測試產物上傳與 OpenSpec 驗證。

## Non-goals

- 不改變抽獎機率、動畫或儲存業務規則。
- 不以 E2E 取代既有 Domain/API 測試。

## Acceptance

- CI 可執行 Donate、戳戳樂、轉盤 E2E，並驗證 OBS read/control token。
- 測試失敗時 artifacts 內包含 trace、screenshot/video 與 console log。

