# Streamer Card Shop — UX Research (desk research)

Date: 2026-11-14 · Role: user researcher · Scope: design.md + specs/ of `streamer-card-shop` (read-only)
Method: web desk research (standard search, a few page fetches). There are no primary interviews yet. Evidence strength is marked as **[Official]** (a regulator, platform policy or court), **[Press]** (news coverage), **[Community]** (reviews, forums, fan guides) or **[Inference]** (my reasoning, no direct source). Several targeted searches found nothing. Those gaps are listed at the end. Nothing in this report is legal advice. Items marked *legal review* need counsel.

---

## 1. Key findings

### F1. Comparable products: pack opening, collection, trading

1. **Topps apps (Marvel/Disney Collect, BUNT).** When Topps changed the pack-opening format, users called it slow and "like a casino game, I hate casino games". Users also say real pull rates feel worse than the published odds ("odds claim to be 1:25 packs but in reality you purchase 75 packs"). Other complaints are lost progress after re-login and greedy 2:1 trade demands. What users praise is that the apps are free-to-play friendly and that the developers answer reviews. [Community]
   - https://justuseapp.com/en/app/1192470294/marvel-collect-by-topps/reviews
   - https://screenwiseapp.com/guides/topps (a parent guide that calls pack opening "essentially a loot box" and says it is addictive for kids)
   - A 2026 lawsuit over Topps physical "Mega Box" odds shows that odds credibility is a reputational and legal issue: https://www.law360.com/articles/2436018 [Press]
2. **Japanese digital trading cards (デジタルトレカ).** Official fan apps (Kiramune, Liyuu, Liella!, Aqours Collection) all advertise fan-to-fan trading toward a complete set as a core loop. ぶいとれか is a VTuber-focused service launched in October 2024. It uses animated cards and voice cards as its rare tiers and has friend trading and user rankings. DiiCa uses per-artist gacha. Notably, DiiCa lets users set cards as their profile background **and download cards to the phone as images**. [Press/Store]
   - https://apps.apple.com/us/app/id6497652875 · https://apps.apple.com/app/id6473574981 · https://www.moguravr.com/?p=303232 · https://app-liv.jp/5349553/
3. **Japan Sumo Association digital cards (大相撲コレクション).** On social media, the complaints sound like social-game complaints: "20回回したのに出ず…" (20 pulls without getting the card) and "トレード制限は改悪" (the trade restrictions made it worse). Users experience trade limits as a downgrade. [Press] https://www.jprime.jp/articles/-/41239
4. **Pokémon TCG Pocket** (the largest recent digital-card trading launch). Trading required rarity matching plus "trade tokens", and the developers said this was to stop bots and multi-account abuse. The backlash was severe: trading one 3-diamond card cost five other 3-diamond cards. The developers promised to replace the tokens. **Lesson:** anti-abuse friction that users perceive as a resource tax is what gets the anger, not the anti-abuse goal itself. [Press]
   - https://videogameschronicle.com/news/pokemon-tcg-pockets-trading-feature-is-changing-after-fan-backlash · https://www.pockettactics.com/pokemon-tcg-pocket/trade-update · https://dexerto.com/pokemon/pokemon-tcg-pocket-dev-responds-to-trading-backlash-3053072
5. **Twitch-integrated collectibles (Streamloots).** Viewers buy random card packs with rarity tiers from Common to Legendary. The card's value is **on-stream use**: alerts, sounds, actions. Cards are played from a browser extension that works on Twitch, YouTube and Kick. Reviewers worry about abusive "show message" cards and about platform fees of 20–30%. Our product is collection-first. Streamloots shows that the stream moment drives the impulse. [Community/Vendor]
   - https://www.streamloots.com/cards · https://medium.com/streamloots/what-is-streamloots-b756807b3517 · https://www.streamscheme.com/?p=29711
