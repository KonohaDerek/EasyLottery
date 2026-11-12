<template>
  <v-app-bar flat border :height="xs ? 112 : 64">
    <v-container class="public-nav d-flex align-center py-0">
      <RouterLink class="brand" to="/about">EasyLottery</RouterLink>
      <v-spacer />
      <RouterLink class="nav-link" to="/about">關於</RouterLink>
      <RouterLink class="nav-link" to="/privacy-policy">隱私權政策</RouterLink>
      <RouterLink class="nav-link" to="/login">管理員登入</RouterLink>
    </v-container>
  </v-app-bar>

  <v-main class="public-main">
    <v-container class="py-10" max-width="960">
      <slot />
    </v-container>
  </v-main>
</template>

<script setup lang="ts">
import { useDisplay } from "vuetify";

const { xs } = useDisplay();
</script>
