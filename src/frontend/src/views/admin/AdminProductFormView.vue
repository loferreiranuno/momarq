<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { api } from '@/api/client'

interface ProductImageDto {
  id: number
  imageUrl: string
  localPath?: string
  isLocalFile: boolean
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

interface ProductForm {
  providerId: number | null
  externalId: string
  name: string
  description: string
  price: number
  currency: string
  category: string
  productUrl: string
}

const router = useRouter()
const route = useRoute()

const productId = computed(() => {
  const id = route.params.id
  return id ? Number(id) : null
})

const isEditing = computed(() => productId.value !== null)
const pageTitle = computed(() => (isEditing.value ? 'Edit Product' : 'New Product'))

// Data
const providers = ref<ProviderDto[]>([])
const images = ref<ProductImageDto[]>([])
const isLoading = ref(true)
const isSaving = ref(false)
const error = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Form
const form = ref<ProductForm>({
  providerId: null,
  externalId: '',
  name: '',
  description: '',
  price: 0,
  currency: 'USD',
  category: '',
  productUrl: '',
})

// Image management
const newImageUrl = ref('')
const isAddingImage = ref(false)
const isUploadingFile = ref(false)
const isDownloadingUrl = ref(false)
const isDragging = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const uploadProgress = ref(0)

onMounted(async () => {
  await loadProviders()
  if (isEditing.value) {
    await loadProduct()
  } else {
    isLoading.value = false
  }
  
  // Add paste event listener for clipboard images
  document.addEventListener('paste', handlePaste)
})

// Cleanup paste listener
import { onUnmounted } from 'vue'
onUnmounted(() => {
  document.removeEventListener('paste', handlePaste)
})

// Handle paste event for clipboard images
async function handlePaste(event: ClipboardEvent) {
  if (!isEditing.value || !productId.value) return
  
  const items = event.clipboardData?.items
  if (!items) return
  
  const imageFiles: File[] = []
  
  for (const item of Array.from(items)) {
    if (item.type.startsWith('image/')) {
      const file = item.getAsFile()
      if (file) {
        imageFiles.push(file)
      }
    }
  }
  
  if (imageFiles.length > 0) {
    event.preventDefault()
    // Convert File[] to FileList-like object
    const dataTransfer = new DataTransfer()
    imageFiles.forEach(f => dataTransfer.items.add(f))
    await uploadFiles(dataTransfer.files)
  }
}

async function loadProviders() {
  try {
    providers.value = await api.get<ProviderDto[]>('/api/admin/providers')
  } catch (e) {
    console.error('Failed to load providers:', e)
  }
}

async function loadProduct() {
  isLoading.value = true
  error.value = null
  try {
    const product = await api.get<ProductDto>(`/api/admin/products/${productId.value}`)
    form.value = {
      providerId: product.providerId,
      externalId: product.externalId ?? '',
      name: product.name,
      description: product.description ?? '',
      price: product.price,
      currency: product.currency ?? 'USD',
      category: product.category ?? '',
      productUrl: product.productUrl ?? '',
    }
    images.value = product.images
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load product'
  } finally {
    isLoading.value = false
  }
}

async function saveProduct() {
  if (!form.value.name.trim() || !form.value.providerId) {
    error.value = 'Please fill in required fields'
    return
  }

  isSaving.value = true
  error.value = null
  successMessage.value = null

  try {
    const payload = {
      providerId: form.value.providerId,
      name: form.value.name.trim(),
      externalId: form.value.externalId.trim() || undefined,
      description: form.value.description.trim() || undefined,
      price: form.value.price,
      currency: form.value.currency || undefined,
      category: form.value.category.trim() || undefined,
      productUrl: form.value.productUrl.trim() || undefined,
    }

    if (isEditing.value) {
      await api.put(`/api/admin/products/${productId.value}`, payload)
      successMessage.value = 'Product updated successfully'
    } else {
      const newProduct = await api.post<ProductDto>('/api/admin/products', payload)
      successMessage.value = 'Product created successfully'
      // Redirect to edit page to manage images
      router.replace({ name: 'admin-product-edit', params: { id: newProduct.id } })
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save product'
  } finally {
    isSaving.value = false
  }
}

async function addImage() {
  if (!newImageUrl.value.trim() || !productId.value) return

  isAddingImage.value = true
  isDownloadingUrl.value = true
  error.value = null
  
  try {
    // Download the image from URL and upload it as a file to avoid SSL issues
    const url = newImageUrl.value.trim()
    
    // Fetch the image as blob
    const response = await fetch(url)
    if (!response.ok) {
      throw new Error(`Failed to download image: ${response.status} ${response.statusText}`)
    }
    
    const blob = await response.blob()
    
    // Validate it's an image
    if (!blob.type.startsWith('image/')) {
      throw new Error('URL does not point to a valid image')
    }
    
    // Create a File object from the blob
    const extension = blob.type.split('/')[1] || 'jpg'
    const filename = `downloaded-image.${extension}`
    const file = new File([blob], filename, { type: blob.type })
    
    isDownloadingUrl.value = false
    
    // Upload the file
    const dataTransfer = new DataTransfer()
    dataTransfer.items.add(file)
    await uploadFiles(dataTransfer.files)
    
    newImageUrl.value = ''
    successMessage.value = 'Image downloaded and uploaded successfully'
  } catch (e) {
    // If download fails (e.g., CORS), fall back to server-side download
    if (e instanceof TypeError || (e instanceof Error && e.message.includes('Failed to fetch'))) {
      try {
        isDownloadingUrl.value = false
        // Fallback: let server download and store the image
        await api.post(`/api/admin/products/${productId.value}/images/download`, {
          imageUrl: newImageUrl.value.trim(),
          isPrimary: images.value.length === 0,
        })
        newImageUrl.value = ''
        await loadProduct()
        successMessage.value = 'Image downloaded by server and uploaded'
      } catch (serverErr) {
        error.value = serverErr instanceof Error ? serverErr.message : 'Failed to download image'
      }
    } else {
      error.value = e instanceof Error ? e.message : 'Failed to add image'
    }
  } finally {
    isAddingImage.value = false
    isDownloadingUrl.value = false
  }
}

// File upload handlers
function triggerFileInput() {
  fileInput.value?.click()
}

function handleFileSelect(event: Event) {
  const input = event.target as HTMLInputElement
  if (input.files?.length) {
    uploadFiles(input.files)
  }
}

function handleDragOver(event: DragEvent) {
  event.preventDefault()
  isDragging.value = true
}

function handleDragLeave() {
  isDragging.value = false
}

function handleDrop(event: DragEvent) {
  event.preventDefault()
  isDragging.value = false
  
  const files = event.dataTransfer?.files
  if (files?.length) {
    uploadFiles(files)
  }
}

async function uploadFiles(files: FileList) {
  if (!productId.value) return

  const validTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp']
  const maxSize = 20 * 1024 * 1024 // 20MB

  isUploadingFile.value = true
  uploadProgress.value = 0
  error.value = null

  let uploadedCount = 0
  const totalFiles = files.length

  try {
    for (const file of Array.from(files)) {
      // Validate file type
      if (!validTypes.includes(file.type)) {
        error.value = `Invalid file type: ${file.name}. Allowed: JPEG, PNG, GIF, WebP`
        continue
      }

      // Validate file size
      if (file.size > maxSize) {
        error.value = `File too large: ${file.name}. Maximum size is 20MB`
        continue
      }

      // Create form data
      const formData = new FormData()
      formData.append('file', file)
      formData.append('isPrimary', (images.value.length === 0 && uploadedCount === 0).toString())

      // Upload with fetch (for multipart/form-data)
      const token = localStorage.getItem('auth_token')
      const response = await fetch(`/api/admin/products/${productId.value}/images/upload`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
        },
        body: formData,
      })

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}))
        throw new Error(errorData.error || `Upload failed for ${file.name}`)
      }

      uploadedCount++
      uploadProgress.value = Math.round((uploadedCount / totalFiles) * 100)
    }

    await loadProduct()
    
    if (uploadedCount > 0) {
      successMessage.value = `${uploadedCount} image${uploadedCount > 1 ? 's' : ''} uploaded and vectorized`
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to upload image'
  } finally {
    isUploadingFile.value = false
    uploadProgress.value = 0
    // Reset file input
    if (fileInput.value) {
      fileInput.value.value = ''
    }
  }
}

