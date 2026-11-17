# UX spec #339: card catalog, images, AI, publish to Hub (streamer admin)

Sources: design.md D3, D6, D10, D11; specs/card-catalog; copy-deck.md; research-ux.md. Based on wave2-base `f30ca9f`.
Markers: **[OWNER]** needs owner/product input. **[LEGAL]** = copy-deck [需法務確認].

## 0. Conventions (reuse what exists, no new design system)

- Shell: `AdminLayout.vue`. Page = `section.settings-page` > `header.page-heading` (`p.eyebrow` + `h1` + `p.lead`) > `v-card.settings-card` blocks with a `v-card-title` and `v-card-text.form-grid`. Copy `CardHubLinkView.vue`.
- Fields: `label.field-label` + `input.settings-input` + `p.field-hint`. Status: `v-chip variant="tonal" size="small"`. Errors: `v-alert type="error" variant="tonal" role="alert"`. Loading: `p.status-text role="status"` or `v-progress-circular`. Load failure: `.login-error` + 「重新載入」. Confirm: `v-dialog`. Primary `v-btn color="primary"`, secondary `variant="outlined"`.
- Secrets follow the Hub link pattern: `type="password"`, `autocomplete="new-password"`, blank means "keep the server value", and the value is never echoed back.
- Strings go into `src/shop/copy.ts` under the new key namespaces below. All of these strings are new: the copy deck has no catalog keys yet.
- Navigation: extend the `getAdminNavigationGroups(shopEnabled)` group 「卡牌商店」. It stays hidden while `Shop:Enabled=false`. Order:
  `Card Hub 連結 /system/card-hub` (exists) → `稀有度 /card-shop/rarities` → `卡組與卡牌 /card-shop/sets` → `AI 生圖設定 /card-shop/ai-settings` → `發布到 Hub /card-shop/publish`. Detail routes (`/card-shop/sets/:id`, `/card-shop/cards/:id`) highlight their parent through the existing prefix matching.
- Global banner on every catalog page: if the Hub status is not `linked`, show `v-alert type="warning"` with `hub.link.status.*` desc and a link to `/system/card-hub`. Editing stays allowed; only publishing is blocked.

## A1. 稀有度 (`/card-shop/rarities`)

**Purpose.** Define the rarity tiers that cards and pack odds use.
**Layout.** Heading (`catalog.rarity.title` 稀有度, lead `catalog.rarity.lead` 「稀有度決定卡牌外框顏色與卡包機率分組。已發布卡牌使用中的稀有度不能刪除。」) → one card holding an ordered list (one row per tier) → 「新增稀有度」 → save bar.
**Seed.** The first visit with no rows seeds N 灰 #9E9E9E, R 藍 #2196F3, SR 紫 #9C27B0, SSR 金 #FFB300, UR 彩/紅 #E53935. The streamer can edit all of them.

| Field | Label key / zh-TW | Validation |
|---|---|---|
| code | `catalog.rarity.code` 代碼 | 1–8 chars `[A-Z0-9]`, unique (case-insensitive). Read-only once a published card uses it. |
| name | `catalog.rarity.name` 顯示名稱 | 1–20 chars, unique |
| color | `catalog.rarity.color` 顏色 | hex `#RRGGBB` via `input type="color"` + text. Show a contrast warning (not a block) when the contrast vs white is under 3:1. |
| sort | `catalog.rarity.sort` 排序 | Up/down buttons (keyboard accessible), no drag-only. Low to high rarity. Controls odds-table order everywhere. |

- Limits: 1–10 tiers. Delete is disabled with tooltip `catalog.rarity.inUse` 「已有卡牌使用此稀有度，無法刪除。」 when any card (draft or published) references the tier.
- Renaming or recoloring a tier used by published cards shows a hint: `catalog.rarity.republishHint` 「名稱與顏色變更需重新發布到 Hub 才會更新。」 **[OWNER]** Should rename be allowed after publish? The Hub treats a card's *rarity assignment* as immutable, not the label.
- States: loading / load error + retry / saved notice 「稀有度已儲存。」 / save error 「稀有度儲存失敗，請稍後再試。」
- Actions: primary 「儲存」. Secondary 「新增稀有度」, 「還原預設」 (only when no tier is in use).

