<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '@/api/client'

interface ProductImageDto {
  id: number
  imageUrl: string
  isPrimary: boolean
  hasEmbedding: boolean
  createdAt: string
}

interface ProductDto {
  id: number
  providerId: number
  providerName: string
  externalId?: string
  name: string
  description?: string
  price: number
  currency?: string
  category?: string
  productUrl?: string
  createdAt: string
  images: ProductImageDto[]
}

interface ProviderDto {
  id: number
  name: string
}

interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

const router = useRouter()

// Data
const products = ref<ProductDto[]>([])
const providers = ref<ProviderDto[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)

// Pagination
const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const totalPages = ref(0)

// Filters
const searchQuery = ref('')
const selectedProviderId = ref<number | null>(null)
const selectedCategory = ref('')
const vectorizedOnly = ref<boolean | null>(null)

// Vectorization
const isVectorizing = ref(false)
const vectorizeProgress = ref<string | null>(null)

// Delete
const showDeleteConfirm = ref(false)
const deletingProduct = ref<ProductDto | null>(null)
const isDeleting = ref(false)

const hasFilters = computed(
  () => searchQuery.value || selectedProviderId.value || selectedCategory.value || vectorizedOnly.value !== null
)

onMounted(async () => {
  await Promise.all([loadProducts(), loadProviders()])
})

watch([page], () => {
  loadProducts()
})

async function loadProducts() {
  isLoading.value = true
  error.value = null
  try {
    const params: Record<string, string | number | boolean> = {
      page: page.value,
      pageSize: pageSize.value,
    }
    if (searchQuery.value) params.search = searchQuery.value
    if (selectedProviderId.value) params.providerId = selectedProviderId.value
    if (selectedCategory.value) params.category = selectedCategory.value
    if (vectorizedOnly.value !== null) params.vectorizedOnly = vectorizedOnly.value

    const result = await api.get<PagedResult<ProductDto>>('/api/admin/products', params)
    products.value = result.items
    totalCount.value = result.totalCount
    totalPages.value = result.totalPages
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load products'
    console.error('Failed to load products:', e)
  } finally {
    isLoading.value = false
  }
}

async function loadProviders() {
  try {
    providers.value = await api.get<ProviderDto[]>('/api/admin/providers')
  } catch (e) {
    console.error('Failed to load providers:', e)
  }
}

function applyFilters() {
  page.value = 1
  loadProducts()
}

function clearFilters() {
  searchQuery.value = ''
  selectedProviderId.value = null
  selectedCategory.value = ''
  vectorizedOnly.value = null
  page.value = 1
  loadProducts()
}

function goToPage(p: number) {
  if (p >= 1 && p <= totalPages.value) {
    page.value = p
  }
}

function editProduct(product: ProductDto) {
  router.push({ name: 'admin-product-edit', params: { id: product.id } })
}

function addProduct() {
  router.push({ name: 'admin-product-new' })
}

function confirmDelete(product: ProductDto) {
  deletingProduct.value = product
  showDeleteConfirm.value = true
}

function cancelDelete() {
  showDeleteConfirm.value = false
  deletingProduct.value = null
}

async function deleteProduct() {
  if (!deletingProduct.value) return

  isDeleting.value = true
  try {
    await api.delete(`/api/admin/products/${deletingProduct.value.id}`)
    showDeleteConfirm.value = false
    deletingProduct.value = null
    await loadProducts()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete product'
  } finally {
    isDeleting.value = false
  }
}

async function vectorizeProduct(product: ProductDto) {
  try {
    await api.post(`/api/admin/products/${product.id}/vectorize`)
    await loadProducts()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to vectorize product'
  }
}

async function vectorizeAll() {
  isVectorizing.value = true
  vectorizeProgress.value = 'Starting vectorization...'
  try {
    await api.post('/api/admin/vectorize-all', { forceRegenerate: false })
    vectorizeProgress.value = 'Vectorization complete!'
    await loadProducts()
    setTimeout(() => {
      vectorizeProgress.value = null
    }, 3000)
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to vectorize products'
    vectorizeProgress.value = null
  } finally {
    isVectorizing.value = false
  }
}

function getPrimaryImage(product: ProductDto): string | null {
  const primary = product.images.find((i) => i.isPrimary)
  return primary?.imageUrl ?? product.images[0]?.imageUrl ?? null
}

function getVectorizationStatus(product: ProductDto): { label: string; class: string } {
  const total = product.images.length
  if (total === 0) return { label: 'No images', class: 'status--empty' }
  
  const vectorized = product.images.filter((i) => i.hasEmbedding).length
  if (vectorized === total) return { label: `${vectorized}/${total}`, class: 'status--complete' }
  if (vectorized > 0) return { label: `${vectorized}/${total}`, class: 'status--partial' }
  return { label: `0/${total}`, class: 'status--none' }
}

