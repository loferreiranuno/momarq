<script setup lang="ts">
import { ref, onMounted, computed, onUnmounted, watch } from 'vue'
import { api } from '@/api/client'

// Types
interface ProviderDto {
  id: number
  name: string
  websiteUrl?: string
}

interface CrawlJobListItem {
  id: number
  providerId: number
  providerName: string
  status: CrawlJobStatus
  createdAtUtc: string
  startedAtUtc?: string
  finishedAtUtc?: string
  pagesTotal: number
  pagesProcessed: number
  productsExtracted: number
  errorsCount: number
  lastError?: string
}

interface JobsListResponse {
  jobs: CrawlJobListItem[]
  totalCount: number
  page: number
  pageSize: number
}

interface JobStatsResponse {
  totalJobs: number
  queuedJobs: number
  runningJobs: number
  succeededJobs: number
  failedJobs: number
  canceledJobs: number
}

type CrawlJobStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Canceled' | 0 | 1 | 2 | 3 | 4

interface CreateJobForm {
  providerId: number | null
  startUrl: string
  sitemapUrl: string
  maxPages: number | null
}

// State
const jobs = ref<CrawlJobListItem[]>([])
const stats = ref<JobStatsResponse | null>(null)
const providers = ref<ProviderDto[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const currentPage = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const filterStatus = ref<CrawlJobStatus | ''>('')

// Modal state
const showCreateModal = ref(false)
const isCreating = ref(false)
const createForm = ref<CreateJobForm>({
  providerId: null,
  startUrl: '',
  sitemapUrl: '',
  maxPages: null,
})

// Action states
const actionInProgress = ref<number | null>(null)

// SSE
const sseConnection = ref<EventSource | null>(null)
let sseReconnectTimeout: ReturnType<typeof setTimeout> | null = null
let sseConnecting = false
let sseStopped = false

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value))

const statusOptions: { value: CrawlJobStatus | '', label: string }[] = [
  { value: '', label: 'All Statuses' },
  { value: 'Queued', label: 'Queued' },
  { value: 'Running', label: 'Running' },
  { value: 'Succeeded', label: 'Succeeded' },
  { value: 'Failed', label: 'Failed' },
  { value: 'Canceled', label: 'Canceled' },
]

onMounted(async () => {
  await Promise.all([loadJobs(), loadStats(), loadProviders()])
  await connectJobsSse()
})

onUnmounted(() => {
  sseStopped = true
  disconnectJobsSse()
})

watch([currentPage, pageSize, filterStatus], () => {
  restartJobsSse()
})

interface JobsSsePayload {
  jobs: JobsListResponse
  stats: JobStatsResponse
  timestampUtc: string
}

interface CreateSseTicketResponse {
  ticket: string
  expiresAt: string
}

async function connectJobsSse() {
  if (sseStopped || sseConnection.value || sseConnecting) return
  sseConnecting = true
  try {
    const ticket = await mintJobsSseTicket()
    const url = buildJobsSseUrl(ticket)

    const es = new EventSource(url)

    es.addEventListener('jobs-snapshot', (event) => {
      try {
        const payload = JSON.parse((event as MessageEvent).data) as JobsSsePayload
        jobs.value = payload.jobs.jobs
        totalCount.value = payload.jobs.totalCount
        stats.value = payload.stats
      } catch {
        // ignore parse errors
      }
    })

    es.addEventListener('error', () => {
      es.close()
      if (sseConnection.value === es) {
        sseConnection.value = null
      }
      if (!sseStopped) {
        if (sseReconnectTimeout) clearTimeout(sseReconnectTimeout)
        sseReconnectTimeout = setTimeout(() => {
          connectJobsSse()
        }, 5000)
      }
    })

    sseConnection.value = es
  } catch (e) {
    console.error('Failed to connect jobs SSE:', e)
  } finally {
    sseConnecting = false
  }
}

function disconnectJobsSse() {
  if (sseReconnectTimeout) {
    clearTimeout(sseReconnectTimeout)
    sseReconnectTimeout = null
  }
  if (sseConnection.value) {
    sseConnection.value.close()
    sseConnection.value = null
  }
}

async function restartJobsSse() {
  disconnectJobsSse()
  await connectJobsSse()
}

async function mintJobsSseTicket(): Promise<string> {
  const response = await api.post<CreateSseTicketResponse>('/api/auth/sse-ticket', { purpose: 'jobs' })
  return response.ticket
}

