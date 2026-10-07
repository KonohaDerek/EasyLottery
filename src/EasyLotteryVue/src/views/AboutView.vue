<script setup lang="ts">
const features = [
  {
    icon: 'mdi-party-popper',
    title: '為直播互動而設計',
    description: '集中管理 Donate、戳戳樂、轉盤與加班台等活動流程。',
  },
  {
    icon: 'mdi-monitor-eye',
    title: 'OBS 友善',
    description: '使用獨立的呈現頁面和資產網址，將活動畫面加入 OBS。',
  },
  {
    icon: 'mdi-database-check-outline',
    title: '資料由部署者管理',
    description: '活動設定、結果和備份保存於所選的自架儲存環境。',
  },
]
</script>

<template>
  <main class="page-content" aria-labelledby="about-title">
    <header>
      <p class="eyebrow">EASYLOTTERY</p>
      <h1 id="about-title" class="page-title">關於 EasyLottery</h1>
      <p class="page-lead">
        EasyLottery 是一套自架的直播活動抽獎工具，讓主持人可以在同一個系統管理活動、參加者、抽獎結果與 OBS 呈現。
      </p>
    </header>

    <v-row class="feature-grid">
      <v-col v-for="feature in features" :key="feature.title" cols="12" md="4">
        <v-card class="content-card about-card" variant="flat">
          <v-avatar color="primary" variant="tonal" size="52">
            <v-icon :icon="feature.icon" size="26" />
          </v-avatar>
          <h2>{{ feature.title }}</h2>
          <p>{{ feature.description }}</p>
        </v-card>
      </v-col>
    </v-row>

    <section class="capability-section" aria-labelledby="capabilities-title">
      <h2 id="capabilities-title" class="section-heading">目前提供的功能</h2>
      <div class="prose">
        <ul>
          <li>讀取公開 YouTube 直播資料及管理會員名單。</li>
          <li>建立 Donate 抽獎、戳戳樂和轉盤活動。</li>
          <li>記錄活動結果，並管理 OBS 圖片、音效與動畫資產。</li>
          <li>透過 Passkey 保護管理員功能。</li>
        </ul>
        <p>EasyLottery 採自架部署方式；功能可依伺服器設定與啟用的串接服務而有所不同。</p>
      </div>
    </section>
  </main>
</template>

<style scoped>
.feature-grid {
  margin-top: 1.75rem;
}

.about-card {
  display: flex;
  min-height: 15rem;
  align-items: flex-start;
  flex-direction: column;
  gap: 1.1rem;
}

.about-card h2 {
  margin: 0;
  color: #214c3f;
  font-size: 1.15rem;
}

.about-card p {
  margin: 0;
  color: #61756c;
  line-height: 1.7;
}
</style>
