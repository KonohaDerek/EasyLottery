<script setup lang="ts">
import PresetSettingsView from "./PresetSettingsView.vue";
</script>

<template>
  <PresetSettingsView
    title="音效預設"
    endpoint="/api/settings/sound-cues"
    value-key="activePresetKey"
    field-label="目前音效預設"
    :presets="[
      { key: 'arcade-stage', label: '街機舞台' },
      { key: 'soft-stage', label: '柔和舞台' },
      { key: 'retro-pixel', label: '復古像素' }
    ]"
  />
</template>