async function setPrimaryImage(image: ProductImageDto) {
  if (!productId.value) return

  try {
    await api.put(`/api/admin/products/${productId.value}/images/${image.id}`, {
      isPrimary: true,
    })
    await loadProduct()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to update image'
  }
}

async function deleteImage(image: ProductImageDto) {
  if (!productId.value) return
  if (!confirm('Are you sure you want to delete this image?')) return

  try {
    await api.delete(`/api/admin/products/${productId.value}/images/${image.id}`)
    await loadProduct()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to delete image'
  }
}

async function vectorizeProduct() {
  if (!productId.value) return

  try {
    await api.post(`/api/admin/products/${productId.value}/vectorize`)
    await loadProduct()
    successMessage.value = 'Vectorization completed'
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to vectorize product'
  }
}

function goBack() {
  router.push({ name: 'admin-products' })
}
</script>

<template>
  <div class="product-form">
    <header class="product-form__header">
      <button class="btn btn--ghost" @click="goBack">← Back to Products</button>
      <h1 class="page-title">{{ pageTitle }}</h1>
    </header>

    <div v-if="error" class="product-form__message product-form__message--error">
      {{ error }}
      <button class="btn btn--sm btn--ghost" @click="error = null">✕</button>
    </div>

    <div v-if="successMessage" class="product-form__message product-form__message--success">
      {{ successMessage }}
      <button class="btn btn--sm btn--ghost" @click="successMessage = null">✕</button>
    </div>

    <div v-if="isLoading" class="product-form__loading">
      <span class="spinner"></span>
      <p>Loading...</p>
    </div>

    <template v-else>
      <div class="product-form__grid">
        <!-- Product Details -->
        <section class="product-form__section card">
          <h2 class="section-title">Product Details</h2>
          
          <form @submit.prevent="saveProduct">
            <div class="form-row">
              <div class="form-group">
                <label class="label" for="provider">Provider *</label>
                <select id="provider" v-model="form.providerId" class="input" required>
                  <option :value="null" disabled>Select a provider</option>
                  <option v-for="provider in providers" :key="provider.id" :value="provider.id">
                    {{ provider.name }}
                  </option>
                </select>
              </div>
              <div class="form-group">
                <label class="label" for="externalId">External ID</label>
                <input
                  id="externalId"
                  v-model="form.externalId"
                  type="text"
                  class="input"
                  placeholder="SKU or external reference"
                />
              </div>
            </div>

            <div class="form-group">
              <label class="label" for="name">Name *</label>
              <input
                id="name"
                v-model="form.name"
                type="text"
                class="input"
                placeholder="Product name"
                required
              />
            </div>

            <div class="form-group">
              <label class="label" for="description">Description</label>
              <textarea
                id="description"
                v-model="form.description"
                class="input"
                rows="3"
                placeholder="Product description"
              ></textarea>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label class="label" for="price">Price *</label>
                <input
                  id="price"
                  v-model.number="form.price"
                  type="number"
                  step="0.01"
                  min="0"
                  class="input"
                  required
                />
              </div>
              <div class="form-group">
                <label class="label" for="currency">Currency</label>
                <select id="currency" v-model="form.currency" class="input">
                  <option value="USD">USD</option>
                  <option value="EUR">EUR</option>
                  <option value="GBP">GBP</option>
                </select>
              </div>
              <div class="form-group">
                <label class="label" for="category">Category</label>
                <input
                  id="category"
                  v-model="form.category"
                  type="text"
                  class="input"
                  placeholder="e.g., Sofas, Tables"
                />
              </div>
            </div>

            <div class="form-group">
              <label class="label" for="productUrl">Product URL</label>
              <input
                id="productUrl"
                v-model="form.productUrl"
                type="url"
                class="input"
                placeholder="https://example.com/product"
              />
            </div>

            <div class="form-actions">
              <button type="button" class="btn btn--secondary" @click="goBack">Cancel</button>
              <button
                type="submit"
                class="btn btn--primary"
                :disabled="isSaving || !form.name.trim() || !form.providerId"
              >
                <span v-if="isSaving" class="spinner spinner--sm"></span>
                {{ isSaving ? 'Saving...' : 'Save Product' }}
              </button>
            </div>
          </form>
        </section>

        <!-- Images Section (only for existing products) -->
        <section v-if="isEditing" class="product-form__section card">
          <div class="section-header">
            <h2 class="section-title">Images</h2>
            <button class="btn btn--sm btn--secondary" @click="vectorizeProduct">
              🔄 Re-vectorize
            </button>
          </div>

          <!-- File Upload Drop Zone -->
          <div 
            class="upload-zone"
            :class="{ 'upload-zone--dragging': isDragging, 'upload-zone--uploading': isUploadingFile }"
            @dragover="handleDragOver"
            @dragleave="handleDragLeave"
            @drop="handleDrop"
            @click="triggerFileInput"
          >
            <input
              ref="fileInput"
              type="file"
              accept="image/jpeg,image/png,image/gif,image/webp"
              multiple
              class="upload-zone__input"
              @change="handleFileSelect"
            />
            <div v-if="isUploadingFile" class="upload-zone__progress">
              <span class="spinner"></span>
              <p>Uploading... {{ uploadProgress }}%</p>
              <div class="progress-bar">
                <div class="progress-bar__fill" :style="{ width: uploadProgress + '%' }"></div>
              </div>
            </div>
            <div v-else class="upload-zone__content">
              <span class="upload-zone__icon">📁</span>
              <p class="upload-zone__text">
                <strong>Click to upload</strong>, drag and drop, or <strong>paste</strong> (Ctrl+V)
              </p>
              <p class="upload-zone__hint">JPEG, PNG, GIF, WebP up to 20MB</p>
            </div>
          </div>

          <!-- OR divider -->
          <div class="upload-divider">
            <span>OR</span>
          </div>

          <!-- Add Image by URL Form -->
          <div class="add-image-form">
            <input
              v-model="newImageUrl"
              type="url"
              class="input"
              placeholder="Enter image URL (will be downloaded and stored locally)..."
              @keyup.enter="addImage"
            />
            <button
              class="btn btn--primary"
              :disabled="isAddingImage || !newImageUrl.trim()"
              @click="addImage"
            >
              <span v-if="isAddingImage" class="spinner spinner--sm"></span>
              {{ isDownloadingUrl ? 'Downloading...' : isAddingImage ? 'Uploading...' : 'Download & Add' }}
            </button>
          </div>

          <!-- Image Grid -->
          <div v-if="images.length === 0" class="images-empty">
            <p>No images yet. Upload files or add an image URL above.</p>
          </div>

          <div v-else class="images-grid">
            <div
              v-for="image in images"
              :key="image.id"
              class="image-card"
              :class="{ 'image-card--primary': image.isPrimary }"
            >
              <div class="image-card__preview">
                <img :src="image.imageUrl" :alt="'Product image'" />
              </div>
              <div class="image-card__info">
                <div class="image-card__badges">
                  <span v-if="image.isPrimary" class="badge badge--primary">Primary</span>
                  <span
                    class="badge"
                    :class="image.hasEmbedding ? 'badge--success' : 'badge--warning'"
                  >
                    {{ image.hasEmbedding ? 'Vectorized' : 'Pending' }}
                  </span>
                  <span v-if="image.isLocalFile" class="badge badge--info" title="Stored locally">
                    📁
                  </span>
                </div>
                <div class="image-card__actions">
                  <button
                    v-if="!image.isPrimary"
                    class="btn btn--sm btn--ghost"
                    title="Set as primary"
                    @click="setPrimaryImage(image)"
                  >
                    ⭐
                  </button>
                  <button
                    class="btn btn--sm btn--ghost"
                    title="Delete"
                    @click="deleteImage(image)"
                  >
                    🗑️
                  </button>
                </div>
              </div>
            </div>
          </div>
        </section>

        <div v-else class="product-form__section card">
          <h2 class="section-title">Images</h2>
          <p class="text-muted">Save the product first to add images.</p>
        </div>
      </div>
    </template>
  </div>
