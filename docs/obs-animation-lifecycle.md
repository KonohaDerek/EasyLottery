# OBS 動畫生命週期

所有抽獎 OBS 頁面都採用一致的生命週期，讓瀏覽器來源在沒有事件時維持透明，不會遮住直播畫面。

## 階段

1. `Idle`：沒有事件時，OBS 頁面不渲染可見內容，背景透明。
2. `Notification`：Donate 活動可先顯示贊助者、金額、留言與付款方式；可在活動設定關閉此階段。
3. `CountingDown`：需要倒數的活動先播放倒數提示。
4. `Animating`：播放活動設定的動畫。Donate 的動畫時間可在活動設定中調整；若活動啟用透明 WebM，會優先播放 WebM，載入失敗或 reduced-motion 時回退 CSS；戳戳樂與轉盤也有各自的播放節奏設定。
5. `ShowingResult`：顯示獎品圖片／名稱或「銘謝惠顧」。未中獎不會使用「恭喜」文案。
6. `Hold`：依設定保留結果一段時間，之後自動回到 `Idle`。

Donate 的新事件會依序排隊處理，不會因為上一個動畫尚未結束而遺失。管理頁目前統一提供「開啟測試 OBS」入口，會產生帶 `controls=1` 與 control token 的 URL。實況主可先在該頁測試動畫與直接點擊操作，再用 OBS 瀏覽器來源擷取需要的區域；不再另外維護功能重疊且容易失效的正式 URL 入口。

Donate 的動畫場景位於 `EasyLotteryWasm/Components/ObsAnimations`，主頁只負責生命週期與資料，不直接包含一番賞、扭蛋、滾筒、塞錢箱或刮刮樂 markup。新增場景時應新增獨立 component，再由 `DonateAnimationScene` 註冊，並保留 `--draw-duration` 與 reduced-motion fallback。

Donate 活動可在設定中啟用 WebM 動畫並從「OBS 資產庫」選擇 `video/webm`。影片只承載視覺特效，不應把贊助者或獎品文字烘進影片；結果資料仍由 HTML 疊加。非循環影片播放完畢會立即進入結果階段，循環影片則依活動動畫秒數結束。若影片無法載入，系統會繼續使用原本的 CSS 場景。

需要離線素材時，請從「OBS 資產庫」上傳圖片、`video/webm`、音效或模型，並使用資產頁提供的 UUID content URL。資產 metadata 位於 `obs-assets.yaml`，二進位檔位於 `obs-assets/`；系統會拒絕刪除仍被引用的資產，外部 URL 失效時 Donate 仍會顯示內建獎品名稱 fallback。

## 可及性與效能

- `motion=reduce`：停用動畫與轉場，保留內容與階段順序，適合對動態敏感的觀眾。
- `quality=low`：降低陰影與背景特效，適合低效能設備或直播編碼器。
- 瀏覽器也會自動遵循作業系統的 `prefers-reduced-motion` 設定。

例如：

```text
/obs/donate/{public-id}?motion=reduce&quality=low
/obs/roulette/{public-id}?controls=1
```

OBS 建議使用「瀏覽器」來源載入管理頁產生的測試 OBS URL，先完成測試後再擷取需要的區域。