**Acceptance.**
- [ ] The first visit with an empty table shows the 5 default tiers in N<R<SR<SSR<UR order.
- [ ] Saving two tiers with the same code or name fails with an inline error on the duplicate row.
- [ ] The delete control is disabled for a tier referenced by any card. The API also rejects such a delete.
- [ ] Reordering changes the order in the card editor's rarity select and in the pack odds table (#340).
- [ ] The code field is read-only for a tier used by a published card.

## A2. 卡組與卡牌 (`/card-shop/sets`, `/card-shop/sets/:id`)

**Purpose.** Group cards into sets. Each card gets a serial.
**List layout.** Heading (`catalog.set.title` 卡組與卡牌) → 「新增卡組」 → table: 名稱, 代碼, 卡牌數 (草稿/已發布), 狀態 chip (`草稿` default / `已發布` success / `有未發布變更` warning), 最後發布時間.
**Empty state.** `catalog.set.empty` 「還沒有卡組。先建立卡組，再新增卡牌。」 + primary 「新增卡組」.

Set fields:

| Field | zh-TW | Validation |
|---|---|---|
| name | `catalog.set.name` 卡組名稱 | 1–40 chars |
| code | `catalog.set.code` 卡組代碼 | 2–10 chars `[A-Z0-9]`, unique per site, uppercase on input. **Immutable after the first publish.** Hint: 「用於序號，例如 STAR → STAR-001。發布後無法修改。」 |
| description | `catalog.set.description` 說明 | 0–200 chars |
| tradable | `catalog.set.tradable` 允許觀眾交換 | Checkbox, default on (D9). Hint: 交換功能於 App 上線後生效。 |

**Set detail layout.** Set fields card → card grid (3:4 thumbnails, rarity-colored border, serial, name, draft/published chip) with filters 稀有度 / 狀態 → 「新增卡牌」.

Card editor (`/card-shop/cards/:id`, or `new?set=`), two columns on ≥md and stacked on mobile. Left: fields. Right: live card preview (3:4, rarity frame color, name, serial).

| Field | zh-TW | Validation |
|---|---|---|
| serial | `catalog.card.serial` 序號 | Read-only, auto `{SETCODE}-{NNN}` (zero-pad 3, grows to 4+ past 999). Assigned on first save and **never reused** after delete. The hint shows the Hub form `{tenant-slug}-{SETCODE}-{NNN}`. |
| name | `catalog.card.name` 卡牌名稱 | 1–30 chars |
| description | `catalog.card.description` 卡牌說明 | 0–200 chars |
| rarity | `catalog.card.rarity` 稀有度 | Required; select in sort order. **Read-only after publish** (lock icon + `catalog.card.lockedHint` 「已發布卡牌的序號與稀有度無法修改。」) |
| image | `catalog.card.image` 卡面圖片 | Required to publish (a draft may have none). Tabs 「上傳」/「AI 生成」, see A3/A4. |

- Delete: a draft can be deleted after a confirm: 「刪除後序號 {serial} 不會再使用。」. A published card cannot be deleted. The action is 「停止發行」 (excluded from future packs; holders keep their instances). **[OWNER]** Confirm that 停止發行 is the only removal path for published cards.
- States: loading skeleton for the grid; save error 「卡牌儲存失敗，請稍後再試。」; leaving with unsaved changes asks for confirmation.

**Acceptance.**
- [ ] Adding three cards to set `S1` gives `S1-001..003`. Deleting `S1-002` and adding one more gives `S1-004`.
- [ ] The set code input is disabled after the set's first successful publish. The API rejects changes.
- [ ] The rarity select is disabled on a published card. A PUT that changes rarity or serial returns a validation error.
- [ ] A published card shows 「停止發行」 and no 「刪除」.
- [ ] The preview frame color updates live when the rarity changes.

## A3. Image upload + 3:4 crop (card editor, 「上傳」 tab)

- Picker: `input type="file" accept="image/png,image/jpeg,image/webp"`, max 10 MB (reuses OBS asset storage, D6). Errors: `catalog.image.tooLarge` 「圖片超過 10 MB，請壓縮後再上傳。」, `catalog.image.badType` 「只支援 PNG、JPEG、WebP。」
- Crop dialog (`v-dialog`): fixed 3:4 frame, drag to pan, zoom slider, keyboard arrows to pan. A 「預覽」 toggle shows the frame with the rarity border. If the source is under 600×800 px, warn `catalog.image.lowRes` 「圖片解析度偏低，卡面可能模糊。」 (warning only).
- **Rights confirmation (required):** checkbox `catalog.image.rightsConfirm` 「我確認擁有此圖片的著作權或已取得授權，可用於卡牌並供觀眾收藏與交換。」 **[LEGAL]**. 「上傳」 stays disabled until it is checked. The confirmation is stored with the asset (who and when).
- Actions: primary 「確認裁切並上傳」 (busy text 「上傳中…」), secondary 「取消」.

