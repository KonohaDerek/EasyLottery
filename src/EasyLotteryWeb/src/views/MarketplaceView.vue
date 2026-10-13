<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { ApiError } from "../api/client";
import {
  installMarketplaceTemplate,
  loadMarketplace,
  MarketplacePublicationError,
  marketplaceEditorPath,
  publishMarketplaceCopy,
  type MarketplaceEntry
} from "../marketplace/api";

const router = useRouter();
const entries = ref<MarketplaceEntry[]>([]);
const loading = ref(true);
const error = ref("");
const busyEntry = ref("");
const pendingCopy = ref<{ entry: MarketplaceEntry; id: number } | null>(null);
const installedKey = (entry: MarketplaceEntry) => `${entry.kind}:${entry.id}`;

async function reload(): Promise<void> {
  loading.value = true;
  error.value = "";
  try {
    entries.value = await loadMarketplace();
  } catch (cause) {
    error.value = cause instanceof ApiError ? cause.message : "無法載入市集，請稍後重試。";
  } finally {
    loading.value = false;
  }
}

async function install(entry: MarketplaceEntry): Promise<void> {
  if (pendingCopy.value) return;
  busyEntry.value = installedKey(entry);
  error.value = "";
  try {
    await router.push(await installMarketplaceTemplate(entry));
  } catch (cause) {
    if (cause instanceof MarketplacePublicationError) {
      pendingCopy.value = { entry, id: cause.copyId };
      const detail = cause.publicationError instanceof ApiError || cause.publicationError instanceof Error
        ? cause.publicationError.message
        : cause.message;
      error.value = `副本 #${cause.copyId} 已建立，但發布未完成（${detail}）。可重試發布，不必再建立副本。`;
    } else error.value = cause instanceof ApiError
      ? `安裝尚未完成：${cause.message}。來源與既有活動不會被覆寫。`
      : cause instanceof Error ? cause.message : "安裝尚未完成，來源與既有活動不會被覆寫。";
  } finally {
    busyEntry.value = "";
  }
}

async function retryPublish(): Promise<void> {
  const pending = pendingCopy.value;
  if (!pending || busyEntry.value) return;
  busyEntry.value = installedKey(pending.entry);
  try {
    const path = await publishMarketplaceCopy(pending.entry, pending.id);
    pendingCopy.value = null;
    error.value = "";
    await router.push(path);
  } catch (cause) {
    const detail = cause instanceof ApiError || cause instanceof Error ? cause.message : "請稍後重試。";
    error.value = `副本 #${pending.id} 仍未發布（${detail}）。可重試，或先開啟副本。`;
  } finally {
    busyEntry.value = "";
  }
}

function openPendingCopy(): void {
  const pending = pendingCopy.value;
  if (pending) void router.push(marketplaceEditorPath(pending.entry, pending.id));
}

onMounted(() => void reload());
</script>

<template>
  <main class="marketplace-page" aria-labelledby="marketplace-title">
    <header class="page-heading">
      <p class="eyebrow">ACTIVITY MARKET</p>
      <h1 id="marketplace-title">活動市集</h1>
      <p class="lead">挑選內建範本，建立可獨立編輯的新活動。重新安裝也會建立新版本，不會覆蓋原有活動。</p>
    </header>

    <v-alert v-if="error" type="error" variant="tonal" role="alert" class="mb-4">
      {{ error }}
      <template #append>
        <div v-if="pendingCopy" class="recovery-actions">
          <v-btn variant="text" :loading="busyEntry === installedKey(pendingCopy.entry)" @click="retryPublish">重試發布副本</v-btn>
          <v-btn variant="text" :disabled="!!busyEntry" @click="openPendingCopy">開啟副本</v-btn>
        </div>
        <v-btn v-else variant="text" @click="reload">重新載入</v-btn>
      </template>
    </v-alert>

    <div v-if="loading" class="state-card" role="status">載入市集範本…</div>
    <div v-else-if="!error && entries.length === 0" class="state-card" role="status">目前沒有可安裝的已發布內建範本。</div>
    <v-row v-else-if="entries.length" class="marketplace-grid">
      <v-col v-for="entry in entries" :key="installedKey(entry)" cols="12" sm="6" xl="4">
        <v-card class="market-card" height="100%" rounded="xl" elevation="2">
          <v-card-item>
            <template #prepend>
              <div class="market-card-mark" :class="entry.kind" aria-hidden="true">{{ entry.kind === "pokebox" ? "戳" : "轉" }}</div>
            </template>
            <v-card-title>{{ entry.name }}</v-card-title>
            <v-card-subtitle>{{ entry.kind === "pokebox" ? "戳戳樂範本" : "轉盤範本" }}</v-card-subtitle>
            <template #append>
              <v-chip :color="entry.installed ? 'success' : 'default'" size="small" variant="tonal">
                {{ entry.installed ? "已安裝" : "尚未安裝" }}
              </v-chip>
            </template>
          </v-card-item>
          <v-card-text class="market-card-description">{{ entry.description || "內建活動範本，可複製後自行調整。" }}</v-card-text>
          <v-card-actions class="market-card-actions">
            <v-btn
              color="primary"
              :aria-label="`${entry.name}：${entry.installed ? '建立最新版本副本' : '建立可編輯副本'}`"
              :loading="busyEntry === installedKey(entry) && !pendingCopy"
              :disabled="!!busyEntry || !!pendingCopy"
              @click="install(entry)"
            >
              {{ pendingCopy && installedKey(pendingCopy.entry) === installedKey(entry) ? "副本待發布" : entry.installed ? "建立最新版本副本" : "建立可編輯副本" }}
            </v-btn>
            <v-btn v-if="entry.installedId" variant="text" :to="`${entry.kind === 'pokebox' ? '/pokebox' : '/roulette'}?edit=${entry.installedId}`">
              開啟已安裝活動
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
  </main>
</template>

<style scoped>
.marketplace-page { max-width: 1200px; margin-inline: auto; }
.marketplace-grid { margin-top: 0.25rem; }
.market-card { border: 1px solid rgba(37, 99, 235, 0.1); background: linear-gradient(155deg, #fff 25%, #f5f7ff 100%); }
.market-card-mark { width: 2.75rem; height: 2.75rem; display: grid; place-items: center; border-radius: 1rem; color: white; font-size: 1.25rem; font-weight: 800; background: linear-gradient(145deg, #7c3aed, #ec4899); box-shadow: 0 8px 18px rgba(124, 58, 237, 0.25); }
.market-card-mark.roulette { background: linear-gradient(145deg, #2563eb, #14b8a6); box-shadow: 0 8px 18px rgba(37, 99, 235, 0.22); }
.market-card-description { min-height: 4.5rem; color: #58677d; }
.market-card-actions { padding: 0.25rem 1rem 1rem; flex-wrap: wrap; }
.recovery-actions { display: flex; flex-wrap: wrap; }
.state-card { padding: 1.25rem; color: #526179; background: rgba(255,255,255,.85); border: 1px solid #dbe4f0; border-radius: 1rem; }
</style>
