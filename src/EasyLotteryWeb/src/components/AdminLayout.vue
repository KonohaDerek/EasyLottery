<script setup lang="ts">
import { useRouter } from "vue-router";
import { useSessionStore } from "../stores/session";

const router = useRouter();
const session = useSessionStore();

const links = [
  ["/donate-activities", "Donate 活動"],
  ["/activity-results", "活動結果"],
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
  session.clearToken();
  router.push("/login");
}
</script>

<template>
  <v-app-bar color="primary" class="admin-header">
    <v-app-bar-title>EasyLottery 管理設定</v-app-bar-title>
    <v-btn variant="text" to="/about">回到公開頁</v-btn>
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
