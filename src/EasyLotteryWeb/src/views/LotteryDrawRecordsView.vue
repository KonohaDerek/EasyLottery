<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { ApiError, apiRequest } from "../api/client";
import { buildCoreLotteryDrawHistoryQuery, type CoreLotteryDrawHistoryPage, type CoreLotteryDrawParticipantPage, type CoreLotteryDrawSummary } from "../core-lottery-records/records";

const filters = reactive({ beforeDate: "", fromDate: "", search: "" });
const history = ref<CoreLotteryDrawHistoryPage | null>(null);
const selectedRecord = ref<CoreLotteryDrawSummary | null>(null);
const participantPage = ref<CoreLotteryDrawParticipantPage | null>(null);
const isDetailOpen = ref(false);
const isDetailLoading = ref(false);
const detailError = ref("");
const isLoading = ref(true);
const loadError = ref("");
const pageSize = 20;
const participantPageSize = 50;
let requestVersion = 0;
let detailRequestVersion = 0;

async function loadRecords(page = 1) {
  const version = ++requestVersion;
  isLoading.value = true;
  loadError.value = "";
  const query = buildCoreLotteryDrawHistoryQuery({ ...filters, page, pageSize });
  try {
    const response = await apiRequest<CoreLotteryDrawHistoryPage>(`/api/lottery-draw-records?${query}`);
    if (version === requestVersion) history.value = response.data;
  } catch (error) {
    if (version !== requestVersion) return;
    loadError.value = error instanceof ApiError && error.status === 401
      ? "管理員登入已逾期，請重新登入後再查看抽獎紀錄。"
      : error instanceof ApiError && error.status === 403
        ? "目前的登入權限不足以查看管理員抽獎紀錄。"
        : "抽獎紀錄暫時無法載入，請檢查連線後重試。";
  } finally {
    if (version === requestVersion) isLoading.value = false;
  }
}

function clearFilters() {
  filters.beforeDate = "";
  filters.fromDate = "";
  filters.search = "";
  void loadRecords(1);
}

function changePage(page: number) {
  if (history.value && page >= 1 && page <= history.value.totalPages) void loadRecords(page);
}

async function openRecordDetails(record: CoreLotteryDrawSummary) {
  const version = ++detailRequestVersion;
  selectedRecord.value = record;
  participantPage.value = null;
  detailError.value = "";
  isDetailLoading.value = true;
  isDetailOpen.value = true;
  try {
    const response = await apiRequest<CoreLotteryDrawSummary>(`/api/lottery-draw-records/${encodeURIComponent(record.drawId)}/summary`);
    if (version === detailRequestVersion) selectedRecord.value = response.data;
    await loadParticipants(record.drawId, 1, version);
  } catch (error) {
    if (version !== detailRequestVersion) return;
    detailError.value = error instanceof ApiError && error.status === 401
      ? "管理員登入已逾期，請重新登入後再查看名單。"
      : error instanceof ApiError && error.status === 403
        ? "目前的登入權限不足以查看參與名單。"
        : "抽獎參與名單暫時無法載入，請重試。";
  } finally {
    if (version === detailRequestVersion) isDetailLoading.value = false;
  }
}

async function loadParticipants(drawId: string, page: number, version = detailRequestVersion) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(participantPageSize) });
  const response = await apiRequest<CoreLotteryDrawParticipantPage>(`/api/lottery-draw-records/${encodeURIComponent(drawId)}/participants?${query}`);
  if (version === detailRequestVersion) participantPage.value = response.data;
}

async function changeParticipantPage(page: number) {
  const record = selectedRecord.value;
  if (!record || !participantPage.value || page < 1 || page > participantPage.value.totalPages || isDetailLoading.value) return;
  const version = detailRequestVersion;
  isDetailLoading.value = true;
  detailError.value = "";
  try {
    await loadParticipants(record.drawId, page, version);
  } catch (error) {
    if (version !== detailRequestVersion) return;
    detailError.value = error instanceof ApiError && error.status === 401
      ? "管理員登入已逾期，請重新登入後再查看名單。"
      : error instanceof ApiError && error.status === 403
        ? "目前的登入權限不足以查看參與名單。"
        : "抽獎參與名單暫時無法載入，請重試。";
  } finally {
    if (version === detailRequestVersion) isDetailLoading.value = false;
  }
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-TW", {
    year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false
  }).format(new Date(value));
}

