<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ApiError, apiRequest } from "../api/client";
import { buildPokeTestUrl, type PokeTemplate } from "../pokebox/template";

const emptyGuid = "00000000-0000-0000-0000-000000000000";
const route = useRoute();
const router = useRouter();
const loading = ref(true);
const error = ref("");

async function openPreview() {
  loading.value = true;
  error.value = "";
  const id = Number(route.params.id);
  if (!Number.isSafeInteger(id) || id <= 0) {
    await router.replace("/pokebox");
    return;
  }

  try {
    let template: PokeTemplate;
    try {
      const response = await apiRequest<PokeTemplate>(`/api/poke-templates/${id}`);
      template = response.data;
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 404) {
        await router.replace("/pokebox");
        return;
      }
      throw cause;
    }

    if (!template || !template.publicId || template.publicId.toLowerCase() === emptyGuid) {
      await router.replace("/pokebox");
      return;
    }

    const session = await apiRequest<{ token: string }>("/api/obs-sessions", {
      method: "POST",
      body: JSON.stringify({
        resourceKind: "pokebox",
        resourceId: template.publicId,
        scopes: ["read", "control"]
      })
    });
    if (!session.data.token) throw new Error("OBS 工作階段未回傳 token。");

    window.location.replace(buildPokeTestUrl(window.location.origin, template.publicId, session.data.token));
  } catch (cause) {
    loading.value = false;
    error.value = cause instanceof Error ? cause.message : "無法開啟戳戳樂 OBS 預覽。";
  }
}

onMounted(() => { void openPreview(); });
</script>

<template>
  <main class="preview-relay" aria-labelledby="preview-title">
    <h1 id="preview-title">戳戳樂 OBS 預覽</h1>
    <p v-if="loading" role="status">正在開啟共用 OBS 預覽…</p>
    <div v-else class="preview-error">
      <p role="alert">無法開啟戳戳樂 OBS 預覽：{{ error }}</p>
      <button class="secondary-button" type="button" @click="openPreview">重新嘗試</button>
      <RouterLink to="/pokebox">返回戳戳樂模板</RouterLink>
    </div>
  </main>
</template>

<style scoped>
.preview-relay { display: grid; gap: .75rem; max-width: 36rem; margin: 2rem auto; text-align: center; }
.preview-relay h1, .preview-relay p { margin: 0; }
.preview-error { display: grid; justify-items: center; gap: .75rem; }
</style>
