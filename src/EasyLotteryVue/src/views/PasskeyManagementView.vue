<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { ApiError } from '@/api'
import { useAuthStore, type PasskeyDevice } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const passkeys = ref<PasskeyDevice[]>([])
const newPasskeyName = ref('Admin Passkey')
const loading = ref(true)
const busy = ref(false)
const error = ref('')

function formatCreatedAt(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.valueOf())) return '時間未知'
  return new Intl.DateTimeFormat('zh-TW', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

async function loadPasskeys(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    passkeys.value = await auth.loadPasskeys()
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 401) {
      auth.refreshSession()
      await router.replace({ name: 'login', query: { redirect: '/system/access' } })
      return
    }
    error.value = cause instanceof Error ? cause.message : '無法載入 Passkey 清單。'
  } finally {
    loading.value = false
  }
}

async function addPasskey(): Promise<void> {
  const name = newPasskeyName.value.trim()
  if (!name) {
    error.value = '請輸入裝置名稱。'
    return
  }

  busy.value = true
  error.value = ''
  try {
    await auth.addPasskey(name)
    newPasskeyName.value = 'Admin Passkey'
    await loadPasskeys()
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : '無法新增 Passkey。'
  } finally {
    busy.value = false
  }
}

async function removePasskey(passkey: PasskeyDevice): Promise<void> {
  if (passkeys.value.length <= 1) return
  if (!window.confirm(`確定要移除「${passkey.name}」嗎？`)) return

  busy.value = true
  error.value = ''
  try {
    await auth.removePasskey(passkey.id)
    await loadPasskeys()
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : '無法移除 Passkey。'
  } finally {
    busy.value = false
  }
}

onMounted(loadPasskeys)
</script>

<template>
  <main class="page-content" aria-labelledby="passkey-title">
    <header>
      <p class="eyebrow">系統設定</p>
      <h1 id="passkey-title" class="page-title">管理員 Passkey</h1>
      <p class="page-lead">
        管理這個 EasyLottery 站台可用的登入裝置。伺服器只保存 Passkey 公開憑證資料，私密金鑰留在你的裝置或密碼管理器。
      </p>
    </header>

    <v-alert class="retention-note" type="info" variant="tonal" icon="mdi-information-outline">
      至少保留一個 Passkey，避免移除最後一個裝置後無法登入。
    </v-alert>

    <v-card class="content-card manage-card" variant="flat">
      <div class="card-heading">
        <div>
          <h2>新增 Passkey 裝置</h2>
          <p>為這台裝置設定一個容易辨認的名稱。</p>
        </div>
        <v-avatar color="primary" variant="tonal" size="46">
          <v-icon icon="mdi-key-plus" />
        </v-avatar>
      </div>

      <form class="add-form" @submit.prevent="addPasskey">
        <v-text-field
          id="passkey-name"
          v-model="newPasskeyName"
          label="裝置名稱"
          placeholder="例如：Office MacBook 或 iPhone"
          maxlength="80"
          autocomplete="off"
          variant="outlined"
          density="comfortable"
          hide-details="auto"
          :disabled="busy"
        />
        <v-btn
          color="primary"
          type="submit"
          prepend-icon="mdi-key-chain-variant"
          :loading="busy"
          :disabled="busy"
        >
          新增 Passkey
        </v-btn>
      </form>
    </v-card>

    <v-card class="content-card manage-card" variant="flat">
      <div class="card-heading">
        <div>
          <h2>已註冊的 Passkey</h2>
          <p>移除不再使用的裝置前，請先確認其他 Passkey 可以正常登入。</p>
        </div>
        <v-chip v-if="!loading" color="primary" variant="tonal">
          {{ passkeys.length }} 個裝置
        </v-chip>
      </div>

      <div v-if="loading" class="loading-row" role="status">
        <v-progress-circular indeterminate color="primary" size="22" />
        <span>正在載入裝置…</span>
      </div>
      <v-alert v-else-if="error" type="error" variant="tonal" role="alert">
        {{ error }}
      </v-alert>
      <p v-else-if="passkeys.length === 0" class="empty-state">目前沒有已註冊的 Passkey。</p>
      <v-list v-else class="passkey-list" lines="two" aria-label="已註冊的 Passkey 清單">
        <v-list-item
          v-for="passkey in passkeys"
          :key="passkey.id"
          :title="passkey.name"
          :subtitle="`建立於 ${formatCreatedAt(passkey.createdAtUtc)}`"
          prepend-icon="mdi-laptop-account"
        >
          <template #append>
            <v-btn
              color="error"
              variant="text"
              size="small"
              :disabled="busy || passkeys.length <= 1"
              :aria-label="`移除 ${passkey.name}`"
              @click="removePasskey(passkey)"
            >
              移除
            </v-btn>
          </template>
        </v-list-item>
      </v-list>
    </v-card>
  </main>
</template>

<style scoped>
.retention-note {
  margin-top: 1.75rem;
}

.manage-card {
  margin-top: 1.25rem;
}

.card-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.card-heading h2 {
  margin: 0;
  color: #214c3f;
  font-size: 1.17rem;
}

.card-heading p {
  margin: 0.45rem 0 0;
  color: #61756c;
  line-height: 1.55;
}

.add-form {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
  gap: 0.85rem;
  margin-top: 1.4rem;
}

.loading-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1.5rem 0 0.25rem;
  color: #61756c;
}

.empty-state {
  margin: 1.4rem 0 0;
  color: #61756c;
}

.passkey-list {
  margin-top: 1.1rem;
  padding: 0;
}

@media (max-width: 640px) {
  .add-form {
    grid-template-columns: 1fr;
  }

  .add-form :deep(.v-btn) {
    width: 100%;
  }
}
</style>