onMounted(() => { void loadRecords(); });
</script>

<template>
  <main class="lottery-records-page">
    <header class="lottery-records-hero">
      <div>
        <div class="lottery-records-eyebrow">CORE LOTTERY ARCHIVE</div>
        <h1>抽獎紀錄</h1>
        <p>僅列出已保存的抽獎結果。本功能啟用前未保存的結果不會補造；抽獎保存狀態不明時，請先對帳。</p>
      </div>
      <div v-if="!isLoading && !loadError && history" class="lottery-records-total" aria-label="抽獎紀錄筆數">
        <span>符合條件</span><strong>{{ history.totalCount }}</strong><span>筆結果</span>
      </div>
    </header>

    <section class="lottery-records-filters" aria-label="抽獎紀錄篩選">
      <div class="lottery-records-filter-grid">
        <div class="lottery-records-filter-field">
          <label for="core-draw-search">搜尋得獎者或獎項</label>
          <input id="core-draw-search" v-model="filters.search" type="search" maxlength="120" placeholder="輸入名稱或獎項關鍵字">
        </div>
        <div class="lottery-records-filter-field">
          <label for="core-draw-from">開始日期</label>
          <input id="core-draw-from" v-model="filters.fromDate" type="date">
        </div>
        <div class="lottery-records-filter-field">
          <label for="core-draw-before">結束日期</label>
          <input id="core-draw-before" v-model="filters.beforeDate" type="date">
        </div>
      </div>
      <div class="lottery-records-filter-actions">
        <v-btn color="primary" :disabled="isLoading" @click="loadRecords(1)">套用篩選</v-btn>
        <v-btn color="secondary" variant="outlined" :disabled="isLoading" @click="clearFilters">清除篩選</v-btn>
      </div>
    </section>

    <section v-if="isLoading" class="lottery-records-state" role="status" aria-live="polite">
      <v-progress-circular indeterminate color="primary" aria-label="載入抽獎紀錄" />
      <span>正在載入抽獎紀錄…</span>
    </section>
    <section v-else-if="loadError" class="lottery-records-state lottery-records-error" role="alert">
      <div><h2>抽獎紀錄載入失敗</h2><p>{{ loadError }}</p></div>
      <v-btn color="primary" @click="loadRecords(history?.page ?? 1)">重新載入</v-btn>
    </section>
    <section v-else-if="history?.totalCount === 0" class="lottery-records-state" aria-live="polite">
      <div>
        <h2>{{ filters.search || filters.fromDate || filters.beforeDate ? "找不到符合條件的抽獎紀錄" : "尚無已保存的抽獎紀錄" }}</h2>
        <p>只顯示伺服器已保存的結果，不會從參加者名單或瀏覽器資料回填舊結果。</p>
      </div>
      <v-btn v-if="filters.search || filters.fromDate || filters.beforeDate" color="secondary" variant="outlined" @click="clearFilters">清除篩選</v-btn>
    </section>
    <template v-else-if="history">
      <div class="lottery-records-table-wrap">
        <table class="lottery-records-table">
          <caption class="lottery-records-sr-only">核心抽獎歷史，共 {{ history.totalCount }} 筆</caption>
          <thead><tr><th scope="col">記錄時間</th><th scope="col">得獎者</th><th scope="col">獎項</th><th scope="col">抽獎識別碼</th><th scope="col">明細</th></tr></thead>
          <tbody>
            <tr v-for="record in history.items" :key="record.drawId">
              <td data-label="記錄時間"><time :datetime="record.drawnAtUtc">{{ formatDate(record.drawnAtUtc) }}</time></td>
              <td data-label="得獎者">{{ record.winnerDisplayName }}</td>
              <td data-label="獎項">{{ record.prizeName }}</td>
              <td data-label="抽獎識別碼"><code>{{ record.drawId }}</code></td>
              <td data-label="明細"><v-btn size="small" variant="text" :aria-label="`檢視紀錄 ${record.drawId}`" @click="openRecordDetails(record)">檢視</v-btn></td>
            </tr>
          </tbody>
        </table>
      </div>
      <nav class="lottery-records-pagination" aria-label="抽獎紀錄分頁">
        <span>第 {{ history.page }} / {{ history.totalPages }} 頁</span><span>每頁 {{ history.pageSize }} 筆</span>
        <v-btn variant="outlined" :disabled="history.page <= 1 || isLoading" @click="changePage(history.page - 1)">上一頁</v-btn>
        <v-btn variant="outlined" :disabled="history.page >= history.totalPages || isLoading" @click="changePage(history.page + 1)">下一頁</v-btn>
      </nav>
    </template>
    <v-dialog v-model="isDetailOpen" max-width="1100" aria-labelledby="core-draw-detail-title">
      <v-card v-if="selectedRecord">
        <v-card-title id="core-draw-detail-title">抽獎紀錄明細</v-card-title>
        <v-card-text>
          <section class="lottery-record-detail">
            <div><span>記錄時間</span><time :datetime="selectedRecord.drawnAtUtc">{{ formatDate(selectedRecord.drawnAtUtc) }}</time></div>
            <div><span>得獎者</span><strong>{{ selectedRecord.winnerDisplayName }}</strong></div>
            <div><span>獎項</span><strong>{{ selectedRecord.prizeName }}</strong></div>
            <div><span>得獎籤 ID</span><code>{{ selectedRecord.winnerParticipantId }}</code></div>
            <div><span>抽獎識別碼</span><code>{{ selectedRecord.drawId }}</code></div>
            <div><span>名單／合格籤數</span><strong>{{ selectedRecord.participantCount }} / {{ selectedRecord.eligibleEntryCount }}</strong></div>
          </section>
          <section v-if="selectedRecord.ruleSnapshot" class="lottery-record-rules" aria-label="抽獎規則快照">
            <h3>抽獎規則快照</h3>
            <dl>
              <div><dt>選擇演算法</dt><dd>{{ selectedRecord.ruleSnapshot.algorithmVersion }}</dd></div>
              <div><dt>資格規則</dt><dd>{{ selectedRecord.ruleSnapshot.eligibilityPolicyVersion }}</dd></div>
              <div><dt>中獎群組規則</dt><dd>{{ selectedRecord.ruleSnapshot.groupWinnerPolicyVersion }}</dd></div>
              <div><dt>獎項順序規則</dt><dd>{{ selectedRecord.ruleSnapshot.prizeSelectionPolicyVersion }}</dd></div>
              <div><dt>抽獎當下獎項佇列</dt><dd>{{ selectedRecord.ruleSnapshot.prizeQueue.join("、") }}</dd></div>
            </dl>
          </section>
          <section class="lottery-record-roster" aria-label="完整參與名單">
            <h3>抽獎當下完整參與名單</h3>
            <p>依抽獎時的票券順序保存。已得獎者標示為排除；其他票券列為合格候選。</p>
            <div v-if="isDetailLoading && !participantPage" class="lottery-records-state" role="status">正在載入參與名單…</div>
            <div v-else-if="detailError" class="lottery-records-state lottery-records-error" role="alert">
              <span>{{ detailError }}</span>
              <v-btn color="primary" @click="participantPage ? changeParticipantPage(participantPage.page) : openRecordDetails(selectedRecord!)">重新載入</v-btn>
            </div>
            <template v-else-if="participantPage">
              <div class="lottery-roster-table-wrap">
                <table class="lottery-roster-table">
                  <caption class="lottery-records-sr-only">抽獎當下完整參與名單，共 {{ participantPage.totalCount }} 張籤</caption>
                  <thead><tr><th scope="col">資格／得獎籤</th><th scope="col">參加者</th><th scope="col">外部平台</th><th scope="col">外部帳號識別</th><th scope="col">等級／分組／標籤</th><th scope="col">籤 ID</th><th scope="col">群組 ID</th></tr></thead>
                  <tbody>
                    <tr v-for="entry in participantPage.items" :key="entry.participantId">
                      <td>{{ entry.participantId === selectedRecord.winnerParticipantId ? "本次得獎籤" : entry.eligibilityStatus === "eligible" ? "合格候選" : "已得獎排除" }}</td>
                      <td>{{ entry.displayName }}</td>
                      <td>{{ entry.externalIdentity?.platform ?? "—" }}</td>
                      <td><code>{{ entry.externalIdentity?.accountId ?? "—" }}</code></td>
                      <td>{{ [entry.level, entry.group, ...entry.tags].filter(Boolean).join("／") || "—" }}</td>
                      <td><code>{{ entry.participantId }}</code></td>
                      <td><code>{{ entry.lotteryGroupId }}</code></td>
                    </tr>
                  </tbody>
                </table>
              </div>
              <nav class="lottery-records-pagination" aria-label="參與名單分頁">
                <span>第 {{ participantPage.page }} / {{ participantPage.totalPages }} 頁</span><span>共 {{ participantPage.totalCount }} 張籤</span>
                <v-btn variant="outlined" :disabled="participantPage.page <= 1 || isDetailLoading" @click="changeParticipantPage(participantPage.page - 1)">上一頁</v-btn>
                <v-btn variant="outlined" :disabled="participantPage.page >= participantPage.totalPages || isDetailLoading" @click="changeParticipantPage(participantPage.page + 1)">下一頁</v-btn>
              </nav>
            </template>
          </section>
        </v-card-text>
        <v-card-actions><v-spacer /><v-btn variant="text" @click="isDetailOpen = false">關閉</v-btn></v-card-actions>
      </v-card>
    </v-dialog>
  </main>
