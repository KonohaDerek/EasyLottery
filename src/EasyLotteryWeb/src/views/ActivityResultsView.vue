<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { apiRequest, ApiError } from "../api/client";
import {
  filterActivityResults,
  serializeActivityResultsCsv,
  type ActivityResultRecord,
  type ActivityTypeFilter
} from "../activity-results/results";

const records = ref<ActivityResultRecord[]>([]);
const isLoading = ref(true);
const loadError = ref("");
const search = ref("");
const typeFilter = ref<ActivityTypeFilter>("all");
const startDate = ref("");
const endDate = ref("");

const filteredRecords = computed(() => filterActivityResults(records.value, {
  search: search.value,
  type: typeFilter.value,
  startDate: startDate.value,
  endDate: endDate.value
}));
const pokeCount = computed(() => filteredRecords.value.filter(record => record.activityType === 0).length);
const rouletteCount = computed(() => filteredRecords.value.filter(record => record.activityType === 1).length);
const latestActivity = computed(() => filteredRecords.value.length
  ? formatDate(filteredRecords.value[0].activityDateUtc)
  : "尚無紀錄");

async function loadResults() {
  isLoading.value = true;
  loadError.value = "";
  try {
    const response = await apiRequest<ActivityResultRecord[]>("/api/activity-results");
    records.value = response.data ?? [];
  } catch (error) {
    loadError.value = error instanceof ApiError && error.status === 401
      ? "管理員登入已逾期，請重新登入後再查看活動結果。"
      : error instanceof Error ? error.message : "活動結果載入失敗，請稍後重試。";
  } finally {
    isLoading.value = false;
  }
}

function clearFilters() {
  search.value = "";
  typeFilter.value = "all";
  startDate.value = "";
  endDate.value = "";
}

function exportResults() {
  if (!filteredRecords.value.length) return;
  const url = URL.createObjectURL(new Blob(
    [serializeActivityResultsCsv(filteredRecords.value)],
    { type: "text/csv;charset=utf-8" }
  ));
  const link = document.createElement("a");
  link.href = url;
  link.download = `activity-results-${new Date().toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z")}.csv`;
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-TW", {
    year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false
  }).format(new Date(value));
}

function typeLabel(type: number) {
  return type === 0 ? "戳戳樂" : type === 1 ? "轉盤" : "活動";
}

function colorStyle(color: string) {
  return { background: color || "linear-gradient(135deg, #dbeafe 0%, #e2e8f0 100%)" };
}

onMounted(() => { void loadResults(); });
</script>

