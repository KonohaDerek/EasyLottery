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

使用 `docker compose build` 產生 Web host image，再以獨立 volume 保存 `/data`。啟動後請依 [Passkey 與 OBS 權限](security.md) 設定 admin email、RP ID／Origin 與首次註冊來源限制，並將網站限制在可信任的 HTTPS 反向代理後方：

```bash
docker compose build api
docker compose up -d api
```

建立 `v*` release tag 後，GitHub Actions 會自動將 Docker image 發佈到 GitHub Container Registry（GHCR）。例如：

```bash
git tag v1.2.3
git push origin v1.2.3
```

Workflow 會發佈以下 image tags：

- `ghcr.io/konohaderek/easylottery:v1.2.3`
- `ghcr.io/konohaderek/easylottery:1.2.3`
- `ghcr.io/konohaderek/easylottery:1.2`
- `ghcr.io/konohaderek/easylottery:latest`（穩定版 tag；含 `-rc`、`-beta` 等 pre-release tag 不會更新 `latest`）
- `ghcr.io/konohaderek/easylottery:sha-<commit>`

GHCR image 使用 workflow 內建的 `GITHUB_TOKEN`，不需要額外建立 registry 密碼；首次發佈後可在 GitHub repository 的 Packages 設定調整可見性與存取權限。
