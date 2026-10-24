<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useSessionStore } from "../stores/session";
import { clearOvertimeToken } from "../lottery/api";
import { adminNavigationGroups, getActiveAdminNavigationPath } from "../navigation/admin-navigation";

const router = useRouter();
const route = useRoute();
const session = useSessionStore();
const mobileMenuOpen = ref(false);
const groups = adminNavigationGroups;
const activeNavigationPath = computed(() => getActiveAdminNavigationPath(route.path));

function groupContainsCurrentPath(links: readonly (readonly [string, string])[]): boolean {
  return links.some(([path]) => activeNavigationPath.value === path);
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
            <RouterLink v-for="[path, label] in group.links" :key="path" :to="path" class="admin-nav-link" :aria-current="activeNavigationPath === path ? 'page' : undefined" @click="mobileMenuOpen = false">
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
        <RouterLink v-for="[path, label] in group.links" :key="path" :to="path" class="admin-nav-link" :aria-current="activeNavigationPath === path ? 'page' : undefined">
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