6. **Taiwan/HK equivalents.** I found no Taiwan- or HK-native streamer digital-card product. The nearest local context is gacha-odds controversies (丁特 vs 天堂M, 2021) and account/item RMT on 8591 (see F2.4). PTT/Dcard reviews of comparable products were not found in this pass. **Gap.**

### F2. Trading UX, scams, disputes

1. **Disney Collect (Topps) trading mechanics and scam types** (from a community best-practices guide that cites a Topps PSA) [Community]:
   - Offers expire after 24 hours. A trade becomes "Voided" if a card in it changes status. Statuses are Declined, Expired and Voided.
   - Known scams: "I'm leaving the app and giving away all my cards", the "accept within 3 seconds" game, and cross-app trades (Topps calls these "extremely risky"). Users who look new get flooded with lowball offers. Reused art makes it easy to confuse a base card with an uncommon.
   - Countermeasures users rely on: trader score and trade history, a "reverse test" (would you accept this offer if roles were swapped?), and report through the profile instead of calling people out publicly.
   - https://docs.google.com/document/u/0/d/1xjzXk6Pfk6oHML6hGAn0izDBgl-FwzB1qaLSwMBsQaE/mobilebasic
   - Parent guide: a new player who pulls a Legendary gets "flooded with trade offers" that only look generous. https://screenwiseapp.com/guides/topps
2. **Steam trade holds.** Steam applies a 15-day hold unless the account has had the mobile authenticator for 7 days (long-time friends get 1 day). The stated purpose is to give the owner time to cancel trades made by someone who stole the password. **Account takeover**, not a bad deal, is the main fraud vector, and a hold is its countermeasure. Scammers also exploit holds through fake "hold removal" services. [Official]
   - https://help.steampowered.com/faqs/view/451E-96B3-D194-50FC · https://store.steampowered.com/news/20631
3. **TCG Pocket.** Rarity-matched trades are an effective anti-bot rule. Costly consumables on top of that rule caused the backlash. [Press] (links in F1.4)
4. **Taiwan RMT channel.** 8591 is Taiwan's dominant marketplace for game items and accounts. Scammers lure buyers into off-platform LINE Pay deals with no escrow. Buyers of accounts get re-bound out of them through Apple ID tricks. Courts have convicted people who lent 8591 accounts for laundering fraud money through virtual currency. A "no cash value" clause will not stop listings. It only lets us refuse support. [Press/Court/Community]
   - https://www.thenewslens.com/article/257396 · https://www.dcard.tw/f/mood/p/254474404/b/1 · https://judgment.judicial.gov.tw/FILES/CYDM/114,嘉簡,119,20250211,1.pdf

### F3. Screenshot protection sentiment

1. **No public complaint thread about a collectible app blocking screenshots** turned up in English or Japanese searches. That is weak evidence the practice is uncommon in this category, not that users accept it. The closest benchmark is DiiCa, which goes the other way and *lets users save card images* (F1.2). [Community/Press]
2. **Platform reality.** Android `FLAG_SECURE` behaves differently across devices: some show a black image and some scramble it. Google's own guidance suggests **"adding a setting in your app that allows users to toggle this flag"** for transparency and control. Android 14 adds a screenshot *detection* callback. iOS cannot block a still screenshot. It can only detect one afterward and obscure content during recording or mirroring. Power users discuss root modules to defeat `FLAG_SECURE`, so determined users always get around it. [Official/Community]
   - https://developer.android.com/security/fraud-prevention/activities · https://developer.android.com/about/versions/14/features/screenshot-detection · https://discuss.grapheneos.org/d/10342-how-to-disable-flag-secure-screenshot-block-in-certain-apps/9 · https://macmyths.com/why-android-says-an-app-does-not-allow-screenshots/
3. **[Inference]** Fans share pulls on social media; "開箱" posts are free marketing for the streamer. A blocked screenshot with no explanation reads as "the app is broken" or "the app doesn't trust me". If a legitimate, watermarked share path sits one tap away, the same block reads as protecting the streamer's art and the value of serials.

