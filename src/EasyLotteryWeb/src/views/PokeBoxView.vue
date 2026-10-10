<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from "vue";
import { apiRequest, apiText } from "../api/client";
import ObsAssetUrlPicker from "../components/ObsAssetUrlPicker.vue";
import {
  buildPokeTestUrl,
  clonePokeTemplate,
  createPokeTemplate,
  resizePokeGrid,
  type PokeAnimation,
  type PokeTemplate
} from "../pokebox/template";

const templates = ref<PokeTemplate[]>([]);
const pageState = ref<"loading" | "error" | "loaded">("loading");
const editorDialog = ref<HTMLDialogElement | null>(null);
const editing = ref<PokeTemplate | null>(null);
const importJson = ref("");
const exportJson = ref("");
const error = ref("");
const editorError = ref("");
const success = ref("");
const saving = ref(false);
const busyAction = ref("");

const editorTitle = computed(() => !editing.value
  ? "戳戳樂模板"
  : editing.value.id === 0 ? "新增戳戳樂模板" : `編輯戳戳樂模板：${editing.value.name}`);
const isPublished = computed({
  get: () => editing.value?.publicationStatus === 0,
  set: (value: boolean) => { if (editing.value) editing.value.publicationStatus = value ? 0 : 1; }
});

const animationNames: Record<PokeAnimation, string> = {
  0: "爆破",
  1: "煙霧",
  2: "光芒",
  3: "彈跳",
  4: "翻轉"
};

async function loadTemplates(openRequestedEditor = false) {
  pageState.value = "loading";
  error.value = "";
  try {
    const response = await apiRequest<PokeTemplate[]>("/api/poke-templates");
    templates.value = response.data;
    pageState.value = "loaded";
    if (openRequestedEditor) await openRequestedEditorFromQuery();
    return true;
  } catch (cause) {
    pageState.value = "error";
    error.value = cause instanceof Error ? cause.message : "戳戳樂模板載入失敗。";
    return false;
  }
}

async function retryLoad() {
  await loadTemplates(true);
}

async function showEditor(template: PokeTemplate) {
  editing.value = clonePokeTemplate(template);
  editorError.value = "";
  await nextTick();
  if (editorDialog.value && !editorDialog.value.open) editorDialog.value.showModal();
}

function create() {
  void showEditor(createPokeTemplate());
}

function edit(template: PokeTemplate) {
  void showEditor(template);
}

function closeEditor() {
  if (editorDialog.value?.open) editorDialog.value.close();
  else editing.value = null;
}

function handleEditorClose() {
  editing.value = null;
  editorError.value = "";
}

function applyGrid() {
  if (editing.value) editing.value = resizePokeGrid(editing.value);
}

async function save() {
  if (!editing.value || saving.value) return;
  const template = resizePokeGrid(editing.value);
  if (!template.name.trim()) {
    editorError.value = "請輸入模板名稱。";
    return;
  }
  editing.value = template;
  editorError.value = "";
  error.value = "";
  success.value = "";
  saving.value = true;
  try {
    const isNew = template.id === 0;
    await apiRequest<PokeTemplate>(isNew ? "/api/poke-templates" : `/api/poke-templates/${template.id}`, {
      method: isNew ? "POST" : "PUT",
      body: JSON.stringify(template)
    });
    closeEditor();
    if (await loadTemplates()) success.value = "戳戳樂模板已儲存。";
  } catch (cause) {
    editorError.value = cause instanceof Error ? cause.message : "戳戳樂模板儲存失敗。";
  } finally {
    saving.value = false;
  }
}

