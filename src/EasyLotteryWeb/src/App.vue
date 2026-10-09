<script setup lang="ts">
import { RouterView, useRoute } from "vue-router";
import AdminLayout from "./components/AdminLayout.vue";
import PublicLayout from "./components/PublicLayout.vue";

const route = useRoute();
</script>

<template>
  <v-app>
    <AdminLayout v-if="route.meta.requiresAuth">
      <RouterView />
    </AdminLayout>
    <PublicLayout v-else>
      <RouterView />
    </PublicLayout>
  </v-app>
</template>
