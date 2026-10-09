<script setup lang="ts">
import { RouterView } from "vue-router";
import PublicLayout from "./components/PublicLayout.vue";
</script>

<template>
  <v-app>
    <PublicLayout>
      <RouterView />
    </PublicLayout>
  </v-app>
</template>
