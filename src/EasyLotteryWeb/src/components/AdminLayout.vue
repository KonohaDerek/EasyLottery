<script setup lang="ts">
import { useRouter } from "vue-router";
import { useSessionStore } from "../stores/session";
import { clearOvertimeToken } from "../lottery/api";

const router = useRouter();
const session = useSessionStore();

const links = [
  ["/", "抽獎工作區"],
  ["/donate-activities", "Donate 活動"],
  ["/activity-results", "活動結果"],
  ["/interactions/rounds", "直播互動回合"],
  ["/interactions/identity-link", "觀眾身份綁定"],
  ["/roulette", "轉盤模板"],
  ["/pokebox", "戳戳樂模板"],
  ["/system/access", "管理員 Passkey"],
  ["/system/payment", "支付配置"],
  ["/system/youtube-login", "YouTube API Key"],
  ["/system/audit", "審計紀錄"],
  ["/system/backups", "設定備份"],
  ["/system/obs-layouts", "OBS 版面"],
  ["/system/obs-assets", "OBS 資產"],
  ["/system/sound-cues", "音效預設"],
  ["/system/visual-styles", "視覺樣式"]
] as const;

function logout() {
  clearOvertimeToken();
  session.clearToken();
  router.push("/login");
}
</script>

<template>
  <v-app-bar color="primary" class="admin-header">
    <v-menu location="bottom start">
      <template #activator="{ props }">
        <v-btn v-bind="props" class="mobile-admin-menu" variant="text" aria-label="管理選單">選單</v-btn>
      </template>
      <v-list class="mobile-admin-menu-list" density="compact" nav>
        <v-list-item v-for="[path, label] in links" :key="path" :to="path" :title="label" />
      </v-list>
    </v-menu>
    <v-app-bar-title>EasyLottery 管理設定</v-app-bar-title>
    <v-btn class="admin-public-link" variant="text" to="/about">回到公開頁</v-btn>
    <v-btn variant="text" @click="logout">登出</v-btn>
  </v-app-bar>

  <v-navigation-drawer permanent class="admin-drawer">
    <nav class="admin-nav" aria-label="管理設定">
      <RouterLink v-for="[path, label] in links" :key="path" :to="path" class="admin-nav-link">
        {{ label }}
      </RouterLink>
    </nav>
  </v-navigation-drawer>

  <v-main class="admin-main">
    <v-container class="admin-content" fluid>
      <slot />
    </v-container>
  </v-main>
</template>