### F4. Regulation (Taiwan + stores), *legal review* required

1. **Taiwan probability disclosure (網路連線遊戲服務定型化契約應記載及不得記載事項).** Amended on 2022-08-10 and effective 2023-01-01. The competent authority is now 數位發展部. [Official]
   - Odds must be shown on the official site's front page, the login page **or the purchase page**, and on packaging.
   - Odds must be stated **as percentages**.
   - The rule applies wherever there is payment, *including indirect or partial payment and items converted later*.
   - Draw records (電磁紀錄) must be kept true and complete for **at least 180 days after the activity ends**, for verification in disputes.
   - Missing disclosure can lead to an order to correct and fines (reported as NT$30k–300k). False odds can be treated as false advertising under 公平交易法. MODA has worked with academics on a statistical audit method. A guaranteed-acquisition mechanism (保底) is *encouraged*, not required.
   - https://www.cna.com.tw/news/ahel/202209270220.aspx · https://technews.tw/2021/12/01/mobile-game-probabilistic-goods-must-be-revealed/ · https://www.ey.gov.tw/Page/DFB720D019CCCB0A/964028ea-f1f6-4383-9c78-f7d0606086f3 · https://files.chcg.gov.tw/files/55_20221102095507447_111年11月份消費者保護宣導(政風).pdf · https://www-api.moda.gov.tw/File/Get/moda/zh-tw/R1uZ1lYuSxIV3f5
   - *Applicability:* we are not a "網路連線遊戲" in the classic sense. However, the regulator's view that any paid random item is covered makes this the safe reference standard.
2. **Minors.** Since 2023 the age of majority in Taiwan is 18, so a 13–17 user is a 限制行為能力人. Under 民法 §77/§79, their contracts need ratification by a legal guardian. The game contract template requires guardian read-and-consent before minors use the service, and refunds of **unused** fees when a minor paid without consent. Streamers report chargebacks after big donation bursts, citing "stolen card" or "minor paid". China bans minors from tipping streamers outright, which is a reference point only. [Official/Press]
   - https://srps.kl.edu.tw/news/file/780/網路連線遊戲服務定型化契約應記載及不得記載事項.pdf · https://vocus.cc/article/6978803dfd89780001957386 · https://www.toy-people.com/?p=104902 · https://www.cool3c.com/article/177203 · https://hypebeast.com/2020/8/teenager-spends-20000-twitch-donations-debit-card
3. **7-day withdrawal right (消保法 §19).** Online digital content is exempt from the withdrawal right **only if** the consumer gave *prior consent* to immediate delivery **and** was told *clearly* that the withdrawal right is excluded. The regulator says the notice belongs in a prominent spot near the product, not buried in T&Cs. Courts read the exception strictly. [Official/Court]
   - https://law.moj.gov.tw/LawClass/LawAll.aspx?PCode=J0170012 · https://www.ey.gov.tw/Page/8F51FEE53AB45EA9/d09b1f97-08be-4e98-9726-3417fce28004 · https://www.legis-pedia.com/dictionary/53
4. **Apple App Store** (current Review Guidelines, verbatim) [Official] https://developer.apple.com/app-store/review/guidelines/
   - 3.1.1: loot boxes "must disclose the odds of receiving each type of item … prior to purchase."
   - 3.1.1: "Apps may not use their own mechanisms to unlock content or functionality, such as license keys … QR codes…" Also: "Digital gift cards, certificates, vouchers, and coupons which can be redeemed for digital goods … can only be sold in your app using in-app purchase."
   - **3.2.1(vii): a monetary gift that bypasses IAP is allowed only if 100% goes to the receiver, "However, a gift that is connected to or associated at any point in time with receiving digital content or services must use in-app purchase."**
   - 5.1.1(v): in-app account deletion is required.
   - 1.2.1(a): creator content needs an age-restriction mechanism.
