<script setup lang="ts">
import { ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "../stores/session";
import { clearOvertimeToken } from "../lottery/api";

const router = useRouter();
const route = useRoute();
const session = useSessionStore();
const mobileMenuOpen = ref(false);

const groups = [
  { title: "開台工作區", links: [["/", "抽獎工作區"], ["/donate-activities", "Donate 活動"], ["/activity-results", "活動結果"], ["/market", "活動市集"]] },
  { title: "直播互動與模板", links: [["/interactions/rounds", "直播互動回合"], ["/interactions/identity-link", "觀眾身份綁定"], ["/activity/overtime", "加班台設定"], ["/roulette", "轉盤模板"], ["/pokebox", "戳戳樂模板"]] },
  { title: "外觀與直播素材", links: [["/system/obs-layouts", "OBS 版面"], ["/system/obs-assets", "OBS 資產"], ["/system/sound-cues", "音效預設"], ["/system/visual-styles", "視覺樣式"]] },
  { title: "平台與系統管理", links: [["/system/platform-connections", "直播平台串接"], ["/system/youtube-login", "YouTube 直播設定"], ["/system/payment", "支付配置"], ["/system/access", "管理員 Passkey"], ["/system/audit", "審計紀錄"], ["/system/backups", "設定備份"]] }
] as const;

function groupContainsCurrentPath(links: readonly (readonly [string, string])[]): boolean {
  return links.some(([path]) => path === "/"
    ? route.path === path
    : route.path === path || route.path.startsWith(`${path}/`));
}

function logout() {
  clearOvertimeToken();
  session.clearToken();
  router.push("/login");
}
</script>

<template>
  <v-app-bar color="primary" class="admin-header">
    <v-menu v-model="mobileMenuOpen" :close-on-content-click="false" location="bottom start">
      <template #activator="{ props }">
        <v-btn v-bind="props" class="mobile-admin-menu" variant="text" aria-label="開啟管理功能導覽">選單</v-btn>
      </template>
      <v-list class="mobile-admin-menu-list" density="compact" nav>
        <nav class="admin-nav admin-nav--mobile" aria-label="管理功能分組">
          <details v-for="group in groups" :key="group.title" class="admin-nav-group" :open="groupContainsCurrentPath(group.links)">
            <summary>{{ group.title }}</summary>
            <RouterLink v-for="[path, label] in group.links" :key="path" :to="path" class="admin-nav-link" :aria-current="route.path === path ? 'page' : undefined" @click="mobileMenuOpen = false">
              {{ label }}
            </RouterLink>
          </details>
        </nav>
      </v-list>
    </v-menu>
    <v-app-bar-title>EasyLottery 管理設定</v-app-bar-title>
    <v-btn class="admin-public-link" variant="text" to="/about">回到公開頁</v-btn>
    <v-btn class="admin-logout" color="white" variant="outlined" aria-label="登出並清除管理員工作階段" @click="logout">登出</v-btn>
  </v-app-bar>

  <v-navigation-drawer permanent class="admin-drawer">
    <nav class="admin-nav" aria-label="管理功能分組">
      <details v-for="group in groups" :key="group.title" class="admin-nav-group" :open="groupContainsCurrentPath(group.links)">
        <summary>{{ group.title }}</summary>
        <RouterLink v-for="[path, label] in group.links" :key="path" :to="path" class="admin-nav-link" :aria-current="route.path === path ? 'page' : undefined">
          {{ label }}
        </RouterLink>
      </details>
    </nav>
  </v-navigation-drawer>

  <v-main class="admin-main">
    <v-container class="admin-content" fluid>
      <slot />
    </v-container>
  </v-main>
</template>