async function remove(template: PokeTemplate) {
  if (template.isBuiltIn || !window.confirm(`確定要刪除「${template.name}」嗎？`)) return;
  error.value = "";
  success.value = "";
  busyAction.value = `delete-${template.id}`;
  try {
    await apiRequest<null>(`/api/poke-templates/${template.id}`, { method: "DELETE" });
    if (await loadTemplates()) success.value = "戳戳樂模板已刪除。";
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "戳戳樂模板刪除失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function duplicate(template: PokeTemplate) {
  error.value = "";
  success.value = "";
  busyAction.value = `duplicate-${template.id}`;
  try {
    const response = await apiRequest<PokeTemplate>(`/api/poke-templates/${template.id}/duplicate`, { method: "POST" });
    if (await loadTemplates()) {
      success.value = `已建立複本「${response.data.name}」。`;
      edit(response.data);
    }
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "戳戳樂模板複製失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function seedDefaults() {
  error.value = "";
  success.value = "";
  busyAction.value = "seed";
  try {
    await apiRequest<null>("/api/poke-templates/seed-defaults", { method: "POST" });
    if (await loadTemplates()) success.value = "已載入預設模板。";
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "載入預設模板失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function exportTemplate(template: PokeTemplate) {
  error.value = "";
  busyAction.value = `export-${template.id}`;
  try {
    const response = await apiText(`/api/poke-templates/${template.id}/export`);
    exportJson.value = response.data;
    success.value = `已匯出「${template.name}」模板。`;
  } catch (cause) {
    error.value = cause instanceof Error ? cause.message : "戳戳樂模板匯出失敗。";
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
    await apiRequest<PokeTemplate>("/api/poke-templates/import", { method: "POST", body: importJson.value });
    importJson.value = "";
    if (await loadTemplates()) success.value = "模板匯入成功。";
  } catch (cause) {
    error.value = cause instanceof Error ? `匯入失敗：${cause.message}` : "戳戳樂模板匯入失敗。";
  } finally {
    busyAction.value = "";
  }
}

async function openTestObs(template: PokeTemplate) {
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
      body: JSON.stringify({ resourceKind: "pokebox", resourceId: template.publicId, scopes: ["read", "control"] })
    });
    popup.location.href = buildPokeTestUrl(window.location.origin, template.publicId, response.data.token);
  } catch (cause) {
    popup.close();
    error.value = cause instanceof Error ? cause.message : "無法建立 OBS 測試工作階段。";
  }
}

function modeLabel(template: PokeTemplate) {
  return template.mode === 0 ? "隨機" : "手動";
}

function publicationLabel(template: PokeTemplate) {
  return template.publicationStatus === 0 ? "啟用" : "停用";
}

async function openRequestedEditorFromQuery() {
  const requestedId = new URLSearchParams(window.location.search).get("edit");
  if (requestedId === null) return;
  const id = Number(requestedId);
  if (!Number.isSafeInteger(id) || id < 0) return;
  if (id === 0) {
    create();
    return;
  }
  const template = templates.value.find(item => item.id === id);
  if (template) edit(template);
}

onMounted(() => { void loadTemplates(true); });
</script>

