# Design

## Frontend boundary

新增 `src/EasyLotteryWeb` 作為 Vite root。Vite 的 `base` 設為 `/vue/`，build output 寫入 `src/EasyLotteryWasm/wwwroot/vue`，讓現有 `EasyLotteryAPI` 透過 static web assets 同源提供 Vue chunk。這個 output 是建置產物，不提交到 git。

Vue Router 使用 history mode，公開 route 為 `/about`、`/privacy-policy`、`/login`。`PublicLayout` 只提供品牌、公開導覽與內容容器，不建立 admin store，也不讀取 settings。

## Backend coexistence

在 `MapFallbackToFile("index.html")` 前新增三個精確 fallback mappings，將上述 route 導向 `vue/index.html`；root 與其餘 route 維持 Blazor fallback。這使第一階段能直接驗證正式 URL，同時維持未遷移功能的既有入口。

## Passkey bridge

`passkey.ts` 使用原生 `fetch` 呼叫既有 options/verify endpoint，將 API 的 base64url challenge、user ID、credential IDs 轉成 WebAuthn 所需的 `Uint8Array`，並將 credential response 轉回 backend 既有 JSON shape。註冊 required 時沿用 Blazor 現有策略，先以 login flow 取得 error code，再 fallback 到 register flow。

登入成功只保存既有短效 token 到 `sessionStorage`，然後導向 `/`，由目前 Blazor home 繼續提供未遷移的 admin experience。此 bridge 在 Phase 2 以前不複製 admin state。

## Tooling

使用 Vue、Vue Router、Pinia、Vuetify、TypeScript、Vite、Vitest；本 phase 不加入未使用的 SignalR client，待 Phase 4 需要時再引入。CI 與 Docker 在 .NET build/publish 前執行 frontend install/build。

## Accessibility and privacy

所有 public view 保留單一 `h1`、label 與 visible focus style；錯誤使用 `role="alert"`。頁面不發送 analytics，也不在 console、storage 或 request log 寫入 email 以外的 Passkey credential material。