function buildJobsSseUrl(ticket: string): string {
  const params = new URLSearchParams({
    ticket,
    page: String(currentPage.value),
    pageSize: String(pageSize.value),
  })
  if (filterStatus.value) {
    params.set('status', String(statusToNumber(filterStatus.value)))
  }
  return `/api/jobs/sse?${params.toString()}`
}

async function loadJobs() {
  isLoading.value = true
  error.value = null
  try {
    const params: Record<string, string | number> = {
      page: currentPage.value,
      pageSize: pageSize.value,
    }
    if (filterStatus.value) {
      params.status = statusToNumber(filterStatus.value)
    }
    const response = await api.get<JobsListResponse>('/api/jobs', params)
    jobs.value = response.jobs
    totalCount.value = response.totalCount
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load jobs'
  } finally {
    isLoading.value = false
  }
}

async function loadStats() {
  try {
    stats.value = await api.get<JobStatsResponse>('/api/jobs/stats')
  } catch (e) {
    console.error('Failed to load job stats:', e)
  }
}

async function loadProviders() {
  try {
    providers.value = await api.get<ProviderDto[]>('/api/admin/providers')
  } catch (e) {
    console.error('Failed to load providers:', e)
  }
}

function statusToNumber(status: CrawlJobStatus): number {
  if (typeof status === 'number') return status
  const map: Record<string, number> = {
    'Queued': 0,
    'Running': 1,
    'Succeeded': 2,
    'Failed': 3,
    'Canceled': 4,
  }
  return map[status] ?? 0
}

function statusToLabel(status: CrawlJobStatus): Exclude<CrawlJobStatus, number> {
  if (typeof status === 'string') return status
  const map: Record<number, Exclude<CrawlJobStatus, number>> = {
    0: 'Queued',
    1: 'Running',
    2: 'Succeeded',
    3: 'Failed',
    4: 'Canceled',
  }
  return map[status] ?? 'Queued'
}

function getStatusClass(status: CrawlJobStatus): string {
  const normalized = statusToLabel(status)
  const classes: Record<Exclude<CrawlJobStatus, number>, string> = {
    'Queued': 'status--queued',
    'Running': 'status--running',
    'Succeeded': 'status--succeeded',
    'Failed': 'status--failed',
    'Canceled': 'status--canceled',
  }
  return classes[normalized] || ''
}

function getStatusLabel(status: CrawlJobStatus): string {
  return statusToLabel(status)
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleString()
}

function formatDuration(startStr?: string, endStr?: string): string {
  if (!startStr || !endStr) return '-'
  const start = new Date(startStr)
  const end = new Date(endStr)
  const diffMs = end.getTime() - start.getTime()
  if (diffMs < 1000) return `${diffMs}ms`
  if (diffMs < 60000) return `${Math.round(diffMs / 1000)}s`
  if (diffMs < 3600000) return `${Math.round(diffMs / 60000)}m`
  return `${Math.round(diffMs / 3600000)}h`
}

function openCreateModal() {
  const firstProvider = providers.value.length > 0 ? providers.value[0] : null
  createForm.value = {
    providerId: firstProvider?.id ?? null,
    startUrl: firstProvider?.websiteUrl ?? '',
    sitemapUrl: '',
    maxPages: null,
  }
  showCreateModal.value = true
}

function onProviderChange() {
  const selectedProvider = providers.value.find(p => p.id === createForm.value.providerId)
  if (selectedProvider?.websiteUrl) {
    createForm.value.startUrl = selectedProvider.websiteUrl
  }
}

function closeCreateModal() {
  showCreateModal.value = false
}