**Acceptance.**
- [ ] An 11 MB file or a GIF is rejected client-side with the message above and server-side with a typed error.
- [ ] Upload cannot be submitted while the rights checkbox is unchecked. The stored asset records the confirmation timestamp.
- [ ] The saved image is exactly 3:4 and a thumbnail exists.

## A4. AI generation panel (card editor, 「AI 生成」 tab)

**Not configured:** the tab shows `catalog.ai.notConfigured` 「尚未設定 AI 生圖供應商，目前只能上傳圖片。」 + link 「前往 AI 生圖設定」. Upload still works (spec scenario).
**Layout (top to bottom).**
1. Provider line: 「{provider} · {model}」 (read-only) + usage meter `catalog.ai.usage` 「今日 {dayUsed}/{dayCap} 張 · 本月 {monthUsed}/{monthCap} 張」 as a `v-progress-linear`, turning warning at ≥80% and error at 100%.
2. Prompt `catalog.ai.prompt` 提示詞: textarea, 1–1000 chars, with a counter.
3. Text-assistant buttons (shown only if Claude text assistant is configured): 「改寫提示詞」 (`catalog.ai.rewrite`) shows the rewrite in a diff box with 「套用」/「保留原文」. 「建議名稱與說明」 (`catalog.ai.suggest`) shows 3 suggestions, and clicking one fills name and description (still editable). Claude never generates images. The UI labels these 「文字助理（Claude）」.
4. Count 「產生張數」 1–4 (default 1). Each image counts toward the caps. The button shows the remaining quota.
5. Primary 「產生圖片」 (busy 「產生中…」, typical wait text 「通常需要 10–60 秒」) → candidate gallery → 「使用這張」 opens the A3 crop dialog with the same rights checkbox, using AI wording `catalog.ai.rightsConfirm` 「我確認此 AI 生成圖片可依供應商條款用於卡牌。」 **[LEGAL]**
- Errors (typed, `v-alert`):
  - content policy: `catalog.ai.refused` 「供應商拒絕了這個提示詞（內容政策）。請修改描述後再試，本次不會建立卡面。」
  - cap: `catalog.ai.capReached` 「已達今日（或本月）生成上限，{resetAt} 後可再使用。」 The button is disabled.
  - invalid key: `catalog.ai.keyInvalid` 「API 金鑰無效或已過期，請到 AI 生圖設定更新。」
  - provider down or timeout: `catalog.ai.unavailable` 「AI 供應商暫時無法使用，請稍後再試。」
- The card stores `source=ai`, `ai_provider`, `ai_prompt` (D3). The prompt is shown read-only on the card for audit.

**Acceptance.**
- [ ] With no provider configured, only upload is offered and the AI tab explains why.
- [ ] A provider content-policy refusal shows `catalog.ai.refused` and creates no asset or card image.
- [ ] At cap, the generate button is disabled, the reset time is shown, and the API also rejects the request.
- [ ] The usage meter updates after each generation without a page reload.
- [ ] The rewrite and suggest buttons are hidden when the text assistant is not configured. They never change fields until the streamer clicks 「套用」 or a suggestion.

## A5. AI 生圖設定 (`/card-shop/ai-settings`)

**Purpose.** Per-streamer provider, model, key and caps.
**Layout.** Heading (`ai.settings.title` AI 生圖設定, lead 「金鑰加密保存在此網站，儲存後不會再顯示。生成費用由你的供應商帳戶支付。」) → card 「圖片生成」 → card 「文字助理（Claude）」 → save bar.

