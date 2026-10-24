<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { ApiError, apiRequest } from "../api/client";
import { buildDonateDrawHistoryQuery, type DonateDrawOutcomeFilter } from "../donate-draw-records/records";

interface DonateActivityOption { id: number; name: string }
interface DonateDrawRecord {
  id: number;
  activityId: number;
  activityName: string;
  drawnAtUtc: string;
  donorName: string;
  amount: number;
  drawCount: number;
  prizeName: string;
  isWinning: boolean;
}
interface DonateDrawHistoryPage {
  items: DonateDrawRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

const filters = reactive({
  activityId: "",
  beforeDate: "",
  fromDate: "",
  outcome: "all" as DonateDrawOutcomeFilter,
  search: ""
});
const activities = ref<DonateActivityOption[]>([]);
const history = ref<DonateDrawHistoryPage | null>(null);
const isLoading = ref(true);
const loadError = ref("");
const requestVersion = ref(0);
const pageSize = 20;

async function loadRecords(page = 1) {
  const version = ++requestVersion.value;
  isLoading.value = true;
  loadError.value = "";
  const query = buildDonateDrawHistoryQuery({ ...filters, page, pageSize });
  try {
    const [activityResponse, historyResponse] = await Promise.all([
      apiRequest<DonateActivityOption[]>("/api/donate-activities"),
      apiRequest<DonateDrawHistoryPage>(`/api/donate-lottery-draw-records?${query}`)
    ]);
    if (version !== requestVersion.value) return;
    activities.value = activityResponse.data ?? [];
    history.value = historyResponse.data;
  } catch (error) {
    if (version !== requestVersion.value) return;
    loadError.value = error instanceof ApiError && error.status === 401
      ? "管理員登入已逾期，請重新登入後再查看 Donate 抽獎紀錄。"
      : error instanceof ApiError && error.status === 403
        ? "目前的登入權限不足以查看管理員 Donate 抽獎紀錄。"
        : "Donate 抽獎紀錄暫時無法載入，請檢查連線後重試。";
  } finally {
    if (version === requestVersion.value) isLoading.value = false;
  }
}

function applyFilters() { void loadRecords(1); }
function clearFilters() {
  filters.activityId = "";
  filters.fromDate = "";
  filters.beforeDate = "";
  filters.outcome = "all";
  filters.search = "";
  applyFilters();
}
function changePage(value: number) {
  if (history.value && value >= 1 && value <= history.value.totalPages) void loadRecords(value);
}
function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-TW", {
    year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false
  }).format(new Date(value));
}
function formatAmount(value: number) {
  return new Intl.NumberFormat("zh-TW", { maximumFractionDigits: 2 }).format(value);
}

onMounted(() => { void loadRecords(); });
</script>