</template>

<style lang="scss" scoped>
.product-form {
  &__header {
    margin-bottom: var(--space-6);

    .btn {
      margin-bottom: var(--space-2);
    }
  }

  &__loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: var(--space-16);
  }

  &__message {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: var(--space-3) var(--space-4);
    border-radius: var(--radius-md);
    margin-bottom: var(--space-4);

    &--error {
      background: #fef2f2;
      color: #dc2626;
      border: 1px solid #fecaca;
    }

    &--success {
      background: #dcfce7;
      color: #166534;
      border: 1px solid #bbf7d0;
    }
  }

  &__grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: var(--space-6);

    @media (max-width: 1024px) {
      grid-template-columns: 1fr;
    }
  }

  &__section {
    padding: var(--space-6);
  }
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--space-4);
}

.section-title {
  font-size: var(--text-lg);
  font-weight: 600;
  margin-bottom: var(--space-4);

  .section-header & {
    margin-bottom: 0;
  }
}

.form-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: var(--space-4);
}

.form-group {
  margin-bottom: var(--space-4);

  textarea.input {
    resize: vertical;
    min-height: 80px;
  }
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  margin-top: var(--space-6);
  padding-top: var(--space-4);
  border-top: 1px solid var(--color-border);
}

.add-image-form {
  display: flex;
  gap: var(--space-3);
  margin-bottom: var(--space-6);

  .input {
    flex: 1;
  }
}