5. **Google Play.** Odds must be disclosed before purchase. Play Billing is required for digital goods sold in the app. Steering users to other payment methods inside the app is prohibited. Developers may talk about outside offers through channels outside the app, and content bought elsewhere may be consumed in the app. [Official/Press]
   - https://fenwick.com/insights/publications/google-play-now-requires-disclosure-of-loot-box-odds · https://support.google.com/googleplay/android-developer/answer/10281818

### F5. Onboarding: email → install → sign-in → redeem

1. The biggest loss happens at the **store hop**: context disappears and the user lands on a generic home screen ("ghost installs"). Deferred deep linking keeps the context. Vendor estimates put the loss at 40–60% of installs, and one practitioner reports 65% drop-off on store redirect. These figures are unverified vendor or community numbers, so use them for direction only. [Vendor/Community]
   - https://www.airbridge.io/en/blog/deferred-deeplink-for-onboarding · https://linkrunner.io/blog/deferred-deep-links-explained-how-to-preserve-context-through-app-install · https://community.web2wave.com/t/reducing-app-store-drop-offs-in-deferred-deep-link-flows/1289
2. **Code-in-clipboard on iOS.** Since iOS 16, reading the clipboard programmatically triggers an "Allow Paste" system prompt. Branch recommends a user-initiated `UIPasteControl` instead of automatic reads. [Official/Vendor]
   - https://www.branch.io/resources/blog/everything-you-need-to-know-about-ios-16-and-pasteboard-opt-ins/ · https://sarunw.com/posts/uipasteboard-privacy-change-ios16/
3. Apple has no install-referrer equivalent, so iOS deferred-link matching is probabilistic. Android has the Play Install Referrer. [Vendor] https://www.airbridge.io/en/blog/deferred-deeplink-vs-standard-deep-link
4. **[Inference] from our own flow.** A first-time viewer goes through up to six handoffs: donation page → email (often in spam or the Promotions tab) → store → install → email OTP (a second email, which expires in 10 min) → typing a 24-character code. Each handoff is a drop-off point.

---

## 2. Prioritized recommendations (mapped to issues)

P0 = must address before launch, P1 = strongly recommended, P2 = nice to have.

