<template>
  <section aria-labelledby="about-title">
    <p class="eyebrow">EASYLOTTERY</p>
    <h1 id="about-title" class="text-h3 text-sm-h2 mb-4">關於 EasyLottery</h1>
    <p class="lead mb-8">把直播互動、抽獎活動與 OBS 呈現集中在同一個工作流程。</p>

    <v-row>
      <v-col v-for="item in highlights" :key="item.title" cols="12" md="4">
        <v-card class="highlight-card h-100" variant="tonal">
          <v-card-title>{{ item.title }}</v-card-title>
          <v-card-text>{{ item.description }}</v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </section>
</template>

<script setup lang="ts">
const highlights = [
  { title: "為直播而設計", description: "用 Donate、戳戳樂、轉盤與加班台建立可重複使用的直播互動體驗。" },
  { title: "OBS 友善", description: "每個呈現畫面都能以透明背景嵌入 OBS，並可用專屬網址隔離不同活動。" },
  { title: "可控且可追溯", description: "活動設定、抽獎結果與稽核紀錄分離保存，方便管理、備份與日後擴充。" }
];
</script>
