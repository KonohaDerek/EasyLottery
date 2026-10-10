<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from "vue";
import { apiRequest, apiText } from "../api/client";
import ObsAssetUrlPicker from "../components/ObsAssetUrlPicker.vue";
import {
  buildRouletteTestUrl,
  cloneRouletteTemplate,
  createRouletteTemplate,
  resizeRouletteSegments,
  type RouletteTemplate
} from "../roulette/template";

const templates = ref<RouletteTemplate[]>([]);
const pageState = ref<"loading" | "error" | "loaded">("loading");
const editorDialog = ref<HTMLDialogElement | null>(null);
const editing = ref<RouletteTemplate | null>(null);
const importJson = ref("");
const exportJson = ref("");
const error = ref("");
const success = ref("");
const saving = ref(false);
const busyAction = ref("");

const editorTitle = computed(() => !editing.value
  ? "轉盤模板"
  : editing.value.id === 0 ? "新增轉盤模板" : `編輯轉盤模板：${editing.value.name}`);
const probabilityTotal = computed(() => editing.value?.segments.reduce((total, segment) => total + Number(segment.probability || 0), 0) ?? 0);

async function loadTemplates() {
  pageState.value = "loading";
  error.value = "";
  try {
    const response = await apiRequest<RouletteTemplate[]>("/api/roulette-templates");
    templates.value = response.data;
    pageState.value = "loaded";
    return true;
  } catch (cause) {
    pageState.value = "error";
    error.value = cause instanceof Error ? cause.message : "轉盤模板載入失敗。";
    return false;
  }
}

async function showEditor(template: RouletteTemplate) {
  editing.value = cloneRouletteTemplate(template);
  await nextTick();
  if (editorDialog.value && !editorDialog.value.open) editorDialog.value.showModal();
}

function create() {
  void showEditor(createRouletteTemplate());
}

function edit(template: RouletteTemplate) {
  void showEditor(template);
}

function closeEditor() {
  if (editorDialog.value?.open) editorDialog.value.close();
  else editing.value = null;
}

function handleEditorClose() {
  editing.value = null;
}

function applySegmentCount() {
  if (editing.value) editing.value = resizeRouletteSegments(editing.value);
}

async function save() {
  if (!editing.value || saving.value) return;
  const template = resizeRouletteSegments(editing.value);
  if (!template.name.trim()) {
    error.value = "請輸入模板名稱。";
    return;
  }
  editing.value = template;
  error.value = "";
  success.value = "";
  saving.value = true;
  try {
    const isNew = template.id === 0;
    await apiRequest<RouletteTemplate>(isNew ? "/api/roulette-templates" : `/api/roulette-templates/${template.id}`, {
      method: isNew ? "POST" : "PUT",
      body: JSON.stringify(template)
    });
    closeEditor();
    if (await loadTemplates()) success.value = "轉盤模板已儲存。";
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "轉盤模板儲存失敗。";
  } finally {
    saving.value = false;
  }
}