| Field | zh-TW | Validation |
|---|---|---|
| provider | `ai.settings.provider` 供應商 | Select: OpenAI Images / Google Gemini（Imagen）/ Stability AI / FLUX / OpenAI 相容端點 / 不使用 |
| baseUrl | `ai.settings.baseUrl` 端點網址 | Only for OpenAI 相容. Must be `https://`, max 300 chars. Public endpoints are allowed by default. Private/LAN endpoints require the site admin to explicitly allowlist the exact HTTPS origin and private host/port. The server resolves and validates on every request, permits only the allowlisted RFC1918/ULA address, pins the connection to that address, and preserves Host/SNI. Loopback, link-local, unspecified, multicast, reserved, and cloud-metadata destinations are always denied. Redirects are disabled; an endpoint cannot redirect to another host. |
| apiKey | `ai.settings.apiKey` API 金鑰 | Write-only password field. Placeholder when saved: 「已設定，留白則保留原金鑰」. Max 500 chars. 「移除金鑰」 needs a confirm. |
| model | `ai.settings.model` 模型 | Select filled by 「重新取得模型清單」 (needs a saved key). For OpenAI 相容, a free-text fallback is allowed. Never hard-coded. |
| dailyCap | `ai.settings.dailyCap` 每日上限（張） | Integer 1–500, default 20 |
| monthlyCap | `ai.settings.monthlyCap` 每月上限（張） | Integer ≥ dailyCap, ≤ 10000, default 300 |
| text assistant | `ai.settings.claudeKey` / `ai.settings.claudeModel` | Same write-only key and fetched model list. Optional. |

- Status chip per card: 未設定 / 已設定 / 金鑰無效 / 無法連線, mirroring the four Hub link states so that a network failure is never shown as an invalid key.
- 「測試連線」: validates the key and lists models without generating an image (no quota used). Toasts: 「連線成功，可用模型 {n} 個。」/「金鑰無效。」/「無法連線到供應商，請稍後再試。」
- Show `{timezone}` 計算 next to the caps. **[OWNER]** Cap window timezone: Asia/Taipei proposed.

**Acceptance.**
- [ ] No API response or page source ever contains the key. GET returns only `hasApiKey: true/false`.
- [ ] Saving with a blank key keeps the existing key. 「移除金鑰」 clears it and A4 flips to the not-configured state.
- [ ] The model list comes from the provider call. A fetch failure shows an error and keeps the previous selection.
- [ ] monthlyCap < dailyCap is rejected inline.
- [ ] 「測試連線」 does not increment usage.

## A6. 發布到 Hub (`/card-shop/publish`)

**Purpose.** Review and push the catalog snapshot (rarities, sets, cards) to Card Hub.
**Layout.**
1. Heading `publish.title` 發布到 Hub, lead 「發布後觀眾才能在 Card Hub 與 App 看到卡牌。序號、稀有度與卡組代碼發布後無法修改。」
2. Hub status chip (reuse `hub.link.status.*`). If not `linked`, the primary action is disabled and a link to Card Hub 連結 is shown.
3. 「變更內容」 card: summary chips 新增 {a} · 變更 {c} · 停止發行 {r} · 未變更 {u}, then grouped expandable lists per set. Each changed card shows field-level before→after (name, description, image thumbnail old/new, rarity label). Items that **become locked by this publish** get a lock badge `publish.willLock` 「發布後鎖定」.
4. Blocking validation list (shown above the button, each item links to the offending card): card without image, set without cards, Hub link not ready. Publish is disabled while any item is listed.
5. 「發布紀錄」 table: 時間, 版本 (Hub catalog version), 結果 chip (成功 / 失敗 / 進行中), 摘要 (+a ~c −r), 錯誤訊息. Last 50 entries, paginated like `DonateDrawRecordsView`.
- Confirm dialog: lists the locked fields ({n} 張卡牌的序號與稀有度將被鎖定) + required checkbox `publish.confirmLock` 「我了解發布後序號、稀有度與卡組代碼無法修改。」 → 「確認發布」.
- Empty: 「目前沒有需要發布的變更。」 with the primary action disabled.
- Errors: Hub unreachable `publish.error.unreachable` 「無法連線到 Card Hub，本次未發布，請稍後再試。」 (no partial publish: the snapshot is atomic). Hub rejection (immutable field changed, tenant disabled) shows the Hub's message per item. Tenant disabled: 「你的 Card Hub 帳號目前停用，無法發布。」
- Success: `v-alert type="success"` 「已發布版本 {version}。」. `hub_link.last_publish_at` updates on the Hub link page.

**Acceptance.**
- [ ] With no changes since the last publish, the diff is empty and publish is disabled.
- [ ] Editing a published card's name shows a "變更" row with before/after. The rarity is never in the diff as editable.
- [ ] Publish cannot start until the lock checkbox is checked.
- [ ] A Hub-unreachable failure writes a 「失敗」 log row, the Hub stays at the previous version, and the status chip shows 無法連線 (not 憑證失效).
- [ ] A card without an image blocks publish and its validation item links to that card.
- [ ] After success, set codes, serials and rarities of the published items are read-only in A1/A2.
