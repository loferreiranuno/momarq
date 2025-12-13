<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { api } from '@/api/client'
import ConfirmModal from '@/components/ConfirmModal.vue'

// Available crawler types
const CRAWLER_TYPES = [
  { value: 'generic', label: 'Generic (HTML/CSS)' },
  { value: 'json-ld', label: 'JSON-LD Structured Data' },
  { value: 'sitemap', label: 'Sitemap' },
  { value: 'api', label: 'API' },
  { value: 'zarahome', label: 'Zara Home (Playwright)' },
] as const

// Default crawler configs per type
const CRAWLER_CONFIG_TEMPLATES: Record<string, object> = {
  generic: {
    requestDelayMs: 1000,
    maxConcurrency: 2,
    respectRobotsTxt: true,
    userAgent: 'VisualSearchBot/1.0',
    productContainerSelector: '.product-card',
    productNameSelector: '.product-title',
    productPriceSelector: '.product-price',
    productImageSelector: '.product-image img',
  },
  'json-ld': {
    requestDelayMs: 1000,
    maxConcurrency: 2,
    respectRobotsTxt: true,
  },
  sitemap: {
    requestDelayMs: 1000,
    maxConcurrency: 2,
    respectRobotsTxt: true,
  },
  api: {
    requestDelayMs: 500,
    maxConcurrency: 5,
    customSettings: {
      apiEndpoint: '',
      apiKey: '',
    },
  },
  zarahome: {
    requestDelayMs: 2000,
    maxConcurrency: 1,
    customSettings: {
      SitemapUrl: 'https://www.zarahome.com/8/info/sitemaps/sitemap-products-zh-es-0.xml.gz',
      MaxPages: '100',
    },
  },
}

interface ProviderDto {
  id: number
  name: string
  logoUrl?: string
  websiteUrl?: string
  productCount: number
  crawlerType?: string
  crawlerConfigJson?: string
}

interface ProviderForm {
  name: string
  logoUrl: string
  websiteUrl: string
  crawlerType: string
  crawlerConfigJson: string
}

const providers = ref<ProviderDto[]>([])
const isLoading = ref(true)
const error = ref<string | null>(null)
const jsonError = ref<string | null>(null)

// Modal state
const showModal = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)
const isSaving = ref(false)
const form = ref<ProviderForm>({
  name: '',
  logoUrl: '',
  websiteUrl: '',
  crawlerType: 'generic',
  crawlerConfigJson: '',
})

// Delete confirmation
const showDeleteConfirm = ref(false)
const deletingProvider = ref<ProviderDto | null>(null)
const isDeleting = ref(false)

const modalTitle = computed(() => (isEditing.value ? 'Edit Provider' : 'Add Provider'))

// Validate JSON when it changes
watch(() => form.value.crawlerConfigJson, (value) => {
  if (!value || value.trim() === '') {
    jsonError.value = null
    return
  }
  try {
    JSON.parse(value)
    jsonError.value = null
  } catch (e) {
    jsonError.value = e instanceof Error ? e.message : 'Invalid JSON'
  }
})

onMounted(async () => {
  await loadProviders()
})

async function loadProviders() {
  isLoading.value = true
  error.value = null
  try {
    providers.value = await api.get<ProviderDto[]>('/api/admin/providers')
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load providers'
    console.error('Failed to load providers:', e)
  } finally {
    isLoading.value = false
  }
}

function openAddModal() {
  isEditing.value = false
  editingId.value = null
  form.value = { name: '', logoUrl: '', websiteUrl: '', crawlerType: 'generic', crawlerConfigJson: '' }
  jsonError.value = null
  showModal.value = true
}

function openEditModal(provider: ProviderDto) {
  isEditing.value = true
  editingId.value = provider.id
  form.value = {
    name: provider.name,
    logoUrl: provider.logoUrl ?? '',
    websiteUrl: provider.websiteUrl ?? '',
    crawlerType: provider.crawlerType ?? 'generic',
    crawlerConfigJson: provider.crawlerConfigJson ?? '',
  }
  jsonError.value = null
  showModal.value = true
}

function closeModal() {
  showModal.value = false
  form.value = { name: '', logoUrl: '', websiteUrl: '', crawlerType: 'generic', crawlerConfigJson: '' }
  jsonError.value = null
}

function loadConfigTemplate() {
  const template = CRAWLER_CONFIG_TEMPLATES[form.value.crawlerType]
  if (template) {
    form.value.crawlerConfigJson = JSON.stringify(template, null, 2)
  }
}

