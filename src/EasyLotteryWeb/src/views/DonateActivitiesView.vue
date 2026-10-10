<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from "vue";
import { apiRequest } from "../api/client";
import ObsAssetUrlPicker from "../components/ObsAssetUrlPicker.vue";
import {
  cloneDonateActivity,
  buildDonateObsTestUrl,
  createDonateActivity,
  createDonatePrize,
  formatLocalDateTime,
  localDateTimeToUtc,
  prizeProbabilityTotal,
  toLocalDateTimeInput,
  type DonateActivity,
  type DonatePrize
} from "../donate-activities/activity";

const activities = ref<DonateActivity[]>([]);
const editorDialog = ref<HTMLDialogElement | null>(null);
const editing = ref<DonateActivity | null>(null);
const startDateInput = ref("");
const endDateInput = ref("");
const pageState = ref<"loading" | "error" | "loaded">("loading");
const error = ref("");
const success = ref("");
const saving = ref(false);
const deletingId = ref<number | null>(null);

const probabilityTotal = computed(() => editing.value ? prizeProbabilityTotal(editing.value.prizes) : 0);
const probabilityExceeded = computed(() => probabilityTotal.value > 100);
const editorTitle = computed(() => !editing.value
  ? "Donate 活動"
  : editing.value.id === 0 ? "新增 Donate 活動" : `編輯 Donate 活動：${editing.value.name}`);

async function loadActivities() {
  pageState.value = "loading";
  error.value = "";
  try {
    const response = await apiRequest<DonateActivity[]>("/api/donate-activities");
    activities.value = response.data;
    pageState.value = "loaded";
    return true;
  } catch (cause) {
    pageState.value = "error";
    error.value = cause instanceof Error ? cause.message : "Donate 活動載入失敗。";
    return false;
  }
}

async function showEditor(activity: DonateActivity) {
  editing.value = activity;
  startDateInput.value = toLocalDateTimeInput(activity.startsAtUtc);
  endDateInput.value = toLocalDateTimeInput(activity.endsAtUtc);
  await nextTick();
  if (editorDialog.value && !editorDialog.value.open) editorDialog.value.showModal();
}

function create() {
  void showEditor(createDonateActivity());
}

function edit(activity: DonateActivity) {
  void showEditor(cloneDonateActivity(activity));
}

function handleEditorClose() {
  editing.value = null;
}

function closeEditor() {
  if (editorDialog.value?.open) editorDialog.value.close();
  else editing.value = null;
}

function addPrize() {
  editing.value?.prizes.push(createDonatePrize());
}

function removePrize(prize: DonatePrize) {
  if (editing.value) editing.value.prizes = editing.value.prizes.filter(item => item !== prize);
}

async function save() {
  if (!editing.value || saving.value || probabilityExceeded.value) return;
  error.value = "";
  success.value = "";
  saving.value = true;
  try {
    const activity = {
      ...editing.value,
      startsAtUtc: localDateTimeToUtc(startDateInput.value),
      endsAtUtc: localDateTimeToUtc(endDateInput.value),
      prizes: editing.value.prizes.map(prize => ({ ...prize }))
    };
    const isNew = activity.id <= 0;
    await apiRequest<DonateActivity>(isNew ? "/api/donate-activities" : `/api/donate-activities/${activity.id}`, {
      method: isNew ? "POST" : "PUT",
      body: JSON.stringify(activity)
    });
    closeEditor();
    const reloaded = await loadActivities();
    if (reloaded) success.value = "Donate 活動已儲存。";
    else error.value = `活動已儲存，但清單重新載入失敗：${error.value}`;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "Donate 活動儲存失敗。";
  } finally {
    saving.value = false;
  }
}

async function remove(activity: DonateActivity) {
  const confirmed = window.confirm(`確定要刪除「${activity.name}」嗎？活動設定與獎項會被移除，但既有抽獎紀錄會保留。`);
  if (!confirmed) return;
  error.value = "";
  success.value = "";
  deletingId.value = activity.id;
  try {
    await apiRequest<null>(`/api/donate-activities/${activity.id}`, { method: "DELETE" });
    const reloaded = await loadActivities();
    if (reloaded) success.value = "Donate 活動已刪除，既有抽獎紀錄會保留。";
    else error.value = `活動已刪除，但清單重新載入失敗：${error.value}`;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "Donate 活動刪除失敗。";
  } finally {
    deletingId.value = null;
  }
}

