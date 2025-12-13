<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { api } from '@/api/client'
import AdminPageHeader from '@/components/admin/AdminPageHeader.vue'
import LoadingState from '@/components/admin/LoadingState.vue'
import EmptyState from '@/components/admin/EmptyState.vue'
import ErrorState from '@/components/admin/ErrorState.vue'

// Types
interface ExtractedProductDto {
  id: number
  crawlJobId: number
  providerId: number
  providerName: string
  externalId?: string
  name?: string
  description?: string
  price?: number
  currency?: string
  productUrl?: string
  imageUrls: string[]
  status: ExtractedProductStatus
  importedProductId?: number
  reviewedAt?: string
  createdAt: string
}

type ExtractedProductStatus = 'Pending' | 'Approved' | 'Rejected' | 'Duplicate' | 0 | 1 | 2 | 3

interface ExtractedProductsPagedResult {
  items: ExtractedProductDto[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

interface ExtractedProductsStatsDto {
  totalCount: number
  pendingCount: number
  approvedCount: number
  rejectedCount: number
  duplicateCount: number
}

interface ProviderDto {
  id: number
  name: string
}

interface CategoryDto {
  id: number
  name: string
}

// State
const products = ref<ExtractedProductDto[]>([])
const stats = ref<ExtractedProductsStatsDto | null>(null)
const providers = ref<ProviderDto[]>([])
const categories = ref<CategoryDto[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const currentPage = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const totalPages = ref(0)

// Filters
const filterStatus = ref<ExtractedProductStatus | ''>('')
const filterProviderId = ref<number | ''>('')
const searchQuery = ref('')

// Selection
const selectedIds = ref<Set<number>>(new Set())
const selectAll = ref(false)

// Action states
const actionInProgress = ref<number | null>(null)
const bulkActionInProgress = ref(false)

// Modal state
const showApproveModal = ref(false)
const approveProductId = ref<number | null>(null)
const selectedCategoryId = ref<number | null>(null)

const statusOptions: { value: ExtractedProductStatus | '', label: string }[] = [
  { value: '', label: 'All Statuses' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Duplicate', label: 'Duplicate' },
]

onMounted(async () => {
  await Promise.all([loadProducts(), loadStats(), loadProviders(), loadCategories()])
})

watch([currentPage, pageSize], () => {
  loadProducts()
})

async function loadProducts() {
  isLoading.value = true
  error.value = null
  try {
    const params: Record<string, string | number> = {
      page: currentPage.value,
      pageSize: pageSize.value,
    }
    if (filterStatus.value !== '') {
      params.status = statusToNumber(filterStatus.value)
    }
    if (filterProviderId.value !== '') {
      params.providerId = filterProviderId.value
    }
    if (searchQuery.value.trim()) {
      params.search = searchQuery.value.trim()
    }
    const response = await api.get<ExtractedProductsPagedResult>('/api/import', params)
    products.value = response.items
    totalCount.value = response.totalCount
    totalPages.value = response.totalPages
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load products'
  } finally {
    isLoading.value = false
  }
}

async function loadStats() {
  try {
    stats.value = await api.get<ExtractedProductsStatsDto>('/api/import/stats')
  } catch (e) {
    console.error('Failed to load stats:', e)
  }
}

async function loadProviders() {
  try {
    providers.value = await api.get<ProviderDto[]>('/api/admin/providers')
  } catch (e) {
    console.error('Failed to load providers:', e)
  }
}

async function loadCategories() {
  try {
    categories.value = await api.get<CategoryDto[]>('/api/categories')
  } catch (e) {
    console.error('Failed to load categories:', e)
  }
}

function statusToNumber(status: ExtractedProductStatus): number {
  if (typeof status === 'number') return status
  const map: Record<string, number> = {
    'Pending': 0,
    'Approved': 1,
    'Rejected': 2,
    'Duplicate': 3,
  }
  return map[status] ?? 0
}

function statusToLabel(status: ExtractedProductStatus): Exclude<ExtractedProductStatus, number> {
  if (typeof status === 'string') return status
  const map: Record<number, Exclude<ExtractedProductStatus, number>> = {
    0: 'Pending',
    1: 'Approved',
    2: 'Rejected',
    3: 'Duplicate',
  }
  return map[status] ?? 'Pending'
}

function getStatusClass(status: ExtractedProductStatus): string {
  const normalized = statusToLabel(status)
  const classes: Record<Exclude<ExtractedProductStatus, number>, string> = {
    'Pending': 'status--pending',
    'Approved': 'status--approved',
    'Rejected': 'status--rejected',
    'Duplicate': 'status--duplicate',
  }
  return classes[normalized] || ''
}

function formatDate(dateStr?: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleString()
}

function formatPrice(price?: number, currency?: string): string {
  if (price === undefined || price === null) return '-'
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency || 'EUR',
  }).format(price)
}

function handleFilterChange() {
  currentPage.value = 1
  loadProducts()
}

function goToPage(page: number) {
  if (page < 1 || page > totalPages.value) return
  currentPage.value = page
}

function openApproveModal(productId: number) {
  approveProductId.value = productId
  selectedCategoryId.value = null
  showApproveModal.value = true
}

function closeApproveModal() {
  showApproveModal.value = false
  approveProductId.value = null
}

async function approveProduct() {
  if (!approveProductId.value) return
  
  actionInProgress.value = approveProductId.value
  try {
    await api.post(`/api/import/${approveProductId.value}/approve`, {
      categoryId: selectedCategoryId.value,
    })
    closeApproveModal()
    await Promise.all([loadProducts(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to approve product'
  } finally {
    actionInProgress.value = null
  }
}

async function rejectProduct(productId: number) {
  actionInProgress.value = productId
  try {
    await api.post(`/api/import/${productId}/reject`)
    await Promise.all([loadProducts(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to reject product'
  } finally {
    actionInProgress.value = null
  }
}

async function resetProduct(productId: number) {
  actionInProgress.value = productId
  try {
    await api.post(`/api/import/${productId}/reset`)
    await Promise.all([loadProducts(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to reset product'
  } finally {
    actionInProgress.value = null
  }
}

function toggleSelection(productId: number) {
  if (selectedIds.value.has(productId)) {
    selectedIds.value.delete(productId)
  } else {
    selectedIds.value.add(productId)
  }
  selectedIds.value = new Set(selectedIds.value)
  updateSelectAll()
}

function toggleSelectAll() {
  if (selectAll.value) {
    selectedIds.value = new Set()
    selectAll.value = false
  } else {
    const pendingProducts = products.value.filter(p => statusToLabel(p.status) === 'Pending')
    selectedIds.value = new Set(pendingProducts.map(p => p.id))
    selectAll.value = true
  }
}

function updateSelectAll() {
  const pendingProducts = products.value.filter(p => statusToLabel(p.status) === 'Pending')
  selectAll.value = pendingProducts.length > 0 && pendingProducts.every(p => selectedIds.value.has(p.id))
}

const hasSelection = computed(() => selectedIds.value.size > 0)

async function bulkApprove() {
  if (!hasSelection.value) return
  
  bulkActionInProgress.value = true
  try {
    await api.post('/api/import/bulk-approve', {
      ids: Array.from(selectedIds.value),
      categoryId: selectedCategoryId.value,
    })
    selectedIds.value = new Set()
    selectAll.value = false
    await Promise.all([loadProducts(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to bulk approve'
  } finally {
    bulkActionInProgress.value = false
  }
}

async function bulkReject() {
  if (!hasSelection.value) return
  
  bulkActionInProgress.value = true
  try {
    await api.post('/api/import/bulk-reject', {
      ids: Array.from(selectedIds.value),
    })
    selectedIds.value = new Set()
    selectAll.value = false
    await Promise.all([loadProducts(), loadStats()])
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to bulk reject'
  } finally {
    bulkActionInProgress.value = false
  }
}
</script>

<template>
  <div class="admin-import">
    <AdminPageHeader
      title="Product Import"
      subtitle="Review and import extracted products from crawl jobs"
    />

    <!-- Stats Cards -->
    <div v-if="stats" class="admin-import__stats">
      <div class="stat-card">
        <span class="stat-card__value">{{ stats.totalCount }}</span>
        <span class="stat-card__label">Total Extracted</span>
      </div>
      <div class="stat-card stat-card--pending">
        <span class="stat-card__value">{{ stats.pendingCount }}</span>
        <span class="stat-card__label">Pending Review</span>
      </div>
      <div class="stat-card stat-card--approved">
        <span class="stat-card__value">{{ stats.approvedCount }}</span>
        <span class="stat-card__label">Approved</span>
      </div>
      <div class="stat-card stat-card--rejected">
        <span class="stat-card__value">{{ stats.rejectedCount }}</span>
        <span class="stat-card__label">Rejected</span>
      </div>
      <div class="stat-card stat-card--duplicate">
        <span class="stat-card__value">{{ stats.duplicateCount }}</span>
        <span class="stat-card__label">Duplicates</span>
      </div>
    </div>

    <!-- Filters -->
    <div class="admin-import__filters card">
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
      <div class="filter-group">
        <label for="provider-filter">Provider:</label>
        <select
          id="provider-filter"
          v-model="filterProviderId"
          class="form-select"
          @change="handleFilterChange"
        >
          <option value="">All Providers</option>
          <option v-for="p in providers" :key="p.id" :value="p.id">{{ p.name }}</option>
        </select>
      </div>
      <div class="filter-group">
        <label for="search">Search:</label>
        <input
          id="search"
          v-model="searchQuery"
          type="text"
          class="form-input"
          placeholder="Name or External ID"
          @keyup.enter="handleFilterChange"
        />
      </div>
      <button class="btn btn--sm btn--secondary" @click="handleFilterChange">
        Search
      </button>
    </div>

    <!-- Bulk Actions -->
    <div v-if="hasSelection" class="admin-import__bulk-actions card">
      <span class="bulk-count">{{ selectedIds.size }} selected</span>
      <div class="filter-group">
        <label for="bulk-category">Category:</label>
        <select id="bulk-category" v-model="selectedCategoryId" class="form-select">
          <option :value="null">No Category</option>
          <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </div>
      <button
        class="btn btn--sm btn--success"
        :disabled="bulkActionInProgress"
        @click="bulkApprove"
      >
        Approve Selected
      </button>
      <button
        class="btn btn--sm btn--danger"
        :disabled="bulkActionInProgress"
        @click="bulkReject"
      >
        Reject Selected
      </button>
    </div>

    <!-- Error Message --> 
    <ErrorState v-if="error" :message="error" @retry="loadProducts" />

    <!-- Loading State -->
    <LoadingState v-if="isLoading && products.length === 0" message="Loading products..." />

    <!-- Empty State -->
    <EmptyState
      v-else-if="products.length === 0"
      icon="📦"
      title="No extracted products"
      message="Products extracted from crawl jobs will appear here for review"
    />

    <!-- Products Table -->
    <div v-else class="admin-import__table-container card">
      <table class="admin-import__table">
        <thead>
          <tr>
            <th class="col-checkbox">
              <input
                type="checkbox"
                :checked="selectAll"
                @change="toggleSelectAll"
              />
            </th>
            <th>Image</th>
            <th>Name</th>
            <th>External ID</th>
            <th>Provider</th>
            <th>Price</th>
            <th>Status</th>
            <th>Extracted</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="product in products" :key="product.id">
            <td class="col-checkbox">
              <input
                v-if="statusToLabel(product.status) === 'Pending'"
                type="checkbox"
                :checked="selectedIds.has(product.id)"
                @change="toggleSelection(product.id)"
              />
            </td>
            <td class="col-image">
              <img
                v-if="product.imageUrls.length > 0"
                :src="product.imageUrls[0]"
                :alt="product.name || 'Product'"
                class="product-thumbnail"
              />
              <span v-else class="no-image">📷</span>
            </td>
            <td class="col-name">
              <div class="product-name">{{ product.name || 'Unknown' }}</div>
              <a v-if="product.productUrl" :href="product.productUrl" target="_blank" class="product-link">
                View →
              </a>
            </td>
            <td>{{ product.externalId || '-' }}</td>
            <td>{{ product.providerName }}</td>
            <td>{{ formatPrice(product.price, product.currency) }}</td>
            <td>
              <span class="status-badge" :class="getStatusClass(product.status)">
                {{ statusToLabel(product.status) }}
              </span>
              <router-link
                v-if="product.importedProductId"
                :to="`/admin/products/${product.importedProductId}/edit`"
                class="imported-link"
              >
                #{{ product.importedProductId }}
              </router-link>
            </td>
            <td>{{ formatDate(product.createdAt) }}</td>
            <td class="col-actions">
              <div class="actions-wrapper">
                <template v-if="statusToLabel(product.status) === 'Pending'">
                  <button
                    class="btn btn--sm btn--success"
                    title="Approve"
                    :disabled="actionInProgress === product.id"
                    @click="openApproveModal(product.id)"
                  >
                    Approve
                  </button>
                  <button
                    class="btn btn--sm btn--danger"
                    title="Reject"
                    :disabled="actionInProgress === product.id"
                    @click="rejectProduct(product.id)"
                  >
                    Reject
                  </button>
                </template>
                <button
                  v-else
                  class="btn btn--sm btn--outline"
                  title="Reset to Pending"
                  :disabled="actionInProgress === product.id"
                  @click="resetProduct(product.id)"
                >
                  Reset
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="admin-import__pagination">
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

    <!-- Approve Modal -->
    <Teleport to="body">
      <div v-if="showApproveModal" class="import-modal-overlay" @click.self="closeApproveModal">
        <div class="import-modal">
          <div class="import-modal__header">
            <h2 class="import-modal__title">Approve Product</h2>
            <button class="import-modal__close" @click="closeApproveModal">&times;</button>
          </div>
          <div class="import-modal__body">
            <div class="form-group">
              <label for="approve-category" class="form-label">Category (Optional)</label>
              <select id="approve-category" v-model="selectedCategoryId" class="form-select">
                <option :value="null">No Category</option>
                <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
              <small class="form-hint">Assign a category to the imported product</small>
            </div>
            <div class="import-modal__actions">
              <button type="button" class="btn btn--outline" @click="closeApproveModal">
                Cancel
              </button>
              <button
                type="button"
                class="btn btn--success"
                :disabled="actionInProgress !== null"
                @click="approveProduct"
              >
                Approve & Import
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style lang="scss">
.admin-import {
  &__stats {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
    gap: var(--space-4);
    margin-bottom: var(--space-6);
  }

  &__filters {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-4);
    align-items: flex-end;
    padding: var(--space-4);
    margin-bottom: var(--space-6);
  }

  &__bulk-actions {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-4);
    align-items: center;
    padding: var(--space-4);
    margin-bottom: var(--space-4);
    background: var(--color-info-light);
    border: 1px solid var(--color-info);
  }

  &__table-container {
    overflow-x: auto;
  }

  &__table {
    width: 100%;
    border-collapse: collapse;

    th,
    td {
      padding: var(--space-3);
      text-align: left;
      border-bottom: 1px solid var(--color-border);
    }

    th {
      font-weight: 600;
      background: var(--color-bg-secondary);
    }
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

.filter-group {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);

  label {
    font-size: 0.875rem;
    font-weight: 500;
    color: var(--color-text-secondary);
  }
}

.stat-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-4);
  background: var(--color-bg-secondary);
  border-radius: var(--radius-md);
  text-align: center;

  &__value {
    font-size: 2rem;
    font-weight: 700;
    line-height: 1;
  }

  &__label {
    font-size: 0.875rem;
    color: var(--color-text-secondary);
  }

  &--pending {
    background: rgba(59, 130, 246, 0.1);
    .stat-card__value { color: var(--color-info); }
  }

  &--approved {
    background: rgba(34, 197, 94, 0.1);
    .stat-card__value { color: var(--color-success); }
  }

  &--rejected {
    background: rgba(239, 68, 68, 0.1);
    .stat-card__value { color: var(--color-danger); }
  }

  &--duplicate {
    background: rgba(156, 163, 175, 0.1);
    .stat-card__value { color: var(--color-text-secondary); }
  }
}

.status-badge {
  display: inline-block;
  padding: 0.25rem 0.5rem;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  border-radius: var(--radius-sm);

  &.status--pending {
    background: rgba(59, 130, 246, 0.1);
    color: var(--color-info);
  }

  &.status--approved {
    background: rgba(34, 197, 94, 0.1);
    color: var(--color-success);
  }

  &.status--rejected {
    background: rgba(239, 68, 68, 0.1);
    color: var(--color-danger);
  }

  &.status--duplicate {
    background: rgba(156, 163, 175, 0.1);
    color: var(--color-text-secondary);
  }
}

.col-checkbox {
  width: 40px;
  text-align: center;
}

.col-image {
  width: 60px;
}

.col-name {
  max-width: 250px;
}

.col-actions {
  width: 100px;
}

.product-thumbnail {
  width: 50px;
  height: 50px;
  object-fit: cover;
  border-radius: var(--radius-sm);
}

.no-image {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 50px;
  height: 50px;
  background: var(--color-bg-secondary);
  border-radius: var(--radius-sm);
  font-size: 1.5rem;
}

.product-name {
  font-weight: 500;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-link {
  font-size: 0.75rem;
  color: var(--color-primary);
}

.imported-link {
  display: block;
  font-size: 0.75rem;
  color: var(--color-success);
}

.actions-wrapper {
  display: flex;
  gap: var(--space-2);
}

.bulk-count {
  font-weight: 600;
  color: var(--color-info);
}

.pagination-info {
  color: var(--color-text-secondary);
}

.import-modal-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.5);
  z-index: 1000;
}

.import-modal {
  background: var(--color-surface);
  border-radius: var(--radius-lg);
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
  max-width: 400px;
  width: 90%;

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--space-4);
    border-bottom: 1px solid var(--color-border);
  }

  &__title {
    font-size: 1.25rem;
    font-weight: 600;
    margin: 0;
  }

  &__close {
    font-size: 1.5rem;
    line-height: 1;
    background: none;
    border: none;
    cursor: pointer;
    color: var(--color-text-secondary);

    &:hover {
      color: var(--color-text);
    }
  }

  &__body {
    padding: var(--space-4);
  }

  &__actions {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-3);
    margin-top: var(--space-4);
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
  font-size: 0.875rem;
  color: var(--color-text-secondary);
  margin-top: var(--space-1);
}

.form-select,
.form-input {
  padding: var(--space-2) var(--space-3);
  font-size: 0.875rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background-color: var(--color-surface);
  color: var(--color-text);
  min-width: 150px;

  &:focus {
    outline: none;
    border-color: var(--color-primary);
  }
}

.form-select {
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%236b7280' d='M3 4.5L6 7.5L9 4.5'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 0.75rem center;
  padding-right: 2rem;
  cursor: pointer;
}
</style>