async function createJob() {
  if (!createForm.value.providerId || !createForm.value.startUrl.trim()) {
    return
  }

  isCreating.value = true
  try {
    await api.post('/api/jobs', {
      providerId: createForm.value.providerId,
      startUrl: createForm.value.startUrl.trim(),
      sitemapUrl: createForm.value.sitemapUrl.trim() || undefined,
      maxPages: createForm.value.maxPages || undefined,
    })
    closeCreateModal()
    await Promise.all([loadJobs(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create job'
  } finally {
    isCreating.value = false
  }
}

async function cancelJob(jobId: number) {
  actionInProgress.value = jobId
  try {
    await api.post(`/api/jobs/${jobId}/cancel`)
    await Promise.all([loadJobs(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to cancel job'
  } finally {
    actionInProgress.value = null
  }
}

async function retryJob(jobId: number) {
  actionInProgress.value = jobId
  try {
    await api.post(`/api/jobs/${jobId}/retry`)
    await Promise.all([loadJobs(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to retry job'
  } finally {
    actionInProgress.value = null
  }
}

async function deleteJob(jobId: number) {
  if (!confirm('Are you sure you want to delete this job?')) return
  
  actionInProgress.value = jobId
  try {
    await api.delete(`/api/jobs/${jobId}`)
    await Promise.all([loadJobs(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete job'
  } finally {
    actionInProgress.value = null
  }
}

function handleFilterChange() {
  currentPage.value = 1
  loadJobs()
}

function goToPage(page: number) {
  if (page < 1 || page > totalPages.value) return
  currentPage.value = page
  loadJobs()
}
</script>

<template>
  <div class="admin-jobs">
    <header class="admin-jobs__header">
      <div class="admin-jobs__title-row">
        <div>
          <h1 class="page-title">Crawl Jobs</h1>
          <p class="admin-jobs__subtitle">Manage and monitor product crawling jobs</p>
        </div>
        <button class="btn btn--primary" @click="openCreateModal">
          + New Job
        </button>
      </div>
    </header>

    <!-- Stats Cards -->
    <div v-if="stats" class="admin-jobs__stats">
      <div class="stat-card">
        <span class="stat-card__value">{{ stats.totalJobs }}</span>
        <span class="stat-card__label">Total Jobs</span>
      </div>
      <div class="stat-card stat-card--queued">
        <span class="stat-card__value">{{ stats.queuedJobs }}</span>
        <span class="stat-card__label">Queued</span>
      </div>
      <div class="stat-card stat-card--running">
        <span class="stat-card__value">{{ stats.runningJobs }}</span>
        <span class="stat-card__label">Running</span>
      </div>
      <div class="stat-card stat-card--succeeded">
        <span class="stat-card__value">{{ stats.succeededJobs }}</span>
        <span class="stat-card__label">Succeeded</span>
      </div>
      <div class="stat-card stat-card--failed">
        <span class="stat-card__value">{{ stats.failedJobs }}</span>
        <span class="stat-card__label">Failed</span>
      </div>
    </div>

    <!-- Filters -->
    <div class="admin-jobs__filters card">
      <div class="filter-group">
        <label for="status-filter">Status:</label>
        <select
          id="status-filter"
          v-model="filterStatus"
          class="form-select"
          @change="handleFilterChange"
        >
          <option v-for="opt in statusOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </option>
        </select>
      </div>
      <button class="btn btn--sm btn--outline" @click="loadJobs">
        ↻ Refresh
      </button>
    </div>

    <!-- Error Message -->
    <div v-if="error" class="admin-jobs__error card">
      <p>{{ error }}</p>
      <button class="btn btn--sm btn--outline" @click="loadJobs">Retry</button>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading && jobs.length === 0" class="admin-jobs__loading">
      <span class="spinner"></span>
      <p>Loading jobs...</p>
    </div>

    <!-- Empty State -->
    <div v-else-if="jobs.length === 0" class="admin-jobs__empty card">
      <span class="admin-jobs__empty-icon">🕷️</span>
      <h3>No crawl jobs yet</h3>
      <p>Create your first crawl job to start importing products</p>
      <button class="btn btn--primary" @click="openCreateModal">+ New Job</button>
    </div>

    <!-- Jobs Table -->
    <div v-else class="admin-jobs__table-container card">
      <table class="admin-jobs__table">
        <thead>
          <tr>
            <th>ID</th>
            <th>Provider</th>
            <th>Status</th>
            <th>Progress</th>
            <th>Products</th>
            <th>Errors</th>
            <th>Created</th>
            <th>Duration</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="job in jobs" :key="job.id">
            <td class="admin-jobs__id">#{{ job.id }}</td>
            <td>{{ job.providerName }}</td>
            <td>
              <span class="status-badge" :class="getStatusClass(job.status)">
                {{ getStatusLabel(job.status) }}
              </span>
            </td>
            <td>
              <div class="progress-cell">
                <div class="progress-bar">
                  <div
                    class="progress-bar__fill"
                    :style="{ width: job.pagesTotal > 0 ? `${(job.pagesProcessed / job.pagesTotal) * 100}%` : '0%' }"
                  ></div>
                </div>
                <span class="progress-text">{{ job.pagesProcessed }} / {{ job.pagesTotal }}</span>
              </div>
            </td>
            <td>{{ job.productsExtracted }}</td>
            <td :class="{ 'text-danger': job.errorsCount > 0 }">
              {{ job.errorsCount }}
            </td>
            <td>{{ formatDate(job.createdAtUtc) }}</td>
            <td>{{ formatDuration(job.startedAtUtc, job.finishedAtUtc) }}</td>
            <td class="admin-jobs__actions">
              <button
                v-if="job.status === 'Queued' || job.status === 'Running'"
                class="btn btn--sm btn--danger"
                :disabled="actionInProgress === job.id"
                @click="cancelJob(job.id)"
              >
                Cancel
              </button>
              <button
                v-if="job.status === 'Failed' || job.status === 'Canceled'"
                class="btn btn--sm btn--outline"
                :disabled="actionInProgress === job.id"
                @click="retryJob(job.id)"
              >
                Retry
              </button>
              <button
                v-if="job.status === 'Succeeded' || job.status === 'Failed' || job.status === 'Canceled'"
                class="btn btn--sm btn--ghost"
                :disabled="actionInProgress === job.id"
                @click="deleteJob(job.id)"
              >
                🗑️
              </button>
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="admin-jobs__pagination">
        <button
          class="btn btn--sm btn--outline"
          :disabled="currentPage <= 1"
          @click="goToPage(currentPage - 1)"
        >
          ← Prev
        </button>
        <span class="pagination-info">
          Page {{ currentPage }} of {{ totalPages }}
        </span>
        <button
          class="btn btn--sm btn--outline"
          :disabled="currentPage >= totalPages"
          @click="goToPage(currentPage + 1)"
        >
          Next →
        </button>
      </div>
    </div>

    <!-- Create Job Modal -->
    <Teleport to="body">
      <div v-if="showCreateModal" class="modal-overlay" @click.self="closeCreateModal">
        <div class="modal">
          <div class="modal__header">
            <h2 class="modal__title">New Crawl Job</h2>
            <button class="modal__close" @click="closeCreateModal">×</button>
          </div>
          <form class="modal__body" @submit.prevent="createJob">
            <div class="form-group">
              <label for="provider" class="form-label">Provider *</label>
              <select
                id="provider"
                v-model="createForm.providerId"
                class="form-select"
                required
                @change="onProviderChange"
              >
                <option v-for="p in providers" :key="p.id" :value="p.id">
                  {{ p.name }}
                </option>
              </select>
            </div>

            <div class="form-group">
              <label for="startUrl" class="form-label">Start URL *</label>
              <input
                id="startUrl"
                v-model="createForm.startUrl"
                type="url"
                class="form-input"
                placeholder="https://example.com/products"
                required
              />
              <small class="form-hint">The initial URL to start crawling from</small>
            </div>

            <div class="form-group">
              <label for="sitemapUrl" class="form-label">Sitemap URL</label>
              <input
                id="sitemapUrl"
                v-model="createForm.sitemapUrl"
                type="url"
                class="form-input"
                placeholder="https://example.com/sitemap.xml"
              />
              <small class="form-hint">Optional sitemap for discovering pages</small>
            </div>

            <div class="form-group">
              <label for="maxPages" class="form-label">Max Pages</label>
              <input
                id="maxPages"
                v-model.number="createForm.maxPages"
                type="number"
                class="form-input form-input--sm"
                min="1"
                max="10000"
                placeholder="Unlimited"
              />
              <small class="form-hint">Maximum number of pages to crawl</small>
            </div>

            <div class="modal__actions">
              <button type="button" class="btn btn--outline" @click="closeCreateModal">
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn--primary"
                :disabled="isCreating || !createForm.providerId || !createForm.startUrl"
              >
                {{ isCreating ? 'Creating...' : 'Create Job' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style lang="scss" scoped>
.admin-jobs {
  &__header {
    margin-bottom: var(--space-6);
  }

  &__title-row {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: var(--space-4);
  }

  &__subtitle {
    color: var(--color-text-muted);
    margin-bottom: 0;
  }

  &__stats {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  &__filters {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: var(--space-4);
    padding: var(--space-4);
    margin-bottom: var(--space-6);
  }

  &__error {
    padding: var(--space-4);
    background: var(--color-danger-bg);
    border-color: var(--color-danger);
    margin-bottom: var(--space-4);

    p {
      margin-bottom: var(--space-2);
      color: var(--color-danger);
    }
  }

  &__loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-4);
    padding: var(--space-10);
    color: var(--color-text-muted);
  }

  &__empty {
    text-align: center;
    padding: var(--space-10);
  }

  &__empty-icon {
    font-size: 3rem;
    display: block;
    margin-bottom: var(--space-4);
  }

  &__table-container {
    overflow-x: auto;
  }

  &__table {
    width: 100%;
    border-collapse: collapse;

    th,
    td {
      padding: var(--space-3) var(--space-4);
      text-align: left;
      border-bottom: 1px solid var(--color-border);
    }

    th {
      font-weight: 600;
      background: var(--color-bg-subtle);
      white-space: nowrap;
    }

    tbody tr:hover {
      background: var(--color-bg-hover);
    }
  }

  &__id {
    font-family: var(--font-mono);
    color: var(--color-text-muted);
  }

  &__actions {
    display: flex;
    gap: var(--space-2);
  }

  &__pagination {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: var(--space-4);
    padding: var(--space-4);
    border-top: 1px solid var(--color-border);
  }
}

.stat-card {
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-4);
  text-align: center;

  &__value {
    display: block;
    font-size: var(--text-2xl);
    font-weight: 700;
    margin-bottom: var(--space-1);
  }

  &__label {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
  }

  &--queued &__value {
    color: var(--color-info);
  }

  &--running &__value {
    color: var(--color-warning);
  }

  &--succeeded &__value {
    color: var(--color-success);
  }

  &--failed &__value {
    color: var(--color-danger);
  }
}

.filter-group {
  display: flex;
  align-items: center;
  gap: var(--space-2);

  label {
    font-weight: 500;
  }
}

.status-badge {
  display: inline-block;
  padding: var(--space-1) var(--space-2);
  border-radius: var(--radius-sm);
  font-size: var(--text-xs);
  font-weight: 600;
  text-transform: uppercase;
}

.status--queued {
  background: var(--color-info-bg, #e0f2fe);
  color: var(--color-info, #0284c7);
}

.status--running {
  background: var(--color-warning-bg, #fef3c7);
  color: var(--color-warning, #d97706);
}

.status--succeeded {
  background: var(--color-success-bg, #dcfce7);
  color: var(--color-success, #16a34a);
}

.status--failed {
  background: var(--color-danger-bg, #fee2e2);
  color: var(--color-danger, #dc2626);
}

.status--canceled {
  background: var(--color-muted-bg, #f3f4f6);
  color: var(--color-text-muted, #6b7280);
}

.progress-cell {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  min-width: 120px;
}

.progress-bar {
  flex: 1;
  height: 8px;
  background: var(--color-border);
  border-radius: var(--radius-full);
  overflow: hidden;

  &__fill {
    height: 100%;
    background: var(--color-primary);
    transition: width 0.3s ease;
  }
}

.progress-text {
  font-size: var(--text-xs);
  color: var(--color-text-muted);
  white-space: nowrap;
}

.pagination-info {
  color: var(--color-text-muted);
}

.text-danger {
  color: var(--color-danger);
  font-weight: 600;
}

// Modal styles
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--color-background);
  border-radius: var(--radius-lg);
  width: 100%;
  max-width: 500px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: var(--shadow-lg);

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: var(--space-4) var(--space-6);
    border-bottom: 1px solid var(--color-border);
  }

  &__title {
    font-size: var(--text-lg);
    font-weight: 600;
    margin: 0;
  }

  &__close {
    background: none;
    border: none;
    font-size: var(--text-2xl);
    cursor: pointer;
    color: var(--color-text-muted);
    line-height: 1;

    &:hover {
      color: var(--color-text-primary);
    }
  }

  &__body {
    padding: var(--space-6);
  }

  &__actions {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-3);
    margin-top: var(--space-6);
    padding-top: var(--space-4);
    border-top: 1px solid var(--color-border);
  }
}

.form-group {
  margin-bottom: var(--space-4);
}

.form-label {
  display: block;
  font-weight: 500;
  margin-bottom: var(--space-1);
}

.form-hint {
  display: block;
  font-size: var(--text-xs);
  color: var(--color-text-muted);
  margin-top: var(--space-1);
}

.form-select {
  display: block;
  width: 100%;
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-size: var(--text-sm);

  &:focus {
    outline: none;
    border-color: var(--color-primary);
    box-shadow: 0 0 0 2px var(--color-primary-light);
  }
}

.form-input {
  display: block;
  width: 100%;
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  font-size: var(--text-sm);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);

  &:focus {
    outline: none;
    border-color: var(--color-accent);
    box-shadow: 0 0 0 2px var(--color-primary-light);
  }

  &--sm {
    width: 140px;
  }
}
</style>
