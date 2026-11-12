<script setup lang="ts">
defineProps<{ message: string }>();
</script>

<template>
  <div class="obs-error-state" role="alert">
    <p>{{ message }}</p>
    <slot />
  </div>
</template>

<style scoped>
.obs-error-state {
  position: relative;
  z-index: 10;
  box-sizing: border-box;
  width: fit-content;
  max-width: min(36rem, calc(100vw - 2rem));
  margin: 1rem;
  padding: 1rem 1.25rem;
  border: 1px solid rgba(255, 255, 255, .3);
  border-radius: .8rem;
  background: rgba(15, 23, 42, .95);
  color: #fff;
  overflow-wrap: anywhere;
}
p { margin: 0; }
</style>