<template>
  <main class="donate-records-page">
    <header class="records-hero">
      <div>
        <div class="records-eyebrow">DONATE DRAW ARCHIVE</div>
        <h1>Donate 抽獎紀錄</h1>
        <p>查詢已保存的 Donate 抽獎結果。此頁不會發起抽獎，也不顯示付款識別碼或捐款留言。</p>
      </div>
      <div v-if="!isLoading && !loadError && history" class="records-total" aria-label="紀錄筆數">
        <span>符合條件</span><strong>{{ history.totalCount }}</strong><span>筆紀錄</span>
      </div>
    </header>

    <section class="records-filter-panel" aria-label="Donate 抽獎紀錄篩選">
      <div class="records-filter-grid">
        <div class="records-filter-field">
          <label for="donate-record-search">搜尋活動、觀眾或獎項</label>
          <input id="donate-record-search" v-model="filters.search" type="search" maxlength="120" placeholder="輸入關鍵字或活動編號">
        </div>
        <div class="records-filter-field">
          <label for="donate-record-activity">Donate 活動</label>
          <select id="donate-record-activity" v-model="filters.activityId">
            <option value="">全部活動</option>
            <option v-for="activity in activities" :key="activity.id" :value="String(activity.id)">{{ activity.name }}</option>
          </select>
        </div>
        <div class="records-filter-field">
          <label for="donate-record-outcome">結果類型</label>
          <select id="donate-record-outcome" v-model="filters.outcome">
            <option value="all">全部結果</option>
            <option value="winning">中獎</option>
            <option value="miss">未中獎</option>
          </select>
        </div>
        <div class="records-filter-field">
          <label for="donate-record-from">開始日期</label>
          <input id="donate-record-from" v-model="filters.fromDate" type="date">
        </div>
        <div class="records-filter-field">
          <label for="donate-record-before">結束日期</label>
          <input id="donate-record-before" v-model="filters.beforeDate" type="date">
        </div>
      </div>
      <div class="records-filter-actions">
        <v-btn color="primary" :disabled="isLoading" @click="applyFilters">套用篩選</v-btn>
        <v-btn color="secondary" variant="outlined" :disabled="isLoading" @click="clearFilters">清除篩選</v-btn>
      </div>
    </section>

    <section v-if="isLoading" class="records-state" role="status" aria-live="polite">
      <v-progress-circular indeterminate color="primary" aria-label="載入 Donate 抽獎紀錄" />
      <span>正在載入 Donate 抽獎紀錄…</span>
    </section>
    <section v-else-if="loadError" class="records-state records-error" role="alert">
      <div><h2>紀錄載入失敗</h2><p>{{ loadError }}</p></div>
      <v-btn color="primary" @click="loadRecords(history?.page ?? 1)">重新載入</v-btn>
    </section>
    <section v-else-if="history?.totalCount === 0" class="records-state" aria-live="polite">
      <div>
        <h2>{{ history.page === 1 && !filters.search && !filters.activityId && filters.outcome === "all" && !filters.fromDate && !filters.beforeDate ? "尚無 Donate 抽獎紀錄" : "找不到符合條件的紀錄" }}</h2>
        <p>只會顯示後端已保存的結果；不會補造過去紀錄。</p>
      </div>
      <v-btn v-if="filters.search || filters.activityId || filters.outcome !== 'all' || filters.fromDate || filters.beforeDate" color="secondary" variant="outlined" @click="clearFilters">清除篩選</v-btn>
    </section>
    <template v-else-if="history">
      <div class="records-table-wrap">
        <table class="records-table">
          <caption class="sr-only">Donate 抽獎結果紀錄，共 {{ history.totalCount }} 筆</caption>
          <thead><tr>
            <th scope="col">抽獎時間</th><th scope="col">Donate 活動</th><th scope="col">觀眾</th>
            <th scope="col">抽獎結果</th><th scope="col">贊助金額</th><th scope="col">抽獎次數</th>
          </tr></thead>
          <tbody>
            <tr v-for="record in history.items" :key="record.id">
              <td data-label="抽獎時間"><time :datetime="record.drawnAtUtc">{{ formatDate(record.drawnAtUtc) }}</time></td>
              <td data-label="Donate 活動">{{ record.activityName }}</td>
              <td data-label="觀眾">{{ record.donorName || "匿名觀眾" }}</td>
              <td data-label="抽獎結果"><span class="records-outcome" :class="record.isWinning ? 'is-winning' : 'is-miss'">{{ record.isWinning ? "中獎" : "未中獎" }}</span><strong class="records-prize">{{ record.prizeName }}</strong></td>
              <td data-label="贊助金額">{{ formatAmount(record.amount) }}</td>
              <td data-label="抽獎次數">{{ record.drawCount }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <nav class="records-pagination" aria-label="Donate 抽獎紀錄分頁">
        <span>第 {{ history.page }} / {{ history.totalPages }} 頁</span>
        <span>每頁 {{ history.pageSize }} 筆</span>
        <v-btn variant="outlined" :disabled="history.page <= 1 || isLoading" @click="changePage(history.page - 1)">上一頁</v-btn>
        <v-btn variant="outlined" :disabled="history.page >= history.totalPages || isLoading" @click="changePage(history.page + 1)">下一頁</v-btn>
      </nav>
    </template>
  </main>
</template>

<style scoped>
.donate-records-page { --ink: #10233f; --muted: #607089; display: grid; gap: 18px; padding: 8px 6px 32px; color: var(--ink); }
.records-hero, .records-filter-panel, .records-state, .records-table-wrap { padding: 20px; border: 1px solid #dce3ec; border-radius: 18px; background: #fff; box-shadow: 0 8px 24px #0f172a0a; }
.records-hero { display: flex; justify-content: space-between; align-items: center; gap: 20px; background: linear-gradient(135deg, #fffdf8, #f3f6fb); }
.records-eyebrow { color: #9a3412; font-size: .76rem; font-weight: 700; letter-spacing: .16em; }
h1 { margin: 8px 0; font-size: clamp(1.8rem, 4vw, 2.5rem); }
.records-hero p, .records-state p { margin: 0; color: var(--muted); line-height: 1.6; }
.records-total { display: grid; min-width: 120px; gap: 4px; text-align: center; color: var(--muted); }
.records-total strong { color: var(--ink); font-size: 2rem; }
.records-filter-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 12px; }
.records-filter-field { display: grid; gap: 7px; }
.records-filter-field label { color: var(--muted); font-size: .88rem; font-weight: 700; }
.records-filter-field input, .records-filter-field select { width: 100%; min-width: 0; min-height: 42px; padding: 8px 10px; border: 1px solid #cbd5e1; border-radius: 8px; background: white; color: var(--ink); font: inherit; }
.records-filter-field input:focus-visible, .records-filter-field select:focus-visible { outline: 3px solid #2563eb55; outline-offset: 2px; }
.records-filter-actions, .records-pagination { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin-top: 16px; }
.records-state { display: flex; justify-content: space-between; align-items: center; gap: 16px; min-height: 120px; }
.records-state h2 { margin: 0 0 6px; font-size: 1.15rem; }
.records-error { border-color: #fecaca; background: #fffafa; }
.records-table-wrap { padding: 0; overflow-x: auto; }
.records-table { width: 100%; border-collapse: collapse; text-align: left; }
.records-table th, .records-table td { padding: 13px 14px; border-bottom: 1px solid #e2e8f0; vertical-align: middle; }
.records-table th { background: #f8fafc; color: #475569; font-size: .84rem; white-space: nowrap; }
.records-table tbody tr:last-child td { border-bottom: 0; }
.records-outcome { display: inline-flex; padding: 3px 8px; border-radius: 999px; font-size: .78rem; font-weight: 700; }
.is-winning { background: #dcfce7; color: #166534; }
.is-miss { background: #f1f5f9; color: #475569; }
.records-prize { display: block; margin-top: 4px; }
.records-pagination { justify-content: flex-end; color: var(--muted); }
.sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 800px) { .records-filter-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
@media (max-width: 600px) {
  .records-hero, .records-state { align-items: flex-start; flex-direction: column; }
  .records-filter-grid { grid-template-columns: 1fr; }
  .records-table-wrap { overflow: visible; padding: 10px; }
  .records-table, .records-table tbody, .records-table tr, .records-table td { display: block; width: 100%; }
  .records-table thead { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0, 0, 0, 0); }
  .records-table tr { padding: 6px 0; border-bottom: 1px solid #cbd5e1; }
  .records-table tr:last-child { border-bottom: 0; }
  .records-table td { display: grid; grid-template-columns: 7.2rem minmax(0, 1fr); gap: 8px; padding: 9px 4px; border: 0; overflow-wrap: anywhere; }
  .records-table td::before { content: attr(data-label); color: var(--muted); font-size: .82rem; font-weight: 700; }
  .records-pagination { justify-content: flex-start; }
}
</style>