function formatPrice(price: number, currency?: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: currency ?? 'USD',
  }).format(price)
}
</script>

<template>
  <div class="admin-products">
    <header class="admin-products__header">
      <div class="admin-products__title-row">
        <div>
          <h1 class="page-title">Products</h1>
          <p class="admin-products__subtitle">
            {{ totalCount.toLocaleString() }} products total
          </p>
        </div>
        <div class="admin-products__actions">
          <button
            class="btn btn--secondary"
            :disabled="isVectorizing"
            @click="vectorizeAll"
          >
            <span v-if="isVectorizing" class="spinner spinner--sm"></span>
            {{ isVectorizing ? 'Vectorizing...' : '🔄 Vectorize All' }}
          </button>
          <button class="btn btn--primary" @click="addProduct">
            + Add Product
          </button>
        </div>
      </div>

      <div v-if="vectorizeProgress" class="admin-products__progress">
        {{ vectorizeProgress }}
      </div>
    </header>

    <!-- Filters -->
    <div class="admin-products__filters card">
      <div class="admin-products__filter-row">
        <input
          v-model="searchQuery"
          type="search"
          class="input"
          placeholder="Search products..."
          @keyup.enter="applyFilters"
        />
        <select v-model="selectedProviderId" class="input">
          <option :value="null">All Providers</option>
          <option v-for="provider in providers" :key="provider.id" :value="provider.id">
            {{ provider.name }}
          </option>
        </select>
        <select v-model="vectorizedOnly" class="input">
          <option :value="null">All Status</option>
          <option :value="true">Vectorized</option>
          <option :value="false">Not Vectorized</option>
        </select>
        <button class="btn btn--primary" @click="applyFilters">Filter</button>
        <button v-if="hasFilters" class="btn btn--ghost" @click="clearFilters">Clear</button>
      </div>
    </div>

    <div v-if="error" class="admin-products__error card">
      <p>{{ error }}</p>
      <button class="btn btn--sm btn--outline" @click="loadProducts">Retry</button>
    </div>

    <div v-if="isLoading" class="admin-products__loading">
      <span class="spinner"></span>
      <p>Loading products...</p>
    </div>

    <template v-else-if="products.length === 0">
      <div class="admin-products__empty card">
        <span class="admin-products__empty-icon">📦</span>
        <h3>No products found</h3>
        <p v-if="hasFilters">Try adjusting your filters</p>
        <p v-else>Add your first product to get started</p>
        <button class="btn btn--primary" @click="addProduct">+ Add Product</button>
      </div>
    </template>

    <template v-else>
      <div class="admin-products__grid">
        <div
          v-for="product in products"
          :key="product.id"
          class="product-card card card--hoverable"
          @click="editProduct(product)"
        >
          <div class="product-card__image">
            <img
              v-if="getPrimaryImage(product)"
              :src="getPrimaryImage(product)!"
              :alt="product.name"
            />
            <span v-else class="product-card__no-image">📦</span>
          </div>
          <div class="product-card__content">
            <span class="product-card__provider">{{ product.providerName }}</span>
            <h3 class="product-card__name">{{ product.name }}</h3>
            <p class="product-card__price">{{ formatPrice(product.price, product.currency) }}</p>
            <div class="product-card__meta">
              <span
                class="product-card__status"
                :class="getVectorizationStatus(product).class"
              >
                {{ getVectorizationStatus(product).label }}
              </span>
              <span v-if="product.category" class="product-card__category">
                {{ product.category }}
              </span>
            </div>
          </div>
          <div class="product-card__actions" @click.stop>
            <button
              class="btn btn--sm btn--ghost"
              title="Vectorize"
              @click="vectorizeProduct(product)"
            >
              🔄
            </button>
            <button
              class="btn btn--sm btn--ghost"
              title="Edit"
              @click="editProduct(product)"
            >
              ✏️
            </button>
            <button
              class="btn btn--sm btn--ghost"
              title="Delete"
              @click="confirmDelete(product)"
            >
              🗑️
            </button>
          </div>
        </div>
      </div>

      <!-- Pagination -->
      <div v-if="totalPages > 1" class="admin-products__pagination">
        <button
          class="btn btn--sm btn--secondary"
          :disabled="page === 1"
          @click="goToPage(page - 1)"
        >
          ← Previous
        </button>
        <span class="admin-products__page-info">
          Page {{ page }} of {{ totalPages }}
        </span>
        <button
          class="btn btn--sm btn--secondary"
          :disabled="page === totalPages"
          @click="goToPage(page + 1)"
        >
          Next →
        </button>
      </div>
    </template>

    <!-- Delete Confirmation Modal -->
    <Teleport to="body">
      <div v-if="showDeleteConfirm" class="modal-overlay" @click.self="cancelDelete">
        <div class="modal modal--sm">
          <div class="modal__header">
            <h2 class="modal__title">Delete Product</h2>
          </div>
          <div class="modal__body">
            <p>
              Are you sure you want to delete <strong>{{ deletingProduct?.name }}</strong>?
            </p>
            <p class="text-muted">This will also delete all associated images. This action cannot be undone.</p>
          </div>
          <div class="modal__footer">
            <button class="btn btn--secondary" @click="cancelDelete">Cancel</button>
            <button
              class="btn btn--danger"
              :disabled="isDeleting"
              @click="deleteProduct"
            >
              <span v-if="isDeleting" class="spinner spinner--sm"></span>
              {{ isDeleting ? 'Deleting...' : 'Delete' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style lang="scss" scoped>
.admin-products {
  &__header {
    margin-bottom: var(--space-6);
  }

  &__title-row {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: var(--space-4);
    flex-wrap: wrap;
  }

  &__subtitle {
    color: var(--color-text-muted);
    margin-bottom: 0;
  }

  &__actions {
    display: flex;
    gap: var(--space-3);
  }

  &__progress {
    margin-top: var(--space-4);
    padding: var(--space-3) var(--space-4);
    background: var(--color-accent);
    color: white;
    border-radius: var(--radius-md);
    font-size: var(--text-sm);
  }

  &__filters {
    padding: var(--space-4);
    margin-bottom: var(--space-6);
  }

  &__filter-row {
    display: flex;
    gap: var(--space-3);
    flex-wrap: wrap;

    .input {
      flex: 1;
      min-width: 150px;
      max-width: 250px;
    }
  }

  &__loading,
  &__empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: var(--space-16);
    text-align: center;
  }

  &__empty-icon {
    font-size: 3rem;
    margin-bottom: var(--space-4);
  }

  &__error {
    background: var(--color-error-bg, #fef2f2);
    border-color: var(--color-error, #dc2626);
    color: var(--color-error, #dc2626);
    padding: var(--space-4);
    margin-bottom: var(--space-4);
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  &__grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: var(--space-6);
  }

  &__pagination {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: var(--space-4);
    margin-top: var(--space-8);
  }

  &__page-info {
    color: var(--color-text-muted);
    font-size: var(--text-sm);
  }
}

.product-card {
  display: flex;
  flex-direction: column;
  cursor: pointer;
  overflow: hidden;

  &__image {
    aspect-ratio: 1;
    background: var(--color-surface);
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;

    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
  }

  &__no-image {
    font-size: 3rem;
    opacity: 0.3;
  }

  &__content {
    padding: var(--space-4);
    flex: 1;
  }

  &__provider {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  &__name {
    font-size: var(--text-base);
    font-weight: 600;
    margin: var(--space-1) 0;
    line-height: 1.3;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  &__price {
    font-size: var(--text-lg);
    font-weight: 600;
    color: var(--color-primary);
    margin: var(--space-2) 0;
  }

  &__meta {
    display: flex;
    gap: var(--space-2);
    flex-wrap: wrap;
    margin-top: var(--space-2);
  }

  &__status {
    font-size: var(--text-xs);
    padding: 2px 8px;
    border-radius: var(--radius-full, 9999px);
    font-weight: 500;

    &.status--complete {
      background: #dcfce7;
      color: #166534;
    }

    &.status--partial {
      background: #fef3c7;
      color: #92400e;
    }

    &.status--none {
      background: #fee2e2;
      color: #dc2626;
    }

    &.status--empty {
      background: var(--color-surface);
      color: var(--color-text-muted);
    }
  }

  &__category {
    font-size: var(--text-xs);
    padding: 2px 8px;
    background: var(--color-surface);
    border-radius: var(--radius-full, 9999px);
    color: var(--color-text-muted);
  }

  &__actions {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-1);
    padding: var(--space-2) var(--space-4) var(--space-4);
    border-top: 1px solid var(--color-border);
  }
}

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: var(--space-4);
}

.modal {
  background: var(--color-background);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  width: 100%;
  max-width: 480px;

  &--sm {
    max-width: 360px;
  }

  &__header {
    padding: var(--space-4) var(--space-6);
    border-bottom: 1px solid var(--color-border);
  }

  &__title {
    font-size: var(--text-lg);
    font-weight: 600;
    margin: 0;
  }

  &__body {
    padding: var(--space-6);
  }

  &__footer {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-6);
    border-top: 1px solid var(--color-border);
    background: var(--color-surface);
  }
}

.btn--danger {
  background: var(--color-error, #dc2626);
  color: white;

  &:hover:not(:disabled) {
    background: #b91c1c;
  }
}

.spinner--sm {
  width: 14px;
  height: 14px;
  margin-right: var(--space-2);
}
</style>