| # | Pri | Issue | Recommendation | Evidence |
|---|---|---|---|---|
| R1 | P0 | #346, #343 | **Get an App Store / Play payments review of the core model before building the iOS redeem screen.** A donation that grants a random digital card, redeemed in-app by code, matches Apple 3.2.1(vii) ("gift … associated with digital content must use IAP") and the 3.1.1 rules on unlock codes and vouchers. Options to evaluate with counsel: (a) the iOS app is a **viewer only**: no purchase or donate CTA, no links to the shop, and redemption and claim happen on the Hub web page; (b) offer packs through IAP as well (3.1.3(b) multiplatform); (c) launch Android plus web first. Write the reviewer notes ahead of time. | F4.4, F4.5 |
| R2 | P0 | #342, #346 | **Odds disclosure in TWD percentages on the donate page itself, next to the amount**, showing every rarity tier with 2 decimal places. Put the version of the odds that applied on each `pack_draws` row (snapshot odds_json and version), and keep draw records ≥180 days after the activity ends. Publish whether odds are **fixed-probability** or **finite-pool** (supply_limit). With a finite pool, rarity odds change as supply runs out, so show "remaining" counts or the live odds. | F4.1 |
| R3 | P0 | #342, #346 | **Minors path.** At donation time the streamer site knows nothing about age. Add an age declaration ("I am 18+ / I am 13–17 and have guardian consent") to the donate form when a card pack is attached. Add a per-viewer spending notice for 13–17, and a written refund/chargeback SOP: refund **unredeemed** draws on a guardian's request, and void the code. Decide policy on serials from reversed payments: retire the instance, or let the viewer keep it. | F4.2 |
| R4 | P0 | #342 | **7-day withdrawal exception notice.** Before paying, the viewer ticks an explicit, prominent checkbox: "I agree the card is delivered immediately and I understand the 7-day withdrawal right does not apply". Store the consent with the payment reference. | F4.3 |
| R5 | P0 | #343, #342 | **Make the code optional for the donor themselves.** When the viewer signs in to the app with the **same email** the draw was sent to, unclaimed draws for that email appear automatically ("You have 1 unopened pack from 某某實況主"). The code stays as the gifting/transfer path and the fallback. This removes the 24-character typing step for most users. | F5.4 |
| R6 | P0 | #342, #343 | **One link in the email, not a bare code.** Use a universal/App Link `https://hub…/r/<code>`. If the app is installed, it opens straight into the reveal. If not, a web landing page shows the sealed pack art and the streamer name, then gives the store button. Carry the code through deferred deep link (Play Install Referrer on Android) plus a **user-tapped** paste button (`UIPasteControl`) on iOS. Never read the clipboard silently. Pre-fill the OTP email from the link. | F5.1–F5.3 |
| R7 | P1 | #341 | **Make the animation fast to skip and keep it from feeling like a casino.** First open: full timeline. Repeat opens: a "quick reveal" toggle that is remembered. Tap-anywhere skip after the tear. Total ≤3 s in quick mode. Keep slot-machine cues out of the mobile version (spinning reels, near-miss teases, coin sounds). Tie rarity flair to the *card art* (animated or holo), not to gambling iconography. Turn the effects down for 13–17 accounts. Reduced-motion support is already in the plan. | F1.1, F1.2 |
| R8 | P1 | #341 | **Reveal screen = the "share moment".** After the reveal, show serial, rarity and set progress ("3/12 in this set"), then "Set as wallpaper" and "Share" (a watermarked share image). Rare tiers can use animation or voice, which is the ぶいとれか pattern. | F1.2, F3.3 |
| R9 | P1 | #343 | **Explain capture protection before the user hits it.** On first visit to a card screen, show a one-time sheet: "為保護實況主的原創卡面與序號價值，卡片畫面無法截圖。想分享？點「分享」產生帶浮水印的官方圖片。" ("To protect the streamer's original card art and serial value, card screens can't be screenshotted. Want to share? Tap 'Share' to make an official watermarked image.") After that, a **detected** iOS screenshot gets a friendly toast with a Share button, never a warning tone. Leave non-card screens (settings, lists without art) capturable so users can send support screenshots. | F3.1–F3.3 |
| R10 | P1 | #344 | **Trade review screen with explicit diffs.** Side by side: card art, **rarity badge, serial, streamer** for both sides. Warn when the trade is lopsided ("You give 1 SSR and get 2 N"). Require a hold-to-confirm, not a single tap. Show no countdowns, which defeats the "accept within 3 seconds" scam. | F2.1 |
| R11 | P1 | #344 | **Offer lifecycle.** Offers expire after 72 h. Auto-void with a clear reason when an instance changes owner (the plan's atomic re-check already covers this). Cap pending outgoing offers. Add trade history and a simple trader reputation (completed trades, account age). Add block and report per user. | F2.1 |
| R12 | P1 | #344 | **Targeted trade holds, not a resource tax.** Put a 72 h–7 day hold on trades **sent by** newly created accounts and after an email change or new-device sign-in, which are takeover signals (the Steam rationale). Block one-way "gift" trades from accounts younger than N days. Do **not** charge tokens or currency to trade. | F2.2, F1.4 |
| R13 | P1 | #344, #346 | **No free-text chat in trades** (and none in v1 at all). This removes the off-platform lure (LINE Pay, 8591, fake support) and the main grooming vector when 13–17 users trade with adults. Use only preset reactions or "wanted list" tags. | F2.4, F4.2 |
| R14 | P1 | #346 | **RMT stance and monitoring.** Terms ban sale for money (already planned). Also: report trade graphs with many one-way transfers to a single account, add a "we will never ask you to pay or contact support via LINE" banner on trade screens, and write a support SOP for account-takeover recovery that reverses trades within the hold window. | F2.4 |
| R15 | P2 | #343 | **Collection browsing.** Default to per-streamer shelves, showing set completion and missing silhouettes for cards not yet owned. Add a filter by rarity. Custom groups are planned already. Keep a "wanted" list that feeds trade matching. | F1.2 |
| R16 | P2 | #346 | **Consider a pity / duplicate-protection mechanic** (保底). Taiwan's regulator encourages it, and it directly answers the "20 pulls without the card" complaint. Avoid "complete-the-set for a paid prize" mechanics (Japan's コンプガチャ precedent; Taiwan applicability to be checked). | F4.1, F1.3 |

