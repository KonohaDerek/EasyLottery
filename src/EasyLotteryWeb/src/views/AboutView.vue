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
import { highlights } from "../about/highlights";
</script>
