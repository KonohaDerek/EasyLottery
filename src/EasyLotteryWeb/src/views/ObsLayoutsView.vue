<script setup lang="ts">
import PresetSettingsView from "./PresetSettingsView.vue";
</script>

<template>
  <PresetSettingsView
    title="OBS 版面"
    endpoint="/api/settings/obs-layout"
    value-key="activeLayoutKey"
    field-label="目前版面"
    :presets="[
      { key: 'stage-spotlight', label: '舞台聚光燈' },
      { key: 'neon-panel', label: '霓虹分割面板' },
      { key: 'festival-pane', label: '祭典舞台面板' }
    ]"
  />
</template>
