<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useSearchStore } from '@/stores/search'
import { X, Camera } from 'lucide-vue-next'

defineProps<{
  compact?: boolean
}>()

const emit = defineEmits<{
  searchStart: []
  searchComplete: []
  searchError: [error: Error]
}>()

const router = useRouter()
const searchStore = useSearchStore()
const isDragging = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)

function handleDragOver(e: DragEvent) {
  e.preventDefault()
  isDragging.value = true
}

function handleDragLeave() {
  isDragging.value = false
}

function handleDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false

  const file = e.dataTransfer?.files[0]
  if (file?.type.startsWith('image/')) {
    processFile(file)
  }
}

function handleFileInput(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) {
    processFile(file)
  }
}

function handlePaste(e: ClipboardEvent) {
  for (const item of e.clipboardData?.items ?? []) {
    if (item.type.startsWith('image/')) {
      const file = item.getAsFile()
      if (file) {
        processFile(file)
        break
      }
    }
  }
}

async function processFile(file: File) {
  emit('searchStart')

  const result = await searchStore.searchWithImage(file)

  if (result) {
    emit('searchComplete')
    // Navigate to search page if not already there
    if (router.currentRoute.value.name !== 'search') {
      router.push({ name: 'search' })
    }
  } else {
    emit('searchError', new Error(searchStore.error || 'Search failed'))
  }
}

function triggerFileInput() {
  fileInputRef.value?.click()
}

function clearPreview() {
  searchStore.clearPreview()
}
</script>

<template>
  <div class="image-upload" @paste="handlePaste">
    <div
      class="image-upload__dropzone"
      :class="{
        'image-upload__dropzone--compact': compact,
        'image-upload__dropzone--dragging': isDragging,
        'image-upload__dropzone--has-preview': searchStore.previewUrl
      }"
      @dragover="handleDragOver"
      @dragleave="handleDragLeave"
      @drop="handleDrop"
      @click="triggerFileInput"
    >
      <input
        ref="fileInputRef"
        type="file"
        accept="image/*"
        class="image-upload__input"
        @change="handleFileInput"
      />

      <!-- Preview State -->
      <template v-if="searchStore.previewUrl">
        <img 
          :src="searchStore.previewUrl" 
          alt="Preview" 
          class="image-upload__preview-img"
        />
        <button
          class="image-upload__clear-btn"
          @click.stop="clearPreview"
        >
          <X :stroke-width="2" />
        </button>
      </template>

      <!-- Empty State -->
      <template v-else>
        <div class="image-upload__icon">
          <Camera :stroke-width="1.5" />
        </div>
        <p class="image-upload__text">Drop an image here or click to upload</p>
        <p class="image-upload__hint">You can also paste an image from clipboard</p>
      </template>

      <!-- Loading Overlay -->
      <div v-if="searchStore.isSearching" class="image-upload__loading">
        <div class="image-upload__spinner"></div>
        <p>Analyzing image...</p>
      </div>
    </div>

    <!-- Error Message -->
    <p v-if="searchStore.error" class="image-upload__error">
      {{ searchStore.error }}
    </p>
  </div>
</template>

<style lang="scss" scoped>
.image-upload {
  width: 100%;

  &__dropzone {
    position: relative;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 200px;
    padding: var(--space-8);
    background-color: var(--color-background);
    border: 2px dashed var(--color-border);
    border-radius: var(--radius-lg);
    cursor: pointer;
    transition: all var(--transition-fast);

    &:hover {
      border-color: var(--color-primary);
      background-color: var(--color-primary-light);
    }

    &--compact {
      min-height: 140px;
      padding: var(--space-4);
    }

    &--dragging {
      border-color: var(--color-primary);
      background-color: var(--color-primary-light);
      transform: scale(1.01);
    }

    &--has-preview {
      padding: var(--space-4);
    }
  }

  &__input {
    display: none;
  }

  &__icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 64px;
    height: 64px;
    background-color: var(--color-surface);
    border-radius: var(--radius-full);
    margin-bottom: var(--space-4);
    color: var(--color-text-muted);

    svg {
      width: 32px;
      height: 32px;
    }
  }

  &__text {
    font-size: var(--text-base);
    font-weight: 500;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-1);
  }

  &__hint {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin: 0;
  }

  &__preview-img {
    max-width: 100%;
    max-height: 280px;
    border-radius: var(--radius-md);
    object-fit: contain;
  }

  &__clear-btn {
    position: absolute;
    top: var(--space-3);
    right: var(--space-3);
    display: flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    background-color: #fff;
    border: 1px solid var(--color-border);
    border-radius: var(--radius-full);
    cursor: pointer;
    transition: all var(--transition-fast);

    svg {
      width: 16px;
      height: 16px;
      color: var(--color-text-secondary);
    }

    &:hover {
      background-color: var(--color-error);
      border-color: var(--color-error);

      svg {
        color: #fff;
      }
    }
  }

  &__loading {
    position: absolute;
    inset: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    background-color: rgba(255, 255, 255, 0.95);
    border-radius: var(--radius-lg);

    p {
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
      margin: 0;
    }
  }

  &__spinner {
    width: 40px;
    height: 40px;
    border: 3px solid var(--color-border);
    border-top-color: var(--color-primary);
    border-radius: 50%;
    margin-bottom: var(--space-3);
    animation: spin 0.8s linear infinite;
  }

  &__error {
    margin: var(--space-4) 0 0;
    padding: var(--space-3) var(--space-4);
    background-color: #FEF2F2;
    border: 1px solid #FECACA;
    border-radius: var(--radius-md);
    font-size: var(--text-sm);
    color: var(--color-error);
    text-align: center;
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