---

## 3. Risks to our current plan

1. **App Store payment model (high).** Our core loop of donating outside the app and unlocking a digital card in the app with a code is exactly what Apple 3.2.1(vii) and 3.1.1 (unlock codes, vouchers) describe. Rejection, or a forced switch to IAP with a 15–30% cut and a different payout flow, would change #342 and #343 fundamentally. The plan's "App store review notes declare the odds disclosure" covers only the loot-box rule, not the payment rule. Also, the plan says odds appear "in the app before any donation". A donate CTA in the iOS app would be in-app steering.
2. **Odds disclosure is not yet specified in a compliance-ready form.** Gaps: percentage format; disclosure *on the purchase/donate page*; per-draw odds version for audit; ≥180-day record retention after the activity ends (our 180-day *email* retention is a different thing); how finite `supply_limit` interacts with stated odds; what happens to odds when a published pack runs out mid-stream.
3. **Age bracket is collected at the wrong moment.** `age_bracket` is set at Hub sign-up, but the *payment* happens earlier on the streamer site, where we know nothing about age. There is no guardian-consent step, no refund path for minors, and no chargeback handling for draws that were already redeemed or traded.
4. **7-day withdrawal right.** Without explicit prior consent and a prominent notice, a donor could arguably withdraw within 7 days after seeing a bad pull.
5. **The onboarding funnel is long.** Two separate emails (the code email and the OTP email), a 24-character code and the store hop all stack up. Without same-email auto-claim (R5) and link-based redemption (R6), many donors will never open their pack. That also weakens the streamer's OBS moment.
6. **Capture protection is asymmetric and can be bypassed.** iOS can only *detect* still screenshots, and camera photos can't be prevented. If we market "screenshot-proof", users on iOS will prove otherwise. Present it as a deterrent plus an official share path, not as a guarantee. An unexplained black screenshot on Android risks "app broken" 1-star reviews.
7. **Trading without anti-takeover or anti-RMT measures.** Atomic transfer solves concurrency, not fraud. Without new-account and new-device holds, a trade review diff and reporting, the first rare serials will attract account-takeover and 8591 resale. Cross-streamer trading widens the blast radius. A simple 20-per-day cap does not address one-way draining.
8. **Minors trading with adults.** Trading is cross-streamer and open between 13–17 and 18+ users. Any messaging feature added later raises child-safety obligations. The plan currently has no rule on this.
9. **Overuse of friction.** In the other direction, TCG Pocket and the Sumo app show that restrictions perceived as a tax cause backlash. Keep each hold targeted and explain it.

---

## 4. Evidence gaps / next research steps

- No PTT/Dcard threads were found on digital trading cards or on screenshot blocking specifically. Next step: run a 5–8 person guerrilla test with Taiwanese viewers of the pilot streamer's community, covering the email-to-reveal funnel, the capture-block explainer copy and the trade review screen.
- Exact current wording of the MODA 應記載事項 (any 2024–2026 revision) and its applicability to a non-game collectible need counsel.
- Store-review search for ぶいとれか/DiiCa/Bandai apps found no detail on trade rules or screenshot policy. Next step: read their 利用規約 and FAQ directly.
- Deep-link drop-off percentages are vendor estimates. Instrument our own funnel (email open → link click → install → sign-in → reveal) from day one.
