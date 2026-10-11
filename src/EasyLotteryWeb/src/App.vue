<script setup lang="ts">
import { onBeforeUnmount, watchEffect } from "vue";
import { RouterView, useRoute } from "vue-router";
import AdminLayout from "./components/AdminLayout.vue";
import PublicLayout from "./components/PublicLayout.vue";

const route = useRoute();

watchEffect(() => {
  document.body.classList.toggle("obs-overlay", route.meta.obs === true);
  document.documentElement.classList.toggle("obs-overlay", route.meta.obs === true);
});
onBeforeUnmount(() => {
  document.body.classList.remove("obs-overlay");
  document.documentElement.classList.remove("obs-overlay");
});
</script>

<template>
  <v-app :class="{ 'obs-app': route.meta.obs === true }">
    <AdminLayout v-if="route.meta.requiresAuth">
      <RouterView />
    </AdminLayout>
    <RouterView v-else-if="route.meta.obs === true" />
    <PublicLayout v-else>
      <RouterView />
    </PublicLayout>
  </v-app>
</template>