.upload-zone {
  border: 2px dashed var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-8);
  text-align: center;
  cursor: pointer;
  transition: all 0.2s ease;
  background: var(--color-surface);
  margin-bottom: var(--space-4);

  &:hover {
    border-color: var(--color-accent);
    background: rgba(var(--color-accent-rgb), 0.05);
  }

  &--dragging {
    border-color: var(--color-accent);
    background: rgba(var(--color-accent-rgb), 0.1);
    border-style: solid;
  }

  &--uploading {
    cursor: wait;
    pointer-events: none;
  }

  &__input {
    display: none;
  }

  &__content {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-2);
  }

  &__icon {
    font-size: 2.5rem;
    opacity: 0.6;
  }

  &__text {
    color: var(--color-text);
    margin: 0;
  }

  &__hint {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin: 0;
  }

  &__progress {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-3);

    p {
      margin: 0;
      font-weight: 500;
    }
  }
}

.progress-bar {
  width: 200px;
  height: 8px;
  background: var(--color-border);
  border-radius: var(--radius-sm);
  overflow: hidden;

  &__fill {
    height: 100%;
    background: var(--color-accent);
    transition: width 0.3s ease;
  }
}

.upload-divider {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin: var(--space-4) 0;

  &::before,
  &::after {
    content: '';
    flex: 1;
    height: 1px;
    background: var(--color-border);
  }

  span {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    font-weight: 500;
  }
}

.images-empty {
  text-align: center;
  padding: var(--space-8);
  color: var(--color-text-muted);
}

.images-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: var(--space-4);
}

.image-card {
  border: 2px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;

  &--primary {
    border-color: var(--color-accent);
  }

  &__preview {
    aspect-ratio: 1;
    background: var(--color-surface);

    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
  }

  &__info {
    padding: var(--space-2);
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: var(--space-2);
    flex-wrap: wrap;
  }

  &__badges {
    display: flex;
    gap: var(--space-1);
    flex-wrap: wrap;
  }

  &__actions {
    display: flex;
    gap: var(--space-1);
  }
}

.badge {
  font-size: var(--text-xs);
  padding: 2px 6px;
  border-radius: var(--radius-sm);
  font-weight: 500;

  &--primary {
    background: var(--color-accent);
    color: white;
  }

  &--success {
    background: #dcfce7;
    color: #166534;
  }

  &--warning {
    background: #fef3c7;
    color: #92400e;
  }

  &--info {
    background: #dbeafe;
    color: #1e40af;
  }
}

.spinner--sm {
  width: 14px;
  height: 14px;
  margin-right: var(--space-2);
}
</style>