<template>
  <main class="activity-results-page">
    <header class="results-hero">
      <div>
        <div class="results-eyebrow">ACTIVITY ARCHIVE</div>
        <h1>活動結果</h1>
        <p>集中查看已結束的戳戳樂與轉盤活動，快速搜尋活動日期與抽獎明細。</p>
      </div>
      <div v-if="!isLoading && !loadError" class="results-metrics" aria-label="活動結果統計">
        <div><span>活動總數</span><strong>{{ records.length }}</strong></div>
        <div><span>符合篩選</span><strong>{{ filteredRecords.length }}</strong></div>
        <div><span>戳戳樂</span><strong>{{ pokeCount }}</strong></div>
        <div><span>轉盤</span><strong>{{ rouletteCount }}</strong></div>
        <div><span>最近活動</span><strong class="latest-date">{{ latestActivity }}</strong></div>
      </div>
    </header>

    <section v-if="!isLoading && !loadError" class="results-filter-panel" aria-label="活動結果篩選">
      <div class="filter-grid">
        <div class="filter-field">
          <label for="results-search">搜尋</label>
          <input id="results-search" v-model="search" type="search" placeholder="活動名稱、摘要或內容關鍵字">
        </div>
        <div class="filter-field">
          <label for="results-type">類型</label>
          <select id="results-type" v-model="typeFilter">
            <option value="all">全部</option>
            <option value="pokebox">戳戳樂</option>
            <option value="roulette">轉盤</option>
          </select>
        </div>
        <div class="filter-field">
          <label for="results-start-date">開始日期</label>
          <input id="results-start-date" v-model="startDate" type="date">
        </div>
        <div class="filter-field">
          <label for="results-end-date">結束日期</label>
          <input id="results-end-date" v-model="endDate" type="date">
        </div>
      </div>
      <div class="filter-actions">
        <v-btn color="secondary" variant="outlined" @click="clearFilters">清除篩選</v-btn>
        <v-btn color="primary" :disabled="!filteredRecords.length" @click="exportResults">匯出目前結果</v-btn>
      </div>
      <p class="filter-meta">目前顯示 {{ filteredRecords.length }} / {{ records.length }} 筆結果</p>
    </section>

    <section v-if="isLoading" class="state-panel" role="status" aria-live="polite">
      <v-progress-circular indeterminate color="primary" aria-label="載入活動結果" />
      <span>正在載入活動結果…</span>
    </section>

    <section v-else-if="loadError" class="state-panel error-panel" role="alert">
      <div>
        <h2>活動結果載入失敗</h2>
        <p>{{ loadError }}</p>
      </div>
      <v-btn color="primary" @click="loadResults">重新載入</v-btn>
    </section>

    <section v-else-if="records.length === 0" class="state-panel" aria-live="polite">
      <div>
        <h2>尚無活動結果紀錄</h2>
        <p>完成一次戳戳樂或轉盤活動並記錄結果後，紀錄就會出現在這裡。</p>
      </div>
    </section>

    <section v-else-if="filteredRecords.length === 0" class="state-panel" aria-live="polite">
      <div>
        <h2>找不到符合條件的活動結果</h2>
        <p>請調整搜尋條件，或清除篩選查看全部紀錄。</p>
      </div>
      <v-btn color="secondary" variant="outlined" @click="clearFilters">清除篩選</v-btn>
    </section>

    <div v-else class="result-card-list">
      <article v-for="record in filteredRecords" :key="record.id" class="result-card">
        <div class="result-card-header">
          <div>
            <div class="result-chip-row">
              <span class="result-type-chip" :class="record.activityType === 0 ? 'type-pokebox' : 'type-roulette'">
                {{ typeLabel(record.activityType) }}
              </span>
              <span class="result-date-chip">{{ formatDate(record.activityDateUtc) }}</span>
            </div>
            <h2>{{ record.activityName }}</h2>
          </div>
          <span class="result-count-chip">{{ record.items.length }} 筆結果</span>
        </div>

        <div class="result-summary">
          <span>活動摘要</span>
          <p>{{ record.summary }}</p>
        </div>

        <details class="result-details">
          <summary>查看活動結果 <span>展開明細</span></summary>
          <div class="result-item-list">
            <div v-for="item in [...record.items].sort((left, right) => left.order - right.order)"
                 :key="item.order" class="result-item-row">
              <strong class="result-item-order">{{ item.order }}</strong>
              <img v-if="item.imageUrl" class="result-item-image" :src="item.imageUrl" :alt="item.name" loading="lazy">
              <div v-else class="result-item-color" :style="colorStyle(item.color)" aria-hidden="true">NO IMAGE</div>
              <div class="result-item-content">
                <strong>{{ item.name }}</strong>
                <p :class="{ muted: !item.description }">{{ item.description || "無額外說明" }}</p>
              </div>
              <div class="result-item-time">
                <span>記錄時間</span>
                <time v-if="item.resultedAtUtc" :datetime="item.resultedAtUtc">{{ formatDate(item.resultedAtUtc) }}</time>
                <span v-else>-</span>
              </div>
            </div>
          </div>
        </details>
      </article>
    </div>
  </main>
</template>

