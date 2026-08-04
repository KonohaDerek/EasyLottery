# 發布策略

EasyLottery 目前是 ASP.NET Core + Blazor WebAssembly 的 Web 應用，正式部署方式是 Docker／容器，不是 Tauri 桌面應用。Repository 目前沒有 Tauri project、Rust workspace 或 `src-tauri/tauri.conf.json`，因此不會保留桌面封裝 workflow，避免建立 tag 後產生必然失敗的 release。

## 版本驗證

每個 Pull Request 與 `main` 的變更都由 CI 執行：

- .NET SDK 10 restore、Release build
- Domain 與 API tests
- OpenSpec validation
- Playwright browser tests

建立版本標籤前，請先確認 CI 通過並在本機執行：

```bash
dotnet test EasyLottery.generated.sln --no-restore
dotnet build src/EasyLotteryAPI/EasyLotteryApi.csproj --configuration Release --no-restore
```

## Docker 發布

使用 `docker compose build` 產生 Web host image，再以獨立 volume 保存 `App_Data`。啟動後請依 [Session 與 OBS 權限](security.md) 將網站限制在可信任的 localhost、內網或 Tunnel：

```bash
docker compose build api
docker compose up -d api
```

目前沒有自動推送 registry 或建立桌面安裝包的流程。若未來需要 Tauri、原生桌面封裝或 registry release，請另開 issue，先加入可建置且可驗證的 project，再新增對應 workflow。
