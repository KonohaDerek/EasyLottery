# OBS 動畫生命週期

所有抽獎 OBS 頁面都採用一致的生命週期，讓瀏覽器來源在沒有事件時維持透明，不會遮住直播畫面。

## 階段

1. `Idle`：沒有事件時，正式 OBS URL 不渲染可見內容，背景透明。
2. `Notification`：Donate 活動可先顯示贊助者、金額、留言與付款方式；可在活動設定關閉此階段。
3. `CountingDown`：需要倒數的活動先播放倒數提示。
4. `Animating`：播放活動設定的動畫。Donate 的動畫時間可在活動設定中調整；戳戳樂與轉盤也有各自的播放節奏設定。
5. `ShowingResult`：顯示獎品圖片／名稱或「銘謝惠顧」。未中獎不會使用「恭喜」文案。
6. `Hold`：依設定保留結果一段時間，之後自動回到 `Idle`。

Donate 的新事件會依序排隊處理，不會因為上一個動畫尚未結束而遺失。正式 OBS 頁面不提供操作按鈕；若要手動預覽或測試，請在網址加上 `controls=1`。

## 可及性與效能

- `motion=reduce`：停用動畫與轉場，保留內容與階段順序，適合對動態敏感的觀眾。
- `quality=low`：降低陰影與背景特效，適合低效能設備或直播編碼器。
- 瀏覽器也會自動遵循作業系統的 `prefers-reduced-motion` 設定。

例如：

```text
/obs/donate/{public-id}?motion=reduce&quality=low
/obs/roulette/{public-id}?controls=1
```

OBS 建議使用「瀏覽器」來源載入正式 URL；測試或調整動畫時才使用 `controls=1` 預覽控制介面。
