<script setup lang="ts">
import PresetSettingsView from "./PresetSettingsView.vue";
</script>

<template>
  <PresetSettingsView
    title="視覺樣式"
    endpoint="/api/settings/visual-style"
    value-key="activeThemeKey"
    field-label="目前視覺樣式"
    :image-fields="true"
    :presets="[
      { key: 'dashboard-console', label: '控制台藍白' },
      { key: 'arcade-neon', label: '霓虹街機' },
      { key: 'gacha-loot', label: '扭蛋寶箱' },
      { key: 'festival-stage', label: '祭典舞台' }
    ]"
  />
</template>