async function openTestObs(activity: DonateActivity) {
  const popup = window.open("about:blank", "_blank");
  if (!popup) {
    error.value = "瀏覽器封鎖了新分頁，請允許此網站開啟測試 OBS 後重試。";
    return;
  }
  popup.opener = null;
  error.value = "";
  try {
    const response = await apiRequest<{ token: string }>("/api/obs-sessions", {
      method: "POST",
      body: JSON.stringify({ resourceKind: "donate", resourceId: activity.publicId, scopes: ["read", "control"] })
    });
    popup.location.href = buildDonateObsTestUrl(window.location.origin, activity.publicId, response.data.token);
  } catch (cause) {
    popup.close();
    error.value = cause instanceof Error ? cause.message : "無法建立 OBS 測試工作階段。";
  }
}

function inventory(activity: DonateActivity, remaining: boolean) {
  return activity.prizes.reduce((total, prize) => total + (remaining ? prize.remainingQuantity : prize.quantity), 0);
}

onMounted(() => { void loadActivities(); });
</script>

<template>
  <main class="donate-page" aria-labelledby="donate-title">
    <header class="page-heading donate-heading">
      <div>
        <p class="eyebrow">DONATE DRAW</p>
        <h1 id="donate-title">Donate 活動</h1>
        <p class="lead">贊助金額每達到最低門檻即可獲得一次抽獎機會。獎項用盡後不會重複抽出。</p>
      </div>
      <div class="heading-actions">
        <a class="legacy-link" href="/legacy/donate-activities">開啟舊版管理頁</a>
        <button class="submit-button compact" type="button" @click="create">新增活動</button>
      </div>
    </header>

    <p v-if="error" class="login-error" role="alert">{{ error }}</p>
    <p v-if="success" class="success-banner" role="status">{{ success }}</p>

    <section class="settings-card activity-card" aria-labelledby="activities-heading">
      <h2 id="activities-heading">活動清單</h2>
      <p v-if="pageState === 'loading'" class="status-text" role="status">載入活動中…</p>
      <div v-else-if="pageState === 'error'" class="load-error">
        <p>無法載入活動清單。請確認管理員工作階段仍有效，或稍後再試。</p>
        <button class="secondary-button" type="button" @click="loadActivities">重新載入</button>
      </div>
      <p v-else-if="activities.length === 0" class="status-text">目前沒有 Donate 活動，請新增活動開始設定。</p>
      <div v-else class="table-wrap">
        <table aria-label="Donate 活動清單">
          <thead><tr><th>活動</th><th>期間（本地時間）</th><th>最低贊助</th><th>獎項庫存</th><th>狀態</th><th>操作</th></tr></thead>
          <tbody>
            <tr v-for="activity in activities" :key="activity.id" class="activity-row">
              <td>{{ activity.name || "（未命名）" }}</td>
              <td>{{ formatLocalDateTime(activity.startsAtUtc) }} — {{ formatLocalDateTime(activity.endsAtUtc) }}</td>
              <td>NT$ {{ activity.minimumDonationAmount }}</td>
              <td>{{ inventory(activity, true) }} / {{ inventory(activity, false) }}</td>
              <td><span :class="activity.isEnabled ? 'state-enabled' : 'state-disabled'">{{ activity.isEnabled ? "啟用" : "停用" }}</span></td>
              <td class="row-actions">
                <button class="secondary-button" type="button" @click="edit(activity)">編輯</button>
                <button class="secondary-button" type="button" :disabled="!activity.isEnabled" @click="openTestObs(activity)">開啟測試 OBS</button>
                <button class="danger-button" type="button" :disabled="deletingId === activity.id" @click="remove(activity)">{{ deletingId === activity.id ? "刪除中…" : "刪除" }}</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <dialog ref="editorDialog" class="editor-dialog" :aria-label="editorTitle" @close="handleEditorClose">
      <form v-if="editing" class="editor-form" @submit.prevent="save">
        <header class="dialog-heading">
          <h2>{{ editorTitle }}</h2>
          <button class="icon-button" type="button" aria-label="關閉編輯器" @click="closeEditor">×</button>
        </header>
        <div class="editor-content">
          <div class="editor-grid">
            <label class="field span-2">活動名稱
              <input v-model="editing.name" class="settings-input" type="text" required maxlength="120" />
            </label>
            <label class="field">最低贊助金額
              <input v-model.number="editing.minimumDonationAmount" class="settings-input" type="number" min="1" step="0.01" required />
            </label>
            <label class="field">抽獎動畫
              <select v-model.number="editing.animation" class="settings-input">
                <option :value="0">一番賞</option><option :value="1">扭蛋</option><option :value="2">日式滾筒</option><option :value="3">塞錢箱／詩籤</option><option :value="4">刮刮樂</option>
              </select>
            </label>
            <label class="field">結果模板
              <select v-model="editing.polaroidTemplateKey" class="settings-input"><option value="classic">經典</option><option value="celebration">慶典</option></select>
            </label>
            <label class="field">結果顯示秒數
              <input v-model.number="editing.resultDisplayDurationSeconds" class="settings-input" type="number" min="3" max="300" required />
              <small>3–300 秒；到期後 OBS 會回到透明待機。</small>
            </label>
            <label class="field">抽獎動畫秒數
              <input v-model.number="editing.animationDurationSeconds" class="settings-input" type="number" min="3" max="30" required />
              <small>依設定顯示通知後播放動畫再揭曉。</small>
            </label>
            <label class="check-field"><input v-model="editing.useAiCongratulation" type="checkbox" /> AI 恭喜文案</label>
            <label class="check-field"><input v-model="editing.showDonateInformation" type="checkbox" /> 顯示 Donate 資訊</label>
            <label class="check-field"><input v-model="editing.isEnabled" type="checkbox" /> 啟用活動</label>
            <label class="check-field"><input v-model="editing.useWebmAnimation" type="checkbox" /> 使用 WebM 動畫</label>
          </div>
          <p class="field-hint">關閉 Donate 資訊時會直接播放抽獎動畫。WebM 載入失敗時會自動使用 CSS 動畫。</p>

          <div v-if="editing.useWebmAnimation" class="editor-grid media-fields">
            <label class="field span-2">WebM 動畫 URL
              <ObsAssetUrlPicker v-model="editing.webmAnimationUrl" :kind="4" label="WebM 動畫 URL" placeholder="WebM URL 或本機資產" />
              <small>建議使用透明背景的 video/webm；影片長度應與動畫秒數一致。</small>
            </label>
            <label class="field span-2">WebM Poster URL（選填）
              <ObsAssetUrlPicker v-model="editing.webmPosterUrl" :kind="0" label="WebM Poster URL" placeholder="Poster 圖片 URL" />
            </label>
            <label class="check-field"><input v-model="editing.webmAnimationLoop" type="checkbox" /> 循環播放（循環影片會依動畫秒數結束）</label>
          </div>

          <div class="editor-grid date-fields">
            <label class="field">開始時間（本地時區）<input v-model="startDateInput" class="settings-input" type="datetime-local" required /></label>
            <label class="field">結束時間（本地時區）<input v-model="endDateInput" class="settings-input" type="datetime-local" required /></label>
          </div>

          <section class="prizes-section" aria-labelledby="prizes-heading">
            <h3 id="prizes-heading">獎項</h3>
            <p class="field-hint">每個獎項設定 0–100% 的中獎率；未配置的剩餘機率固定為「銘謝惠顧」。合計不可超過 100%。</p>
            <div v-for="(prize, index) in editing.prizes" :key="prize.id || `new-${index}`" class="prize-editor">
              <label class="field">獎項名稱<input v-model="prize.name" class="settings-input" type="text" maxlength="120" /></label>
              <label class="field">中獎機率 (%)<input v-model.number="prize.probability" class="settings-input" type="number" min="0" max="100" step="0.01" /></label>
              <label class="field">圖片 URL<ObsAssetUrlPicker v-model="prize.imageUrl" :kind="0" label="獎項圖片 URL" placeholder="圖片 URL" /></label>
              <label class="field">數量<input v-model.number="prize.quantity" class="settings-input" type="number" min="0" step="1" /></label>
              <label class="check-field grand-prize"><input v-model="prize.isGrandPrize" type="checkbox" /> 最大獎</label>
              <button class="danger-button" type="button" @click="removePrize(prize)">移除</button>
            </div>
            <p :class="probabilityExceeded ? 'probability-error' : 'probability-info'" role="status">
              目前獎項機率合計：{{ probabilityTotal.toFixed(2) }}%　／　銘謝惠顧：{{ Math.max(0, 100 - probabilityTotal).toFixed(2) }}%
              <strong v-if="probabilityExceeded">機率總和不可超過 100%，請調整獎項機率後再儲存。</strong>
            </p>
            <button class="secondary-button" type="button" @click="addPrize">新增獎項</button>
          </section>
          <p v-if="error" class="login-error" role="alert">{{ error }}</p>
        </div>
        <footer class="editor-footer">
          <button class="submit-button compact" type="submit" :disabled="saving || probabilityExceeded">{{ saving ? "儲存中…" : "儲存" }}</button>
          <button class="secondary-button" type="button" :disabled="saving" @click="closeEditor">取消</button>
        </footer>
      </form>
    </dialog>
  </main>
