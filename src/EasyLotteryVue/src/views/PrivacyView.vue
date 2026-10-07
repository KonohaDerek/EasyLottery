<template>
  <main class="page-content" aria-labelledby="privacy-title">
    <header>
      <p class="eyebrow">資料處理說明</p>
      <h1 id="privacy-title" class="page-title">隱私權政策</h1>
      <p class="page-lead">
        EasyLottery 以自架方式部署。實際資料由執行此服務的部署者管理；不同站台的設定、串接和保存期限可能不同。
      </p>
    </header>

    <v-card class="content-card privacy-card" variant="flat">
      <section class="prose" aria-labelledby="operator-title">
        <h2 id="operator-title" class="section-heading">誰負責管理資料？</h2>
        <p>
          請聯絡你正在使用的 EasyLottery 站台管理者，詢問該站台的資料管理者、保存期限、備份方式及刪除程序。
          EasyLottery 專案本身不會集中接收所有自架站台的資料。
        </p>

        <h2 class="section-heading">站台可能處理哪些資料？</h2>
        <p>依部署者啟用的功能，資料可能包含：</p>
        <ul>
          <li>抽獎活動設定、模板、參加者名稱、抽獎結果與 Donate 活動紀錄。</li>
          <li>管理員 email、Passkey 公開憑證資料，以及目前瀏覽器工作階段中的登入 token。</li>
          <li>部署者上傳的 OBS 圖片、音效、模型與相關中繼資料。</li>
          <li>為設定功能輸入的 YouTube API Key 及其他串接服務設定。</li>
        </ul>
        <p>
          應用資料會依站台設定保存於 YAML 或 SQLite 儲存目錄；部署者也可能透過備份保留資料。
          Passkey 私密金鑰留在你的裝置或密碼管理器，伺服器保存驗證所需的公開憑證資料。
          登入 token 保存在瀏覽器的 sessionStorage，並受其有效期限限制。
        </p>

        <h2 class="section-heading">第三方服務</h2>
        <p>
          若部署者啟用 YouTube 或支付串接，相關請求會依該功能設定傳送至對應服務。YouTube 資料會透過部署者設定的 API Key 取得；
          支付交易則由部署者設定的支付服務處理。這些服務各自的資料處理方式，依其政策及部署者與服務商的設定為準。
        </p>
        <p>
          YouTube 與 Google 服務的資料處理方式可參閱
          <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">Google 隱私權政策</a>。
        </p>

        <h2 class="section-heading">如何提出資料相關問題？</h2>
        <p>
          請聯絡該 EasyLottery 站台的管理者。管理者可查閱自己的儲存目錄、備份與串接設定，並依其資料管理程序處理你的請求。
        </p>
      </section>
    </v-card>
  </main>
</template>
