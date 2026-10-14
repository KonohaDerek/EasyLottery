# Design

## Context

Vue 3/Vuetify 與現有 REST API 已提供完整管理頁。此次是 Strangler Migration 中既有 Vue presentation 的體驗改善；沿用路由、API client、伺服器 secret redaction、ETag 與身份驗證，不碰 Blazor 後端或 OBS overlay contract。

YouTube Live Chat 從 `SystemSettings.YouTube.ApiKey` 取值；平台互動設定另存 `PlatformConnections`。兩者雖是相似名稱的 API Key，並非同一設定。`PaymentSettingsView` 與 `YoutubeLoginView` 都能編輯 `SystemSettings.YouTube`，因此保留專用 YouTube 頁作單一 UI 入口，支付頁改為連結，互動頁標明用途；API resource 仍照原樣載入/保存 masked secret。

## 決策

### 導覽與登出

依「開台常用 → 直播互動與模板 → 外觀與素材 → 系統與安全」分組。使用原生 `<details>/<summary>` 作可收合導覽，不引進元件或依賴；目前路由所在群組預設展開，RouterLink 保留 active state。桌面 drawer 與行動 menu 共用同一份分組資料。登出維持既有 token 清除流程，改用白色高對比文字、可見 focus ring 與描述性 accessible name。

### 創作者視覺

以 CSS variables 和 Vuetify theme 宣告一組高對比靛紫、莓紅、青綠色 tokens；背景使用低干擾淡紫灰與白色 surfaces。沿用既有動畫，不新增動態，不覆寫 `.obs-overlay` 的透明背景或 `prefers-reduced-motion` 行為。驗證主要首頁、導覽及設定頁樣式與互動。

### YouTube 設定引導

`/system/youtube-login` 是 `SystemSettings.YouTube.ApiKey` 與 Super Chat 的唯一編輯入口。Payment settings 不再顯示同一欄位/開關，而是明確連至該頁。`/system/platform-connections` 保留其不同的 `PlatformConnections` 欄位，標記為互動設定、說明目前抽獎 Live Chat 不讀取該值。首頁依既有 server status 顯示設定狀態和 CTA；無效 key 的 provider error 沿用現有 Live Chat 操作錯誤回報，不增加自動呼叫或 key 前端驗證。

### 市集圖片

以兩張原創動畫風格圖片展示 Roulette 與 PokeBox，縮為 768×768 WebP（各 <150 KB），圖片採 lazy-load、明確 alt text 與固定 aspect ratio。圖片失敗時以原有範本 glyph 與名稱 fallback。安裝操作與 API helper 不變。

### Donate 活動狀態

純前端 helper 接收 `DonateActivity` 與可選 `now`，按 UTC 日期導出「已排程／進行中／已結束／時間未完整」；另一欄持續顯示保存的「啟用／停用」。開始時間到達即進行中，結束時間到達即已結束；空結束時間表示不設截止日。非法或缺少必要時間以「時間未完整」表示。只讀顯示，不新增寫入或背景工作。

## 驗證與回復

Vitest 覆蓋狀態邊界；Playwright 覆蓋登出可見度、導覽 disclosure/active state、YouTube CTA/單一入口、市集 alt/fallback/安裝控制與核心頁視覺 token。於既有 Dev Container 執行前端 tests/typecheck/build 與 focused E2E。回復時 revert 此 Vue/CSS/素材分支；無資料或 API rollback。

## 可觀測性

使用目前頁面上的 status/alert 和既有 API 錯誤呈現；無新背景程序、logging、telemetry、API endpoint 或部署指標。CI/PR checks 是交付前驗證依據。
