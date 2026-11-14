# UX spec #340: card packs, odds versions, public shop, draw history

Sources: design.md D3, D4, D10, D11, D12; specs/card-packs; copy-deck.md §7; research-ux.md R2–R4, F4. Based on wave2-base `f30ca9f`.
Markers: **[OWNER]** needs owner/product input. **[LEGAL]** = copy-deck [需法務確認]. Conventions (layout classes, fields, alerts, Hub-status banner, nav group 「卡牌商店」) are the same as ux-spec-339.md §0.

Nav additions (inside 「卡牌商店」, after 發布到 Hub): `卡包 /card-shop/packs`, `抽卡紀錄 /card-shop/draws`. Public: the existing `/shop` (`CardShopView.vue`, currently a placeholder) and the public nav link 「卡牌商店」. Everything is hidden or blocked while `Shop:Enabled=false`.

## Odds model (applies to every screen)

- Admins enter odds in **% with up to 2 decimals**, stored as **basis points** (1 bp = 0.01%). The sum must be exactly **10000 bp (100.00%)**. Odds are always displayed as % with 2 decimals (D12, R2).
- Every publish of a pack's odds creates `odds_version` = previous + 1 (starting at v1). Every draw records the version it used.
- Finite supply: when a rarity's remaining count reaches 0, its weight becomes 0 and the remaining rarities are renormalized proportionally: `p_i' = p_i / (1 − p_soldout)`. Rounding to bp uses largest-remainder so the sum stays 10000. The Hub publishes this automatically as a new `odds_version` before the next draw.
- D3 stores per-rarity finite quantities in `card_packs.rarity_supply_json`, separately from the optional overall pack limit. The Hub tracks each rarity's lifecycle quantity and drawn count independently of odds versions, so a republished odds version cannot reset supply.

## B1. 卡包列表 (`/card-shop/packs`)

- Layout: heading (`pack.list.title` 卡包, lead `pack.list.lead` 「每包抽出 1 張卡牌。卡包發布後，機率會公開在商店頁。」) → 「新增卡包」 → table: 封面 thumbnail, 名稱, 卡組, odds 版本 (`v{n}` or 「未發布」), 狀態 chip (草稿 / 販售中 success / 已售完 default / 有未發布變更 warning / 已停售), 已抽 / 上限.
- Empty: `pack.list.empty` 「還沒有卡包。卡包需要一個已發布的卡組。」. If no set is published, the primary action changes to 「前往發布卡組」.
- Loading and error follow the records-page pattern (`v-progress-circular`, error + 「重新載入」).

## B2. 卡包編輯 (`/card-shop/packs/:id`, `/card-shop/packs/new`)

Two columns on ≥md (fields left, **live preview** right, sticky). Stacked on mobile with the preview first.

Sections in order:
1. **基本資料**

| Field | zh-TW | Validation |
|---|---|---|
| title | `pack.field.title` 卡包名稱 | 1–40 chars |
| set | `pack.field.set` 卡組 | Required. Only sets that are **published to Hub** are listed. Locked after the first pack publish (changing it would invalidate odds history). |
| cover | `pack.field.cover` 卡包封面 | Required to publish. Same upload + 3:4 crop + rights checkbox as #339 A3 (`catalog.image.rightsConfirm` **[LEGAL]**). Upload only, no AI generation. **[OWNER]** Should AI generation be allowed for covers? |
| donateActivity | `pack.field.donate` Donate 活動 | Select from existing Donate 活動. Price = that activity's 最低贊助金額. Hint: 「價格與付款沿用 Donate 活動設定；抽卡觸發於 Donate 卡包活動上線後生效。」 **[OWNER]** Confirm the pack↔Donate binding lives here vs in #342. |

2. **抽卡機率** (`pack.odds.title` 抽卡機率). One row per rarity, in rarity sort order:
   - Columns: rarity chip (color) · 卡牌數 (published cards of this rarity in the set) · 機率 % input (`type="number" step="0.01" min="0" max="100" inputmode="decimal"`) · bp helper text (「= 2500 bp」) · 可抽數量 (only when 限量 is on).
   - A rarity with 0 published cards is disabled at 0, with hint `pack.odds.noCards` 「此卡組沒有這個稀有度的卡牌。」
   - Live sum bar under the table (`aria-live="polite"`):
     - OK: `pack.odds.sumOk` 「合計 100.00%」 (success)
     - Short: `pack.odds.sumShort` 「合計 {sum}%，還差 {diff}%」 (error)
     - Over: `pack.odds.sumOver` 「合計 {sum}%，超過 {diff}%」 (error)
   - Field errors: more than 2 decimals → 「最多到小數點後 2 位。」; <0 or >100 → 「請輸入 0–100。」. A weight >0 on a rarity with no cards → 「有機率的稀有度至少要有 1 張卡牌。」
   - Helper link 「每張卡的實際機率」 expands per-card odds = rarity % ÷ cards in that rarity (copy `shop.odds.footnote`).
3. **數量限制** (`pack.supply.title` 數量限制)
   - Radio 「不限量」 (default) / 「限量」.
   - 總包數上限 `pack.supply.total`: integer 1–100000, optional.
   - 可抽數量 per rarity (finite pool): integer ≥0 per row. The sum must be ≥ the total limit if both are set.
   - Explanation, always visible when 限量 is on: `pack.supply.explain` 「某個稀有度抽完後，系統會依其餘稀有度原本的比例重新計算機率，並自動發布新的機率版本；商店頁會在下一筆 Donate 前顯示更新後的機率。全部抽完後停止抽卡。」 Add a worked example from the current inputs: 「例：SSR 抽完後，N 70% → 73.68%、R 25% → 26.32%」.
   - Lowering a limit below the opened count is rejected: 「上限不能小於已抽數量 {opened}。」
4. **預覽** (right column): renders the exact public shop pack card component (B4) with draft values and a 「草稿預覽」 ribbon.
5. **機率版本紀錄** (only after the first publish): table of 版本, 發布時間, 原因 (手動發布 / {rarity} 抽完自動重算), 機率摘要. Read-only.

- Actions: primary 「發布卡包」 (B3). Secondary 「儲存草稿」, 「停售」 (confirm: 「停售後商店頁不再顯示此卡包，已抽出的卡牌不受影響。」). Unsaved-changes guard on leave.
- Save errors: 「卡包儲存失敗，請稍後再試。」

**Acceptance.**
- [ ] Typing 70 / 25 / 4.99 shows 「合計 99.99%，還差 0.01%」 and publish is disabled. Changing 4.99 to 5 enables it.
- [ ] 33.333 is rejected (3 decimals). 33.33 + 33.33 + 33.34 is accepted and stored as 3333/3333/3334 bp.
- [ ] A rarity with no cards in the set cannot get a weight >0. The API rejects it too (spec: invalid odds cannot be published).
- [ ] Only Hub-published sets appear in the set select.
- [ ] The preview updates live and is the same component as the public shop card.
- [ ] With 限量 on, the recalculation explanation and a worked example computed from the current inputs are visible.

## B3. 發布卡包 (confirm dialog from B2)

- The dialog shows: 卡包名稱, 將發布為 `pack.publish.version` 「機率版本 v{n}」, an odds table exactly as the public shop will render it, and a diff vs the live version (`v{n-1}`: 「SR 5.00% → 4.00%」). Supply settings are summarized.
- If odds changed on a live pack: `pack.publish.changeNotice` 「機率變更會立即套用到下一筆 Donate 抽卡，舊版本保留於紀錄。」
- Blocking checks (same as B2, plus Hub `linked`, cover present, set published) are listed with links. 「確認發布」 is disabled until they pass.
- Success: `v-alert success` 「已發布，機率版本 v{n}。」. The list chip becomes 販售中.
- Errors: Hub unreachable `publish.error.unreachable` (from #339). Hub rejects odds (e.g. a sold-out race): show the message and reload the current version.

**Acceptance.**
- [ ] The first publish shows v1. Changing only the title and republishing keeps the version. Changing any odds or supply value increments it.
- [ ] The odds shown in the dialog equal what `/shop` displays after publish (same formatter).
- [ ] A Hub failure leaves the live version unchanged and shows the error.

## B4. 公開卡牌商店 (`/shop`, public, no auth)

**Purpose.** Show packs, price and **odds before any donation** (D10, D12, R2; Taiwan rule: on the purchase page, as %).
**Layout (top to bottom).** Header (eyebrow CARD SHOP, h1 卡牌商店, lead `shop.lead` 「每次 Donate 達指定金額可抽 1 包，每包 1 張卡牌。」) → pack grid (1 column on mobile, 2–3 on desktop). Each pack card, in this order:
1. Cover (3:4, `alt` = pack name), badges: 限量 / 已售完.
2. Name, set name, 價格 `shop.price` 「單筆 Donate 滿 NT${amount}」.
3. **Odds table**, always expanded and never behind a click: title `shop.odds.title` 抽卡機率. Intro line `shop.odds.line` with each row as `shop.odds.item` (「{rarity} {percent}%」, 2 decimals). Columns 稀有度 · 機率 · (限量時) 剩餘. Footnote `shop.odds.footnote`. Meta line `shop.odds.version` 「機率版本 v{n}，更新於 {publishedAt}」.
4. If the version changed because a rarity sold out: `v-alert info` `shop.odds.recalculated` 「{rarity} 已抽完，機率已於 {publishedAt} 重新計算。」 **[LEGAL]** wording.
5. `shop.odds.preDonate` 「Donate 前請先確認抽卡機率。」 **[LEGAL]**
6. `shop.noCashValue` 「卡牌無現金價值、不得轉售換取金錢，僅供收藏與觀眾間交換。」 **[LEGAL]**
7. **#342 placeholder slot** `ShopDonateConsentSlot`: reserves the position for the age-bracket declaration and the 消保法 §19 withdrawal-exemption consent checkbox **[LEGAL]**. In #340 it renders nothing in production and a dashed box 「（#342：年齡聲明與鑑賞期同意）」 in dev builds. It must sit between the odds and the CTA.
8. CTA 「前往 Donate」 (`shop.cta.donate`), which opens the bound Donate page. **[OWNER]** Hide the CTA until #342 ships (recommended: show price and odds only, because without #342 a donation would not draw). Sold out: the CTA is disabled with 「已售完」.

- Empty: `shop.empty` 「目前沒有販售中的卡包。」 (reuse the current placeholder copy for the not-ready state).
- Loading: skeleton cards. Error: 「商店載入失敗，請稍後再試。」 + 「重新載入」.
- Hub unreachable: show the last cached published odds with 「機率資料可能不是最新，暫停 Donate 抽卡。」 and disable the CTA. Never show stale odds as current without that notice. **[OWNER]** Confirm we cache or fail closed.
- Accessibility: the odds table is a real `<table>` with `<caption>`. Rarity is shown by text, not color alone.

**Acceptance.**
- [ ] For every published pack, the odds table (all rarities with weight >0, 2-decimal %) renders above the CTA without any interaction, at 360 px width too.
- [ ] Displayed percentages sum to 100.00%.
- [ ] After a rarity sells out, a reload shows the new version number, the recalculated %, and the recalculated notice, before the CTA.
- [ ] The no-cash-value line is present on every pack card.
- [ ] With `Shop:Enabled=false`, `/shop` is blocked and the public nav link is hidden (existing behavior kept).
- [ ] Sold-out and stopped packs show no active CTA. Stopped packs are not listed.

## B5. 抽卡紀錄 (`/card-shop/draws`)

**Purpose.** Audit trail of pack draws (dispute checks, D12).
**Layout.** Reuse `DonateDrawRecordsView` structure: hero with total count → filter panel → table → pagination.
- Retention note under the hero, always visible: `draws.retention` 「抽卡紀錄會在活動結束後至少保存 180 天，供機率與爭議查核；此期間不能刪除。」 **[LEGAL]** (This is separate from the 180-day email retention.)
- Filters: 關鍵字 (serial or Donate 付款編號, max 120), 卡包 (select), 稀有度, 機率版本, 日期起 / 日期迄, 狀態 (全部 / 已抽出 / 排隊中 / 失敗). Actions 「套用篩選」 / 「清除篩選」.
- Columns: 時間 · 卡包 · 機率版本 · 稀有度 chip · 卡牌（名稱＋序號） · Donate 付款編號 · 狀態. **No viewer email** in #340. **[LEGAL]** Should a masked email appear once #342 lands?
- Export: 「匯出 CSV」 exports **all rows matching the current filter** (not only the current page). UTF-8 with BOM, filename `card-draws-{yyyyMMddTHHmmssZ}.csv` (existing pattern). Columns match the table plus `odds_version`, `rng_version`, `draw_id`. Each export writes an audit event. More than 50,000 rows → 「資料過多，請縮小日期範圍。」
- Empty: 「沒有符合條件的抽卡紀錄。」 + 「清除篩選」 when filters are active. Loading and error follow the records-page pattern.

**Acceptance.**
- [ ] Filtering by pack + odds version returns only draws recorded with that version.
- [ ] The CSV row count equals the filtered total, and the file opens in Excel with correct zh-TW text.
- [ ] Every export creates an audit event (who, when, filter).
- [ ] The retention note is visible without scrolling on desktop.
- [ ] No email address appears in the table or the CSV.
- [ ] There is no delete action for draw records in the UI or the API.
