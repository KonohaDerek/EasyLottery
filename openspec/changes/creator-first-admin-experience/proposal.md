# Proposal

## 背景

Issue #294–#299 彙整了 EasyLottery 管理介面對實況主不友善的部分：登出不易辨認、功能導覽平鋪、YouTube 設定入口混淆、Vue 畫面風格不一、市集範本缺少視覺預覽，以及 Donate 活動只呈現手動啟用旗標。核心 TA 是各類實況主，優先照顧 VTuber 與日常開台流程。

## 目標與範圍

- 提升共用 Vue 管理列的登出對比、焦點可見度與 accessible name。
- 依開台工作流程分組管理路由，保留所有現有路徑、權限與行為。
- 將系統 YouTube API Key 與 Super Chat 設定集中至明確頁面，說明另一個互動平台欄位的獨立儲存用途，首頁提供下一步與狀態。
- 建立供公開頁、管理 shell 與主要活動頁重用的視覺 tokens，遵守 WCAG AA、reduced-motion 與透明 OBS 畫面規則。
- 為市集輪盤與戳戳樂範本提供原創動畫風格預覽圖與 fallback。
- 根據既有 UTC 日期欄位推算活動期間狀態，與手動啟用旗標分開顯示，不寫回資料。

## 非目標

- 不修改 .NET、Domain、REST、SignalR、Payment、YouTube protocol、WebAuthn/Passkey 或資料格式。
- 不移動或刪除任何伺服器端憑證；不在瀏覽器記錄或回顯 secret。
- 不進行原生 OBS 驗證（由使用者依 #269 自行驗證），不改正式環境、不合併、不部署。

## 驗收

- 六項 issue 的驗收條件都有可重現測試或視覺檢查；活動日期邊界採確定 UTC 測試。
- 既有 API 與範本複製/安裝契約保持不變；所有修改都位於 Vue presentation、測試、素材與工作紀錄。
- 前端 Vitest、typecheck、production build、相關 Playwright 及 strict OpenSpec validation 通過。
- PR Ready for review，連結 #252、#294–#299；Project #1 狀態同步至 In review。

## 風險與回復

- 調整共用導覽或色彩可能影響其他 Vue 管理頁：以全站 build、導覽 Playwright 與 desktop/mobile 檢查降低風險；可單一 revert UI/素材變更。
- YouTube 有兩套伺服器端設定：只合併重複 UI，不碰資料或 API；對不同用途明確標註，不把 interaction store 的值當作 Live Chat key。
- 時區或邊界比較錯誤可能造成過期活動被誤認仍有效：只以 UTC instant 比較，明確測試開始和結束邊界，且不更改後端 `isEnabled`。

## 影響

Vue 全域主題與樣式、AdminLayout、LotteryHome、YouTube/Payment/Platform Settings、Marketplace、Donate 活動狀態 helper 與相關測試；新增兩個靜態 WebP 素材及 OpenSpec/agent 交付紀錄。沒有資料遷移或 API 變更。