</template>

<style scoped>
.donate-page { display: grid; gap: 1.25rem; color: #172033; }
.donate-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 1rem; }
.donate-heading .lead { max-width: 46rem; }
.heading-actions, .row-actions, .editor-footer { display: flex; flex-wrap: wrap; align-items: center; gap: .55rem; }
.legacy-link { color: #1d4ed8; font-size: .9rem; }
.activity-card { padding: 1.1rem 1.25rem; border-radius: .65rem; background: #fff; }
.activity-card h2 { margin: 0 0 .8rem; color: #1e293b; font-size: 1.2rem; }
.activity-row td { vertical-align: middle; }
.row-actions { min-width: 16rem; }
.state-enabled, .state-disabled { display: inline-block; padding: .15rem .5rem; border-radius: 1rem; font-size: .85rem; }
.state-enabled { color: #166534; background: #dcfce7; }
.state-disabled { color: #475569; background: #e2e8f0; }
.success-banner { margin: 0; padding: .75rem 1rem; color: #166534; background: #dcfce7; border-radius: .4rem; }
.load-error { display: flex; align-items: center; flex-wrap: wrap; gap: .8rem; color: #7f1d1d; }
.load-error p { margin: 0; }
.secondary-button { min-height: 2.65rem; padding: .5rem .75rem; color: #1e40af; font: inherit; background: #eff6ff; border: 1px solid #bfdbfe; border-radius: .35rem; cursor: pointer; }
.secondary-button:disabled, .danger-button:disabled, .submit-button:disabled { cursor: not-allowed; opacity: .55; }
.editor-dialog { width: min(1080px, calc(100vw - 1rem)); max-height: calc(100vh - 1rem); padding: 0; overflow: hidden; border: 0; border-radius: .8rem; box-shadow: 0 20px 60px #0f172a55; }
.editor-dialog::backdrop { background: #0f172a88; }
.editor-form { display: grid; grid-template-rows: auto minmax(0, 1fr) auto; max-height: calc(100vh - 1rem); }
.dialog-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 1rem 1.25rem; border-bottom: 1px solid #e5eaf2; }
.dialog-heading h2 { margin: 0; font-size: 1.25rem; }
.icon-button { font: inherit; font-size: 1.5rem; border: 0; background: transparent; cursor: pointer; }
.editor-content { padding: 1.25rem; overflow-y: auto; }
.editor-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); align-items: start; gap: .9rem 1rem; }
.field { display: grid; gap: .35rem; color: #334155; font-weight: 600; }
.field small, .field-hint { color: #64748b; font-size: .86rem; font-weight: 400; line-height: 1.5; }
.span-2 { grid-column: span 2; }
.check-field { display: flex; gap: .5rem; align-items: center; min-height: 2.75rem; color: #334155; }
.settings-input { box-sizing: border-box; width: 100%; min-height: 2.75rem; padding: .65rem .75rem; color: #1f2a37; font: inherit; background: #fff; border: 1px solid #b8c4d6; border-radius: .35rem; }
.field-hint { margin: .5rem 0 1rem; }
.media-fields { margin-top: 1rem; padding: 1rem; background: #f8fafc; border-radius: .5rem; }
.date-fields { margin-top: 1rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.prizes-section { margin-top: 1.5rem; }
.prizes-section h3 { margin-bottom: .25rem; }
.prize-editor { display: grid; grid-template-columns: 1.3fr .9fr 1.4fr .65fr .8fr auto; gap: .65rem; align-items: end; padding: .8rem 0; border-bottom: 1px solid #e5eaf2; }
.grand-prize { justify-content: center; }
.probability-info, .probability-error { margin: 1rem 0; padding: .65rem .8rem; border-radius: .35rem; }
.probability-info { color: #1e40af; background: #eff6ff; }
.probability-error { color: #991b1b; background: #fef2f2; }
.probability-error strong { display: block; margin-top: .25rem; }
.editor-footer { justify-content: flex-end; padding: .9rem 1.25rem; border-top: 1px solid #e5eaf2; }
@media (max-width: 900px) { .editor-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } .prize-editor { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 700px) { .donate-heading { align-items: flex-start; flex-direction: column; } .heading-actions { align-items: flex-start; flex-direction: column; } .editor-grid, .date-fields { grid-template-columns: 1fr; } .span-2 { grid-column: auto; } .prize-editor { grid-template-columns: 1fr; } .grand-prize { justify-content: flex-start; } }
</style>