async function saveProvider() {
  if (!form.value.name.trim()) {
    return
  }

  // Don't save if there's a JSON error
  if (form.value.crawlerConfigJson && jsonError.value) {
    return
  }

  isSaving.value = true
  try {
    const payload = {
      name: form.value.name.trim(),
      logoUrl: form.value.logoUrl.trim() || undefined,
      websiteUrl: form.value.websiteUrl.trim() || undefined,
      crawlerType: form.value.crawlerType,
      crawlerConfigJson: form.value.crawlerConfigJson.trim() || undefined,
    }

    if (isEditing.value && editingId.value) {
      await api.put(`/api/admin/providers/${editingId.value}`, payload)
    } else {
      await api.post('/api/admin/providers', payload)
    }

    closeModal()
    await loadProviders()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save provider'
  } finally {
    isSaving.value = false
  }
}

function confirmDelete(provider: ProviderDto) {
  deletingProvider.value = provider
  showDeleteConfirm.value = true
}

function cancelDelete() {
  showDeleteConfirm.value = false
  deletingProvider.value = null
}

async function deleteProvider() {
  if (!deletingProvider.value) return

  isDeleting.value = true
  try {
    await api.delete(`/api/admin/providers/${deletingProvider.value.id}`)
    showDeleteConfirm.value = false
    deletingProvider.value = null
    await loadProviders()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete provider'
  } finally {
    isDeleting.value = false
  }
}
</script>

