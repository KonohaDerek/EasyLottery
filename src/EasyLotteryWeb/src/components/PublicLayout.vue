<template>
  <v-app-bar flat border :height="xs ? 112 : 64">
    <v-container class="public-nav d-flex align-center py-0">
      <RouterLink class="brand" to="/about">EasyLottery</RouterLink>
      <v-spacer />
      <RouterLink v-for="[path, label] in navigationLinks" :key="path" class="nav-link" :to="path">{{ label }}</RouterLink>
    </v-container>
  </v-app-bar>

  <v-main class="public-main">
    <v-container class="py-10" max-width="960">
      <slot />
    </v-container>
  </v-main>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useDisplay } from "vuetify";
import { getPublicNavigationLinks } from "../navigation/admin-navigation";
import { isShopEnabled } from "../shop/api";

const { xs } = useDisplay();
const shopEnabled = ref(false);
const navigationLinks = computed(() => getPublicNavigationLinks(shopEnabled.value));

onMounted(async () => {
  shopEnabled.value = await isShopEnabled();
});
</script>
