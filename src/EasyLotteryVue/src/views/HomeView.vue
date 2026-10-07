<script setup lang="ts">
const nextSteps = [
  {
    title: '管理活動',
    description: '接下來會逐步搬入 Donate、戳戳樂與轉盤活動管理。',
    icon: 'mdi-party-popper',
  },
  {
    title: '管理 OBS 呈現',
    description: '版型、資產和直播畫面會在後續階段接上新的介面。',
    icon: 'mdi-monitor-eye',
  },
]
</script>

<template>
  <main class="page-content home-page">
    <section class="welcome-panel" aria-labelledby="home-title">
      <div class="welcome-copy">
        <p class="eyebrow">EASYLOTTERY · 新版介面</p>
        <h1 id="home-title" class="page-title">讓直播互動更順手</h1>
        <p class="page-lead">
          EasyLottery 把直播互動、抽獎活動與 OBS 呈現放在同一個工作流程。
          新版介面正分階段接手功能，尚未搬入的頁面可從舊版系統繼續使用。
        </p>
        <div class="welcome-actions">
          <v-btn color="primary" size="large" prepend-icon="mdi-key-chain-variant" to="/system/access">
            管理 Passkey
          </v-btn>
          <v-btn variant="text" size="large" href="/">
            開啟完整舊版
          </v-btn>
        </div>
      </div>
      <div class="welcome-mark" aria-hidden="true">
        <div class="lottery-orbit orbit-one" />
        <div class="lottery-orbit orbit-two" />
        <div class="lottery-ticket">
          <span>EL</span>
          <v-icon icon="mdi-ticket-confirmation-outline" size="72" />
        </div>
      </div>
    </section>

    <section class="overview-section" aria-labelledby="available-title">
      <div class="section-intro">
        <div>
          <p class="eyebrow">目前可用</p>
          <h2 id="available-title">新版工作區</h2>
        </div>
        <v-chip color="success" variant="tonal" prepend-icon="mdi-check-circle-outline">
          基礎功能已就緒
        </v-chip>
      </div>

      <v-row>
        <v-col cols="12" md="6">
          <v-card class="content-card feature-card" :to="{ name: 'about' }" variant="flat">
            <v-avatar color="primary" variant="tonal" size="52">
              <v-icon icon="mdi-information-outline" size="26" />
            </v-avatar>
            <h3>關於 EasyLottery</h3>
            <p>了解這套直播活動工具的用途與核心功能。</p>
            <v-icon class="feature-arrow" icon="mdi-arrow-right" aria-hidden="true" />
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card class="content-card feature-card" :to="{ name: 'privacy' }" variant="flat">
            <v-avatar color="secondary" variant="tonal" size="52">
              <v-icon icon="mdi-shield-account-outline" size="26" />
            </v-avatar>
            <h3>隱私權與資料處理</h3>
            <p>查看自架部署環境中設定、活動與登入資料的處理方式。</p>
            <v-icon class="feature-arrow" icon="mdi-arrow-right" aria-hidden="true" />
          </v-card>
        </v-col>
      </v-row>
    </section>

    <section class="overview-section" aria-labelledby="coming-title">
      <div class="section-intro">
        <div>
          <p class="eyebrow">接續搬遷</p>
          <h2 id="coming-title">逐步帶入熟悉的工具</h2>
        </div>
      </div>
      <v-row>
        <v-col v-for="step in nextSteps" :key="step.title" cols="12" md="6">
          <v-card class="content-card next-card" variant="flat">
            <v-icon :icon="step.icon" color="primary" size="28" />
            <div>
              <h3>{{ step.title }}</h3>
              <p>{{ step.description }}</p>
            </div>
            <v-chip size="small" variant="outlined">規劃中</v-chip>
          </v-card>
        </v-col>
      </v-row>
    </section>
  </main>
</template>

<style scoped>
.welcome-panel {
  position: relative;
  display: flex;
  min-height: 22rem;
  align-items: center;
  justify-content: space-between;
  overflow: hidden;
  padding: clamp(1.75rem, 5vw, 3.5rem);
  border: 1px solid #dfeae4;
  border-radius: 1.6rem;
  background: linear-gradient(116deg, #fff 0%, #f2f8f4 58%, #eaf4ed 100%);
  box-shadow: 0 20px 60px rgb(18 76 56 / 8%);
}

.welcome-copy {
  position: relative;
  z-index: 1;
  max-width: 44rem;
}

.welcome-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 1.7rem;
}

.welcome-mark {
  position: absolute;
  top: 50%;
  right: 4%;
  width: 19rem;
  height: 19rem;
  transform: translateY(-50%);
}

.lottery-orbit {
  position: absolute;
  inset: 0;
  border: 1px solid rgb(20 92 75 / 14%);
  border-radius: 50%;
}

.orbit-two {
  inset: 2rem;
  border-style: dashed;
  transform: rotate(-18deg);
}

.lottery-ticket {
  position: absolute;
  inset: 4.25rem;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  gap: 0.5rem;
  border: 1px solid rgb(20 92 75 / 10%);
  border-radius: 2rem;
  background: rgb(255 255 255 / 78%);
  color: #145c4b;
  box-shadow: 0 18px 45px rgb(20 92 75 / 12%);
  transform: rotate(-8deg);
}

.lottery-ticket span {
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.2em;
}

.overview-section {
  margin-top: 2.8rem;
}

.section-intro {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.15rem;
}

.section-intro .eyebrow {
  margin-bottom: 0.25rem;
}

.section-intro h2 {
  margin: 0;
  color: #214c3f;
  font-size: clamp(1.35rem, 3vw, 1.75rem);
}

.feature-card {
  position: relative;
  display: flex;
  min-height: 13rem;
  align-items: flex-start;
  flex-direction: column;
  gap: 1rem;
  color: inherit;
  text-decoration: none;
  transition: transform 160ms ease, box-shadow 160ms ease;
}

.feature-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 18px 42px rgb(21 74 57 / 11%);
}

.feature-card h3,
.next-card h3 {
  margin: 0;
  color: #214c3f;
  font-size: 1.12rem;
}

.feature-card p,
.next-card p {
  margin: 0;
  color: #61756c;
  line-height: 1.65;
}

.feature-arrow {
  position: absolute;
  right: 1.5rem;
  bottom: 1.4rem;
  color: #51796a;
}

.next-card {
  display: flex;
  min-height: 8.5rem;
  align-items: flex-start;
  gap: 1rem;
}

.next-card > div {
  flex: 1;
}

.next-card p {
  margin-top: 0.45rem;
}

@media (max-width: 760px) {
  .welcome-panel {
    min-height: 0;
  }

  .welcome-mark {
    top: auto;
    right: -5rem;
    bottom: -9rem;
    width: 16rem;
    height: 16rem;
    transform: none;
    opacity: 0.35;
  }

  .section-intro {
    align-items: flex-start;
    flex-direction: column;
  }
}

@media (prefers-reduced-motion: reduce) {
  .feature-card {
    transition: none;
  }
}
</style>