<style scoped>
.activity-results-page { --ink: #10233f; --muted: #607089; display: grid; gap: 20px; padding: 8px 6px 32px; color: var(--ink); }
.results-hero { display: grid; grid-template-columns: minmax(0, 1fr) minmax(300px, 1.1fr); gap: 20px; padding: 24px; border: 1px solid #dce3ec; border-radius: 22px; background: linear-gradient(135deg, #fffdf8, #f3f6fb); }
.results-eyebrow { color: #9a3412; font-size: .76rem; font-weight: 700; letter-spacing: .16em; }
h1 { margin: 8px 0; font-size: clamp(1.8rem, 4vw, 2.5rem); }
.results-hero p, .state-panel p { margin: 0; color: var(--muted); line-height: 1.6; }
.results-metrics { display: grid; grid-template-columns: repeat(auto-fit, minmax(110px, 1fr)); gap: 10px; }
.results-metrics > div { display: grid; align-content: center; gap: 7px; min-height: 86px; padding: 12px; border: 1px solid #e2e8f0; border-radius: 15px; background: #ffffffd9; }
.results-metrics span, .result-item-time span { color: var(--muted); font-size: .84rem; }
.results-metrics strong { font-size: 1.7rem; }
.results-metrics .latest-date { font-size: .9rem; line-height: 1.4; }
.results-filter-panel, .result-card, .state-panel { padding: 20px; border: 1px solid #e2e8f0; border-radius: 18px; background: white; box-shadow: 0 8px 24px #0f172a0a; }
.filter-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
.filter-field { display: grid; gap: 7px; }
.filter-field label { color: var(--muted); font-size: .88rem; font-weight: 700; }
.filter-field input, .filter-field select { min-width: 0; min-height: 42px; padding: 8px 10px; border: 1px solid #cbd5e1; border-radius: 8px; background: white; color: var(--ink); font: inherit; }
.filter-field input:focus, .filter-field select:focus, .result-details summary:focus-visible { outline: 3px solid #2563eb55; outline-offset: 2px; }
.filter-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 16px; }
.filter-meta { margin: 12px 0 0; color: var(--muted); font-size: .9rem; }
.result-card-list { display: grid; gap: 16px; }
.result-card-header { display: flex; justify-content: space-between; gap: 12px; align-items: flex-start; }
.result-card-header h2 { margin: 10px 0 0; font-size: 1.25rem; }
.result-chip-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.result-type-chip, .result-date-chip, .result-count-chip { display: inline-flex; align-items: center; min-height: 28px; padding: 4px 10px; border-radius: 999px; font-size: .82rem; }
.type-pokebox { background: #fff7ed; color: #9a3412; }
.type-roulette { background: #ecfdf5; color: #047857; }
.result-date-chip, .result-count-chip { background: #f1f5f9; color: #475569; }
.result-summary { margin-top: 16px; padding: 12px; border-radius: 12px; background: #f8fafc; }
.result-summary span { color: var(--muted); font-size: .82rem; font-weight: 700; }
.result-summary p { margin: 5px 0 0; white-space: pre-wrap; }
.result-details { margin-top: 16px; border-top: 1px solid #e2e8f0; }
.result-details summary { display: flex; justify-content: space-between; gap: 10px; padding: 14px 2px 2px; cursor: pointer; font-weight: 700; }
.result-details summary span { color: var(--muted); font-size: .86rem; font-weight: 400; }
.result-item-list { display: grid; gap: 10px; margin-top: 12px; }
.result-item-row { display: grid; grid-template-columns: 34px 64px minmax(0, 1fr) auto; gap: 12px; align-items: center; padding: 12px; border: 1px solid #e2e8f0; border-radius: 12px; }
.result-item-order { display: grid; width: 32px; height: 32px; place-items: center; border-radius: 50%; background: #e0e7ff; }
.result-item-image, .result-item-color { width: 64px; height: 64px; border-radius: 10px; object-fit: cover; }
.result-item-color { display: grid; place-items: center; color: #475569; font-size: .65rem; }
.result-item-content p { margin: 4px 0 0; color: #475569; white-space: pre-wrap; }
.result-item-content p.muted { color: #94a3b8; }
.result-item-time { display: grid; gap: 5px; text-align: right; }
.state-panel { display: flex; flex-wrap: wrap; gap: 14px; align-items: center; justify-content: space-between; min-height: 110px; }
.state-panel h2 { margin: 0 0 6px; font-size: 1.1rem; }
.error-panel { border-color: #fecaca; background: #fff7f7; }
@media (max-width: 900px) { .results-hero { grid-template-columns: 1fr; } .filter-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 560px) { .filter-grid { grid-template-columns: 1fr; } .result-card-header { flex-direction: column; } .result-item-row { grid-template-columns: 32px 54px minmax(0, 1fr); } .result-item-image, .result-item-color { width: 54px; height: 54px; } .result-item-time { grid-column: 3; text-align: left; } }
</style>