<template>
  <div class="admin-providers">
    <header class="admin-providers__header">
      <div class="admin-providers__title-row">
        <div>
          <h1 class="page-title">Providers</h1>
          <p class="admin-providers__subtitle">Manage product providers and suppliers</p>
        </div>
        <button class="btn btn--primary" @click="openAddModal">
          + Add Provider
        </button>
      </div>
    </header>

    <div v-if="error" class="admin-providers__error card">
      <p>{{ error }}</p>
      <button class="btn btn--sm btn--outline" @click="loadProviders">Retry</button>
    </div>

    <div v-if="isLoading" class="admin-providers__loading">
      <span class="spinner"></span>
      <p>Loading providers...</p>
    </div>

    <template v-else-if="providers.length === 0">
      <div class="admin-providers__empty card">
        <span class="admin-providers__empty-icon">🏪</span>
        <h3>No providers yet</h3>
        <p>Add your first provider to start managing products</p>
        <button class="btn btn--primary" @click="openAddModal">+ Add Provider</button>
      </div>
    </template>

    <template v-else>
      <div class="admin-providers__table-container card">
        <table class="admin-providers__table">
          <thead>
            <tr>
              <th>Logo</th>
              <th>Name</th>
              <th>Website</th>
              <th>Crawler</th>
              <th>Products</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="provider in providers" :key="provider.id">
              <td class="admin-providers__logo-cell">
                <img
                  v-if="provider.logoUrl"
                  :src="provider.logoUrl"
                  :alt="provider.name"
                  class="admin-providers__logo"
                />
                <span v-else class="admin-providers__logo-placeholder">🏪</span>
              </td>
              <td class="admin-providers__name">{{ provider.name }}</td>
              <td>
                <a
                  v-if="provider.websiteUrl"
                  :href="provider.websiteUrl"
                  target="_blank"
                  rel="noopener"
                  class="admin-providers__link"
                >
                  {{ provider.websiteUrl }}
                </a>
                <span v-else class="text-muted">—</span>
              </td>
              <td>
                <span class="admin-providers__crawler-badge">{{ provider.crawlerType ?? 'generic' }}</span>
              </td>
              <td>
                <span class="admin-providers__badge">{{ provider.productCount }}</span>
              </td>
              <td class="admin-providers__actions">
                <button
                  class="btn btn--sm btn--ghost"
                  title="Edit"
                  @click="openEditModal(provider)"
                >
                  ✏️
                </button>
                <button
                  class="btn btn--sm btn--ghost"
                  title="Delete"
                  :disabled="provider.productCount > 0"
                  @click="confirmDelete(provider)"
                >
                  🗑️
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>

    <!-- Add/Edit Modal -->
    <Teleport to="body">
      <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
        <div class="modal">
          <div class="modal__header">
            <h2 class="modal__title">{{ modalTitle }}</h2>
            <button class="btn btn--ghost btn--icon" @click="closeModal">✕</button>
          </div>
          <form class="modal__body" @submit.prevent="saveProvider">
            <div class="form-group">
              <label class="label" for="provider-name">Name *</label>
              <input
                id="provider-name"
                v-model="form.name"
                type="text"
                class="input"
                placeholder="Provider name"
                required
              />
            </div>
            <div class="form-group">
              <label class="label" for="provider-logo">Logo URL</label>
              <input
                id="provider-logo"
                v-model="form.logoUrl"
                type="url"
                class="input"
                placeholder="https://example.com/logo.png"
              />
            </div>
            <div class="form-group">
              <label class="label" for="provider-website">Website URL</label>
              <input
                id="provider-website"
                v-model="form.websiteUrl"
                type="url"
                class="input"
                placeholder="https://example.com"
              />
            </div>
            <div class="form-group">
              <label class="label" for="provider-crawler-type">Crawler Type</label>
              <select
                id="provider-crawler-type"
                v-model="form.crawlerType"
                class="input"
              >
                <option v-for="type in CRAWLER_TYPES" :key="type.value" :value="type.value">
                  {{ type.label }}
                </option>
              </select>
            </div>
            <div class="form-group">
              <label class="label" for="provider-crawler-config">
                Crawler Config (JSON)
                <button
                  type="button"
                  class="btn btn--sm btn--ghost admin-providers__template-btn"
                  @click="loadConfigTemplate"
                  title="Load template for selected crawler type"
                >
                  📋 Load Template
                </button>
              </label>
              <textarea
                id="provider-crawler-config"
                v-model="form.crawlerConfigJson"
                class="input admin-providers__json-input"
                :class="{ 'input--error': jsonError }"
                placeholder='{"baseUrl": "https://example.com", ...}'
                rows="8"
              ></textarea>
              <p v-if="jsonError" class="form-error">{{ jsonError }}</p>
            </div>
            <div class="modal__footer">
              <button type="button" class="btn btn--secondary" @click="closeModal">
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn--primary"
                :disabled="isSaving || !form.name.trim() || Boolean(form.crawlerConfigJson && jsonError)"
              >
                <span v-if="isSaving" class="spinner spinner--sm"></span>
                {{ isSaving ? 'Saving...' : 'Save' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- Delete Confirmation Modal -->
    <ConfirmModal
      v-model="showDeleteConfirm"
      title="Delete Provider"
      :message="`<p>Are you sure you want to delete <strong>${deletingProvider?.name}</strong>?</p><p class='text-muted'>This action cannot be undone.</p>`"
      confirm-text="Delete"
      cancel-text="Cancel"
      :is-loading="isDeleting"
      variant="danger"
      @confirm="deleteProvider"
      @cancel="cancelDelete"
    />
  </div>
</template>

<style lang="scss" scoped>
.admin-providers {
  &__header {
    margin-bottom: var(--space-8);
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
      font-size: var(--text-sm);
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--color-text-muted);
      background: var(--color-surface);
    }

    tr:hover {
      background: var(--color-surface);
    }
  }

  &__logo-cell {
    width: 60px;
  }

  &__logo {
    width: 40px;
    height: 40px;
    object-fit: contain;
    border-radius: var(--radius-sm);
  }

  &__logo-placeholder {
    font-size: 1.5rem;
    display: block;
    width: 40px;
    text-align: center;
  }

  &__name {
    font-weight: 500;
  }

  &__link {
    color: var(--color-primary);
    text-decoration: none;
    font-size: var(--text-sm);

    &:hover {
      text-decoration: underline;
    }
  }

  &__badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 24px;
    padding: 2px 8px;
    background: var(--color-surface);
    border-radius: var(--radius-full, 9999px);
    font-size: var(--text-sm);
    font-weight: 500;
  }

  &__crawler-badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 2px 8px;
    background: var(--color-primary-light, #e0f2fe);
    color: var(--color-primary-dark, #0369a1);
    border-radius: var(--radius-sm, 4px);
    font-size: var(--text-xs);
    font-weight: 500;
    text-transform: lowercase;
  }

  &__actions {
    display: flex;
    gap: var(--space-1);
  }
}

.form-group {
  margin-bottom: var(--space-4);

  &:last-child {
    margin-bottom: 0;
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
  max-height: 90vh;
  overflow: auto;

  &--sm {
    max-width: 360px;
  }

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

.admin-providers__json-input {
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
  font-size: var(--text-sm);
  resize: vertical;
  min-height: 120px;
}

.admin-providers__template-btn {
  margin-left: var(--space-2);
  font-size: var(--text-xs);
}

.form-error {
  color: var(--color-error, #dc2626);
  font-size: var(--text-sm);
  margin-top: var(--space-1);
  margin-bottom: 0;
}

.input--error {
  border-color: var(--color-error, #dc2626);

  &:focus {
    border-color: var(--color-error, #dc2626);
    box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.2);
  }
}
</style>
