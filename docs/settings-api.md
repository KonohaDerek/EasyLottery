# 設定資源 API

設定頁現在以資源為邊界讀寫，避免修改付款設定時覆蓋 OBS、音效或加班台資料。管理頁使用 admin session token；OBS overlay 可讀取非敏感的版面、音效、視覺與加班台資源。`PUT` 可帶 `If-Match` 進行樂觀鎖定；版本不一致會回傳 `409 Conflict`，回應會附上新的 `ETag`。

| 資源 | 讀取 | 更新 |
| --- | --- | --- |
| OBS 版面 | `GET /api/settings/obs-layout` | `PUT /api/settings/obs-layout` |
| 音效 | `GET /api/settings/sound-cues` | `PUT /api/settings/sound-cues` |
| 視覺樣式 | `GET /api/settings/visual-style` | `PUT /api/settings/visual-style` |
| 付款、Notify、SMTP、YouTube | `GET /api/settings/payments` | `PUT /api/settings/payments` |
| 加班台 | `GET /api/settings/overtime` | `PUT /api/settings/overtime` |

每個 `PUT` 只更新 `settings.yaml` 內對應的區段；付款資源中的金鑰、SMTP 密碼與 YouTube API Key 會遮罩回傳，送回遮罩值會保留原密鑰。

## 相容端點

`GET/PUT /settings` 與 `/easy-lottery-config.yaml` 保留作為舊版匯入、匯出及 OBS 相容投影使用，回應會帶 `Deprecation: true` 與 successor `Link`。新管理頁不應再把整份 YAML 作為主要寫入路徑。

## 範例

```http
GET /api/settings/visual-style
X-EasyLottery-Session-Token: <admin-token>
```

```http
PUT /api/settings/visual-style
If-Match: "<etag>"
X-EasyLottery-Session-Token: <admin-token>
Content-Type: application/json

{"activeThemeKey":"dashboard-console","backgroundImageUrl":"","bannerImageUrl":""}
```