async function remove(template: RouletteTemplate) {
  if (template.isBuiltIn || !window.confirm(`確定要刪除「${template.name}」嗎？`)) return;
  error.value = "";
  success.value = "";
  busyAction.value = `delete-${template.id}`;
  try {
    await apiRequest<null>(`/api/roulette-templates/${template.id}`, { method: "DELETE" });
    if (await loadTemplates()) success.value = "轉盤模板已刪除。";
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "轉盤模板刪除失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function duplicate(template: RouletteTemplate) {
  error.value = "";
  success.value = "";
  busyAction.value = `duplicate-${template.id}`;
  try {
    const response = await apiRequest<RouletteTemplate>(`/api/roulette-templates/${template.id}/duplicate`, { method: "POST" });
    if (await loadTemplates()) {
      success.value = `已建立複本「${response.data.name}」。`;
      edit(response.data);
    }
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "轉盤模板複製失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function seedDefaults() {
  error.value = "";
  success.value = "";
  busyAction.value = "seed";
  try {
    await apiRequest<null>("/api/roulette-templates/seed-defaults", { method: "POST" });
    if (await loadTemplates()) success.value = "已載入預設模板。";
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "載入預設模板失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function exportTemplate(template: RouletteTemplate) {
  error.value = "";
  busyAction.value = `export-${template.id}`;
  try {
    const response = await apiText(`/api/roulette-templates/${template.id}/export`);
    exportJson.value = response.data;
    success.value = `已匯出「${template.name}」模板。`;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "轉盤模板匯出失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function importTemplate() {
  if (!importJson.value.trim()) {
    error.value = "請貼上 JSON 內容。";
    return;
  }
  error.value = "";
  success.value = "";
  busyAction.value = "import";
  try {
    await apiRequest<RouletteTemplate>("/api/roulette-templates/import", { method: "POST", body: importJson.value });
    importJson.value = "";
    if (await loadTemplates()) success.value = "模板匯入成功。";
  } catch (cause) {
    error.value = cause instanceof Error ? `匯入失敗：${cause.message}` : "轉盤模板匯入失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function openTestObs(template: RouletteTemplate) {
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
      body: JSON.stringify({ resourceKind: "roulette", resourceId: template.publicId, scopes: ["read", "control"] })
    });
    popup.location.href = buildRouletteTestUrl(window.location.origin, template.publicId, response.data.token);
  } catch (cause) {
    popup.close();
    error.value = cause instanceof Error ? cause.message : "無法建立 OBS 測試工作階段。";
  }
}

function statusLabel(template: RouletteTemplate) {
  return template.publicationStatus === 0 ? "啟用" : "草稿";
}

onMounted(() => { void loadTemplates(); });
</script>

<template>
  <main class="roulette-page" aria-labelledby="roulette-title">
    <header class="page-heading roulette-heading">
      <div>
        <p class="eyebrow">ROULETTE TEMPLATES</p>
        <h1 id="roulette-title">轉盤模板</h1>
        <p class="lead">建立轉盤項目、機率與旋轉效果；儲存後可開啟測試 OBS。</p>
      </div>
      <div class="heading-actions">
        <a class="legacy-link" href="/legacy/roulette">開啟舊版管理頁</a>
        <button class="secondary-button" type="button" :disabled="busyAction === 'seed'" @click="seedDefaults">載入預設模板</button>
        <button class="submit-button compact" type="button" @click="create">新增模板</button>
      </div>
    </header>

    <p v-if="error" class="login-error" role="alert">{{ error }}</p>
    <p v-if="success" class="success-banner" role="status">{{ success }}</p>

    <section class="settings-card" aria-labelledby="roulette-list-title">
      <h2 id="roulette-list-title">模板清單</h2>
      <p v-if="pageState === 'loading'" class="status-text" role="status">載入模板中…</p>
      <div v-else-if="pageState === 'error'" class="load-error">
        <p>無法載入轉盤模板。請確認管理員工作階段仍有效，或稍後再試。</p>
        <button class="secondary-button" type="button" @click="loadTemplates">重新載入</button>
      </div>
      <p v-else-if="templates.length === 0" class="status-text">尚無模板，請新增模板或載入預設模板。</p>
      <div v-else class="table-wrap">
        <table aria-label="轉盤模板清單">
          <thead><tr><th>模板</th><th>格數</th><th>旋轉時間</th><th>結果停留</th><th>緩動曲線</th><th>狀態</th><th>操作</th></tr></thead>
          <tbody>
            <tr v-for="template in templates" :key="template.id">
              <td><strong>{{ template.name || "（未命名）" }}</strong><small class="template-description">{{ template.description }}</small></td>
              <td>{{ template.segmentCount }}</td>
              <td>{{ template.spinDurationSec }} 秒</td>
              <td>{{ template.resultDisplayDurationSeconds }} 秒</td>
              <td>{{ template.easingFunction }}</td>
              <td><span :class="template.publicationStatus === 0 ? 'state-enabled' : 'state-disabled'">{{ statusLabel(template) }}</span></td>
              <td class="row-actions">
                <button class="secondary-button" type="button" @click="edit(template)">編輯</button>
                <button class="secondary-button" type="button" :disabled="template.publicationStatus !== 0 || busyAction !== ''" @click="openTestObs(template)">開啟測試 OBS</button>
                <button class="secondary-button" type="button" :disabled="busyAction !== ''" @click="duplicate(template)">複製</button>
                <button class="secondary-button" type="button" :disabled="busyAction !== ''" @click="exportTemplate(template)">匯出</button>
                <button v-if="!template.isBuiltIn" class="danger-button" type="button" :disabled="busyAction !== ''" @click="remove(template)">刪除</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <details class="settings-card import-export">
      <summary>進階：匯入／匯出模板</summary>
      <div class="import-export-grid">
        <label class="field">匯入 JSON
          <textarea v-model="importJson" class="settings-input" rows="8" placeholder="貼上 JSON 模板內容…"></textarea>
          <button class="submit-button compact" type="button" :disabled="busyAction !== ''" @click="importTemplate">匯入</button>
        </label>
        <label class="field">匯出結果
          <textarea v-model="exportJson" class="settings-input" rows="8" readonly placeholder="按下列表中的匯出後顯示於此"></textarea>
        </label>
      </div>
    </details>

    <dialog ref="editorDialog" class="roulette-dialog" :aria-label="editorTitle" @close="handleEditorClose">
      <form v-if="editing" class="roulette-editor" @submit.prevent="save">
        <header class="dialog-heading">
          <h2>{{ editorTitle }}</h2>
          <button class="icon-button" type="button" aria-label="關閉編輯器" @click="closeEditor">×</button>
        </header>
        <div class="roulette-editor-content">
          <div class="roulette-fields">
            <label class="field">模板名稱
              <input v-model="editing.name" class="settings-input" type="text" required maxlength="120" />
            </label>
            <label class="field">說明
              <input v-model="editing.description" class="settings-input" type="text" />
            </label>
            <label class="field">格數
              <select v-model.number="editing.segmentCount" class="settings-input">
                <option :value="6">6</option><option :value="8">8</option><option :value="12">12</option><option :value="24">24</option>
              </select>
            </label>
            <div class="field field-action"><span aria-hidden="true">&nbsp;</span><button class="secondary-button" type="button" @click="applySegmentCount">套用格數</button></div>
            <label class="field">旋轉時間（秒）
              <input v-model.number="editing.spinDurationSec" class="settings-input" type="number" min="1" max="30" step="0.1" />
            </label>
            <label class="field">結果停留秒數
              <input v-model.number="editing.resultDisplayDurationSeconds" class="settings-input" type="number" min="1" max="300" />
            </label>
            <label class="field">緩動曲線
              <select v-model="editing.easingFunction" class="settings-input">
                <option value="ease-out-cubic">ease-out-cubic</option><option value="ease-out-quad">ease-out-quad</option><option value="ease-out-quart">ease-out-quart</option><option value="ease-out-expo">ease-out-expo</option><option value="linear">linear</option>
              </select>
            </label>
            <label class="field">初始角度
              <input v-model.number="editing.initialAngleDeg" class="settings-input" type="number" min="0" max="359" />
            </label>
            <label class="field field-checkbox">啟用
              <input v-model.number="editing.publicationStatus" type="checkbox" :true-value="0" :false-value="1" />
            </label>
            <label class="field">中心圖片 URL
              <ObsAssetUrlPicker v-model="editing.centerImageUrl" :kind="0" label="中心圖片 URL" placeholder="輸入圖片網址或從資產庫選擇" />
            </label>
            <label class="field">背景圖片 URL
              <ObsAssetUrlPicker v-model="editing.backgroundImageUrl" :kind="0" label="背景圖片 URL" placeholder="輸入圖片網址或從資產庫選擇" />
            </label>
            <label class="field">指針圖片 URL
              <ObsAssetUrlPicker v-model="editing.pointerImageUrl" :kind="0" label="指針圖片 URL" placeholder="輸入圖片網址或從資產庫選擇" />
            </label>
            <label class="field">轉動音效 URL
              <ObsAssetUrlPicker v-model="editing.spinSoundUrl" :kind="1" label="轉動音效 URL" placeholder="輸入音訊網址或從資產庫選擇" />
            </label>
            <label class="field">中獎音效 URL
              <ObsAssetUrlPicker v-model="editing.winSoundUrl" :kind="1" label="中獎音效 URL" placeholder="輸入音訊網址或從資產庫選擇" />
            </label>
          </div>

          <section class="segments-section" aria-labelledby="segments-heading">
            <div class="segments-heading">
              <div><h3 id="segments-heading">轉盤項目</h3><p>共 {{ editing.segments.length }} 格；機率全為 0 時採等比分配。</p></div>
              <span role="status">{{ probabilityTotal > 0 ? `目前機率權重合計：${probabilityTotal.toFixed(2).replace(/\.00$/, "")}%` : "目前使用等比分配。" }}</span>
            </div>
            <div class="segment-grid">
              <fieldset v-for="segment in editing.segments" :key="segment.id || segment.index" class="segment-card" :style="{ borderLeftColor: segment.color }">
                <legend>格 {{ segment.index + 1 }}</legend>
                <label class="field">標題
                  <input v-model="segment.title" class="settings-input" type="text" />
                </label>
                <label class="field">中獎機率 (%)
                  <input v-model.number="segment.probability" class="settings-input" type="number" min="0" max="100" step="0.01" />
                </label>
                <label class="field">圖片 URL
                  <ObsAssetUrlPicker v-model="segment.imageUrl" :kind="0" :label="`第 ${segment.index + 1} 格圖片 URL`" placeholder="輸入圖片網址或從資產庫選擇" />
                </label>
                <label class="field">顏色
                  <input v-model="segment.color" class="settings-input color-input" type="color" />
                </label>
              </fieldset>
            </div>
          </section>
        </div>
        <footer class="dialog-footer">
          <button class="submit-button compact" type="submit" :disabled="saving">{{ saving ? "儲存中…" : "儲存" }}</button>
          <button class="secondary-button" type="button" @click="closeEditor">取消</button>
        </footer>
      </form>
    </dialog>
  </main>
</template>

<style scoped>
.roulette-page { display: grid; gap: 1rem; padding-bottom: 2rem; }
.roulette-heading { align-items: flex-start; }
.heading-actions, .row-actions { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; }
.table-wrap { overflow-x: auto; }
.table-wrap table { width: 100%; border-collapse: collapse; }
.table-wrap th, .table-wrap td { padding: .7rem; border-bottom: 1px solid #e5eaf2; text-align: left; vertical-align: middle; }
.template-description { display: block; color: #64748b; margin-top: .2rem; }
.import-export summary { cursor: pointer; font-weight: 600; }
.import-export-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; padding-top: 1rem; }
.field { display: grid; gap: .35rem; min-width: 0; }
.field-action { align-content: end; }
.field-checkbox { display: flex; align-items: center; gap: .6rem; }
.field-checkbox input { width: 1.1rem; height: 1.1rem; }
.roulette-dialog { width: min(75vw, 1400px); max-width: 75vw; max-height: 90vh; padding: 0; border: 0; border-radius: .8rem; box-shadow: 0 20px 60px #0f172a55; }
.roulette-dialog::backdrop { background: #0f172a88; }
.roulette-editor { display: grid; grid-template-rows: auto minmax(0, 1fr) auto; max-height: 90vh; }
.dialog-heading, .dialog-footer { display: flex; align-items: center; justify-content: space-between; gap: .75rem; padding: 1rem 1.25rem; border-bottom: 1px solid #e5eaf2; }
.dialog-heading h2 { margin: 0; font-size: 1.2rem; }
.dialog-footer { justify-content: flex-end; border-top: 1px solid #e5eaf2; border-bottom: 0; }
.icon-button { border: 0; background: transparent; font: inherit; font-size: 1.5rem; cursor: pointer; }
.roulette-editor-content { overflow: auto; padding: 1.25rem; }
.roulette-fields { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1rem; }
.segments-section { margin-top: 1.5rem; }
.segments-heading { display: flex; justify-content: space-between; align-items: center; gap: 1rem; margin-bottom: .75rem; }
.segments-heading h3, .segments-heading p { margin: 0 0 .25rem; }
.segments-heading span { color: #475569; }
.segment-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .85rem; }
.segment-card { display: grid; grid-template-columns: 1fr 1fr; gap: .65rem; min-width: 0; padding: .8rem; border: 1px solid #dbe3ef; border-left: 6px solid #ccc; border-radius: .55rem; }
.segment-card legend { padding: 0 .35rem; font-weight: 700; }
.segment-card .field:nth-of-type(3) { grid-column: 1 / -1; }
.color-input { max-width: 6rem; padding: .25rem; }
@media (max-width: 900px) { .roulette-dialog { width: calc(100vw - 1rem); max-width: calc(100vw - 1rem); } .roulette-fields { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 640px) { .import-export-grid, .segment-grid, .roulette-fields { grid-template-columns: 1fr; } .segments-heading { align-items: flex-start; flex-direction: column; } .heading-actions { width: 100%; } }
</style>
