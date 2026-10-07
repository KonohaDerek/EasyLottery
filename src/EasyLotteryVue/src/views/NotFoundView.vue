<template>
  <main class="page-content not-found" aria-labelledby="not-found-title">
    <v-icon color="primary" icon="mdi-map-marker-question-outline" size="48" aria-hidden="true" />
    <p class="eyebrow">404</p>
    <h1 id="not-found-title" class="page-title">找不到這個頁面</h1>
    <p class="page-lead">請檢查網址，或回到新版首頁繼續使用。</p>
    <v-btn class="home-button" color="primary" :to="{ name: 'home' }">回到首頁</v-btn>
  </main>
</template>

<style scoped>
.not-found {
  display: flex;
  min-height: 60vh;
  align-items: flex-start;
  justify-content: center;
  flex-direction: column;
}

.not-found .eyebrow {
  margin-top: 1.5rem;
}

.home-button {
  margin-top: 1.5rem;
}
</style>