<template>
  <main class="pokebox-page" aria-labelledby="pokebox-title">
    <header class="page-heading poke-heading">
      <div>
        <p class="eyebrow">POKEBOX TEMPLATES</p>
        <h1 id="pokebox-title">戳戳樂模板</h1>
        <p class="lead">建立格子與揭露效果，儲存後可開啟測試 OBS。</p>
      </div>
      <div class="heading-actions">
        <a class="legacy-link" href="/legacy/pokebox">開啟舊版管理頁</a>
        <button class="secondary-button" type="button" :disabled="busyAction !== ''" @click="seedDefaults">載入預設模板</button>
        <button class="submit-button compact" type="button" @click="create">新增模板</button>
      </div>
    </header>

    <p v-if="error" class="login-error" role="alert">{{ error }}</p>
    <p v-if="success" class="success-banner" role="status">{{ success }}</p>

    <section class="settings-card" aria-labelledby="pokebox-list-title">
      <h2 id="pokebox-list-title">模板清單</h2>
      <p v-if="pageState === 'loading'" class="status-text" role="status">載入模板中…</p>
      <div v-else-if="pageState === 'error'" class="load-error">
        <p>無法載入戳戳樂模板。請確認管理員工作階段仍有效，或稍後再試。</p>
        <button class="secondary-button" type="button" @click="retryLoad">重新載入</button>
      </div>
      <p v-else-if="templates.length === 0" class="status-text">尚無模板，請新增模板或載入預設模板。</p>
      <div v-else class="table-wrap">
        <table aria-label="戳戳樂模板清單">
          <thead><tr><th>模板</th><th>格數</th><th>模式</th><th>動畫</th><th>節奏</th><th>狀態</th><th>操作</th></tr></thead>
          <tbody>
            <tr v-for="template in templates" :key="template.id">
              <td><strong>{{ template.name || "（未命名）" }}</strong><small class="template-description">{{ template.description }}</small></td>
              <td>{{ template.gridRows }} × {{ template.gridColumns }}</td>
              <td>{{ modeLabel(template) }}</td>
              <td>{{ animationNames[template.animation] }}</td>
              <td>{{ template.animationDurationMs }} ms / {{ template.resultDisplayDurationSeconds }} 秒</td>
              <td><span :class="template.publicationStatus === 0 ? 'state-enabled' : 'state-disabled'">{{ publicationLabel(template) }}</span></td>
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

    <dialog ref="editorDialog" class="poke-dialog" :aria-label="editorTitle" @close="handleEditorClose">
      <form v-if="editing" class="poke-editor" @submit.prevent="save">
        <header class="dialog-heading">
          <h2>{{ editorTitle }}</h2>
          <button class="icon-button" type="button" aria-label="關閉編輯器" @click="closeEditor">×</button>
        </header>
        <div class="poke-editor-content">
          <p v-if="editorError" class="login-error" role="alert">{{ editorError }}</p>
          <div class="poke-fields">
            <label class="field">模板名稱
              <input v-model="editing.name" class="settings-input" type="text" required maxlength="120" />
            </label>
            <label class="field">說明
              <input v-model="editing.description" class="settings-input" type="text" />
            </label>
            <label class="field">行數
              <input v-model.number="editing.gridRows" class="settings-input" type="number" min="1" max="10" step="1" />
            </label>
            <label class="field">欄數
              <input v-model.number="editing.gridColumns" class="settings-input" type="number" min="1" max="10" step="1" />
            </label>
            <div class="field field-action"><span aria-hidden="true">&nbsp;</span><button class="secondary-button" type="button" @click="applyGrid">套用格數</button></div>
            <label class="field">戳擊模式
              <select v-model.number="editing.mode" class="settings-input"><option :value="0">隨機</option><option :value="1">手動</option></select>
            </label>
            <label class="field">動畫效果
              <select v-model.number="editing.animation" class="settings-input"><option :value="0">爆破</option><option :value="1">煙霧</option><option :value="2">光芒</option><option :value="3">彈跳</option><option :value="4">翻轉</option></select>
            </label>
            <label class="field">動畫秒數（毫秒）
              <input v-model.number="editing.animationDurationMs" class="settings-input" type="number" min="300" max="10000" step="1" />
            </label>
            <label class="field">結果停留秒數
              <input v-model.number="editing.resultDisplayDurationSeconds" class="settings-input" type="number" min="1" max="300" step="1" />
            </label>
            <label class="field field-checkbox">允許重複戳
              <input v-model="editing.allowRePoking" type="checkbox" />
            </label>
            <label class="field">次數限制（0 = 無限）
              <input v-model.number="editing.maxPokeCount" class="settings-input" type="number" min="0" step="1" />
            </label>
            <label class="field field-checkbox">啟用
              <input v-model="isPublished" type="checkbox" />
            </label>
            <label class="field">字體
              <input v-model="editing.fontFamily" class="settings-input" type="text" placeholder="例如：Arial" />
            </label>
            <label class="field">恭喜語句
              <input v-model="editing.congratulationMessage" class="settings-input" type="text" />
            </label>
            <label class="field">Overlay 寬度
              <input v-model.number="editing.overlayWidth" class="settings-input" type="number" min="320" max="3840" step="1" />
            </label>
            <label class="field">Overlay 高度
              <input v-model.number="editing.overlayHeight" class="settings-input" type="number" min="240" max="2160" step="1" />
            </label>
            <label class="field">背景圖片 URL
              <ObsAssetUrlPicker v-model="editing.backgroundImageUrl" :kind="0" label="背景圖片 URL" placeholder="輸入圖片網址或從資產庫選擇" />
            </label>
            <label class="field">戳擊音效 URL
              <ObsAssetUrlPicker v-model="editing.pokeSoundUrl" :kind="1" label="戳擊音效 URL" placeholder="輸入音訊網址或從資產庫選擇" />
            </label>
            <label class="field">揭露音效 URL
              <ObsAssetUrlPicker v-model="editing.openSoundUrl" :kind="1" label="揭露音效 URL" placeholder="輸入音訊網址或從資產庫選擇" />
            </label>
          </div>

          <section class="cells-section" aria-labelledby="cells-heading">
            <div class="cells-heading">
              <div><h3 id="cells-heading">格子設定</h3><p>共 {{ editing.cells.length }} 格；調整行列後請按「套用格數」。</p></div>
            </div>
            <div class="cell-grid">
              <fieldset v-for="cell in editing.cells" :key="cell.id || cell.index" class="cell-card" :style="{ borderLeftColor: cell.revealedColor }">
                <legend>格 {{ cell.index + 1 }}</legend>
                <label class="field">標題
                  <input v-model="cell.title" class="settings-input" type="text" />
                </label>
                <label class="field">副標
                  <input v-model="cell.subTitle" class="settings-input" type="text" />
                </label>
                <label class="field">未揭露圖片 URL
                  <ObsAssetUrlPicker v-model="cell.imageUrl" :kind="0" :label="`第 ${cell.index + 1} 格未揭露圖片 URL`" placeholder="輸入圖片網址或從資產庫選擇" />
                </label>
                <label class="field">揭露後圖片 URL
                  <ObsAssetUrlPicker v-model="cell.revealedImageUrl" :kind="0" :label="`第 ${cell.index + 1} 格揭露後圖片 URL`" placeholder="輸入圖片網址或從資產庫選擇" />
                </label>
                <label class="field">揭露後顏色
                  <input v-model="cell.revealedColor" class="settings-input color-input" type="color" />
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
.pokebox-page { display: grid; gap: 1rem; padding-bottom: 2rem; }
.poke-heading { align-items: flex-start; }
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
.poke-dialog { width: min(75vw, 1400px); max-width: 75vw; max-height: 90vh; padding: 0; border: 0; border-radius: .8rem; box-shadow: 0 20px 60px #0f172a55; }
.poke-dialog::backdrop { background: #0f172a88; }
.poke-editor { display: grid; grid-template-rows: auto minmax(0, 1fr) auto; max-height: 90vh; }
.dialog-heading, .dialog-footer { display: flex; align-items: center; justify-content: space-between; gap: .75rem; padding: 1rem 1.25rem; border-bottom: 1px solid #e5eaf2; }
.dialog-heading h2 { margin: 0; font-size: 1.2rem; }
.dialog-footer { justify-content: flex-end; border-top: 1px solid #e5eaf2; border-bottom: 0; }
.icon-button { border: 0; background: transparent; font: inherit; font-size: 1.5rem; cursor: pointer; }
.poke-editor-content { overflow: auto; padding: 1.25rem; }
.poke-fields { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 1rem; }
.cells-section { margin-top: 1.5rem; }
.cells-heading { margin-bottom: .75rem; }
.cells-heading h3, .cells-heading p { margin: 0 0 .25rem; }
.cell-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .85rem; }
.cell-card { display: grid; grid-template-columns: 1fr 1fr; gap: .65rem; min-width: 0; padding: .8rem; border: 1px solid #dbe3ef; border-left: 6px solid #ccc; border-radius: .55rem; }
.cell-card legend { padding: 0 .35rem; font-weight: 700; }
.cell-card .field:nth-of-type(3), .cell-card .field:nth-of-type(4) { grid-column: 1 / -1; }
.color-input { max-width: 6rem; padding: .25rem; }
@media (max-width: 900px) { .poke-dialog { width: calc(100vw - 1rem); max-width: calc(100vw - 1rem); } .poke-fields { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 640px) { .import-export-grid, .cell-grid, .poke-fields { grid-template-columns: 1fr; } .heading-actions { width: 100%; } }
</style>
