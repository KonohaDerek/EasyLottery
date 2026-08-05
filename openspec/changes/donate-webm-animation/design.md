## 設計

Donate 活動新增 `UseWebmAnimation`、`WebmAnimationUrl`、`WebmPosterUrl` 與 `WebmAnimationLoop`。`Animation` 仍是 CSS fallback 的動畫類型，舊 YAML 缺少新欄位時使用空字串與停用狀態。

`DonateAnimationScene` 同時保留 CSS fallback 與 WebM `<video>` layer。影片先以透明度 0 預載，`canplay` 後切換到影片；`error` 時移除影片並顯示 CSS fallback。`motion=reduce` 或瀏覽器偏好 reduced motion 時不啟用 WebM。

非循環影片的 `ended` 事件呼叫 `DonateObsRevealLifecycle.CompleteAnimation`，讓結果不必等待下一次輪詢；循環影片與影片未載入時仍依活動動畫秒數推進，以避免影片載入失敗卡住生命週期。

OBS 資產庫新增 `ObsAssetKind.Video`，只接受 `video/webm`，內容 endpoint 使用既有 range processing，適合瀏覽器來源串流與 poster 預覽。