</template>

<style scoped>
.lottery-records-page { --ink: #10233f; --muted: #607089; display: grid; gap: 18px; padding: 8px 6px 32px; color: var(--ink); }
.lottery-records-hero, .lottery-records-filters, .lottery-records-state, .lottery-records-table-wrap { padding: 20px; border: 1px solid #dce3ec; border-radius: 18px; background: #fff; box-shadow: 0 8px 24px #0f172a0a; }
.lottery-records-hero { display: flex; justify-content: space-between; align-items: center; gap: 20px; background: linear-gradient(135deg, #fffdf8, #f3f6fb); }
.lottery-records-eyebrow { color: #9a3412; font-size: .76rem; font-weight: 700; letter-spacing: .16em; }
h1 { margin: 8px 0; font-size: clamp(1.8rem, 4vw, 2.5rem); }
.lottery-records-hero p, .lottery-records-state p { margin: 0; color: var(--muted); line-height: 1.6; }
.lottery-records-total { display: grid; min-width: 120px; gap: 4px; text-align: center; color: var(--muted); }
.lottery-records-total strong { color: var(--ink); font-size: 2rem; }
.lottery-records-filter-grid { display: grid; grid-template-columns: 2fr 1fr 1fr; gap: 12px; }
.lottery-records-filter-field { display: grid; gap: 7px; }
.lottery-records-filter-field label { color: var(--muted); font-size: .88rem; font-weight: 700; }
.lottery-records-filter-field input { width: 100%; min-width: 0; min-height: 42px; padding: 8px 10px; border: 1px solid #cbd5e1; border-radius: 8px; background: white; color: var(--ink); font: inherit; }
.lottery-records-filter-field input:focus-visible { outline: 3px solid #2563eb55; outline-offset: 2px; }
.lottery-records-filter-actions, .lottery-records-pagination { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; margin-top: 16px; }
.lottery-records-state { display: flex; justify-content: space-between; align-items: center; gap: 16px; min-height: 120px; }
.lottery-records-state h2 { margin: 0 0 6px; font-size: 1.15rem; }
.lottery-records-error { border-color: #fecaca; background: #fffafa; }
.lottery-records-table-wrap { padding: 0; overflow-x: auto; }
.lottery-records-table { width: 100%; border-collapse: collapse; text-align: left; }
.lottery-records-table th, .lottery-records-table td { padding: 13px 14px; border-bottom: 1px solid #e2e8f0; vertical-align: middle; }
.lottery-records-table th { background: #f8fafc; color: #475569; font-size: .84rem; white-space: nowrap; }
.lottery-records-table tbody tr:last-child td { border-bottom: 0; }
.lottery-record-detail { display: grid; gap: 12px; }
.lottery-record-detail > div { display: grid; grid-template-columns: 8rem minmax(0, 1fr); gap: 10px; align-items: start; }
.lottery-record-detail span { color: var(--muted); font-weight: 700; }
.lottery-record-detail code { overflow-wrap: anywhere; }
.lottery-record-rules { margin-top: 20px; padding: 16px; border-radius: 12px; background: #f8fafc; }
.lottery-record-rules h3, .lottery-record-roster h3 { margin: 0 0 10px; font-size: 1rem; }
.lottery-record-rules dl { display: grid; gap: 8px; margin: 0; }
.lottery-record-rules dl > div { display: grid; grid-template-columns: 10rem minmax(0, 1fr); gap: 8px; }
.lottery-record-rules dt { color: var(--muted); font-weight: 700; }
.lottery-record-rules dd { margin: 0; overflow-wrap: anywhere; }
.lottery-record-roster { margin-top: 20px; }
.lottery-record-roster > p { color: var(--muted); }
.lottery-roster-table-wrap { max-height: 55vh; overflow: auto; border: 1px solid #dce3ec; border-radius: 12px; }
.lottery-roster-table { width: 100%; border-collapse: collapse; text-align: left; }
.lottery-roster-table th, .lottery-roster-table td { padding: 9px 10px; border-bottom: 1px solid #e2e8f0; vertical-align: top; }
.lottery-roster-table th { position: sticky; top: 0; background: #f8fafc; color: #475569; font-size: .82rem; }
.lottery-roster-table code { overflow-wrap: anywhere; font-size: .78rem; }
.lottery-records-table code { color: #475569; font-size: .78rem; overflow-wrap: anywhere; }
.lottery-records-pagination { justify-content: flex-end; color: var(--muted); }
.lottery-records-sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
@media (max-width: 800px) { .lottery-records-filter-grid { grid-template-columns: 1fr 1fr; } .lottery-records-filter-field:first-child { grid-column: 1 / -1; } }
@media (max-width: 600px) {
  .lottery-records-hero, .lottery-records-state { align-items: flex-start; flex-direction: column; }
  .lottery-records-filter-grid { grid-template-columns: 1fr; }
  .lottery-records-filter-field:first-child { grid-column: auto; }
  .lottery-records-table-wrap { overflow: visible; padding: 10px; }
  .lottery-records-table, .lottery-records-table tbody, .lottery-records-table tr, .lottery-records-table td { display: block; width: 100%; }
  .lottery-records-table thead { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0, 0, 0, 0); }
  .lottery-records-table tr { padding: 6px 0; border-bottom: 1px solid #cbd5e1; }
  .lottery-records-table tr:last-child { border-bottom: 0; }
  .lottery-records-table td { display: grid; grid-template-columns: 7.2rem minmax(0, 1fr); gap: 8px; padding: 9px 4px; border: 0; overflow-wrap: anywhere; }
  .lottery-records-table td::before { content: attr(data-label); color: var(--muted); font-size: .82rem; font-weight: 700; }
  .lottery-record-rules dl > div { grid-template-columns: 1fr; gap: 2px; }
  .lottery-roster-table-wrap { max-height: 60vh; }
  .lottery-records-pagination { justify-content: flex-start; }
}
</style>
