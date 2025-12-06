import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { uploadImage, type ImageSearchResponse, type ProductResult } from '@/api/client'
import { addSearchHistory } from '@/db'

/**
 * Centralized search state store.
 * Manages search results, loading states, and preview images
 * to persist state across Home → Search navigation.
 */
export const useSearchStore = defineStore('search', () => {
  // State
  const results = ref<ProductResult[]>([])
  const processingTimeMs = ref<number | null>(null)
  const embeddingTimeMs = ref<number | null>(null)
  const isSearching = ref(false)
  const hasSearched = ref(false)
  const error = ref<string | null>(null)
  const previewUrl = ref<string | null>(null)
  const uploadedFile = ref<File | null>(null)

  // Computed
  const hasResults = computed(() => results.value.length > 0)
  const resultCount = computed(() => results.value.length)

  /**
   * Perform visual search with an image file.
   * Handles resizing, upload, and state management.
   */
  async function searchWithImage(file: File): Promise<ImageSearchResponse | null> {
    // Clear previous state
    error.value = null
    isSearching.value = true
    hasSearched.value = false
    uploadedFile.value = file

    // Create preview URL
    if (previewUrl.value) {
      URL.revokeObjectURL(previewUrl.value)
    }
    previewUrl.value = URL.createObjectURL(file)

    try {
      // Resize image for faster upload (max 800px)
      const resizedBlob = await resizeImage(file, 800)

      // Upload and search
      const response = await uploadImage(resizedBlob)

      // Update state with results
      results.value = response.results
      processingTimeMs.value = response.processingTimeMs
      embeddingTimeMs.value = response.embeddingTimeMs ?? null
      hasSearched.value = true

      // Save to history
      const thumbnailDataUrl = await createThumbnailDataUrl(file, 100)
      await addSearchHistory(
        thumbnailDataUrl,
        'Image Search',
        response.results.length,
        response.results
      )

      return response
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Search failed'
      hasSearched.value = true
      return null
    } finally {
      isSearching.value = false
    }
  }

  /**
   * Clear all search state and reset to initial.
   */
  function clearSearch() {
    results.value = []
    processingTimeMs.value = null
    embeddingTimeMs.value = null
    isSearching.value = false
    hasSearched.value = false
    error.value = null
    uploadedFile.value = null

    if (previewUrl.value) {
      URL.revokeObjectURL(previewUrl.value)
      previewUrl.value = null
    }
  }

  /**
   * Clear only the preview image (for re-upload).
   */
  function clearPreview() {
    if (previewUrl.value) {
      URL.revokeObjectURL(previewUrl.value)
      previewUrl.value = null
    }
    uploadedFile.value = null
  }

  return {
    // State
    results,
    processingTimeMs,
    embeddingTimeMs,
    isSearching,
    hasSearched,
    error,
    previewUrl,
    uploadedFile,
    // Computed
    hasResults,
    resultCount,
    // Actions
    searchWithImage,
    clearSearch,
    clearPreview,
  }
})

// Helper: Resize image to max dimension
async function resizeImage(file: File, maxSize: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => {
      const canvas = document.createElement('canvas')
      let { width, height } = img

      if (width > maxSize || height > maxSize) {
        if (width > height) {
          height = (height / width) * maxSize
          width = maxSize
        } else {
          width = (width / height) * maxSize
          height = maxSize
        }
      }

      canvas.width = width
      canvas.height = height
      canvas.getContext('2d')?.drawImage(img, 0, 0, width, height)

      canvas.toBlob(
        (blob) => {
          if (blob) {
            resolve(blob)
          } else {
            reject(new Error('Failed to create blob'))
          }
        },
        'image/jpeg',
        0.85
      )
    }
    img.onerror = () => reject(new Error('Failed to load image'))
    img.src = URL.createObjectURL(file)
  })
}

// Helper: Create thumbnail data URL for history storage
async function createThumbnailDataUrl(file: File, maxSize: number): Promise<string> {
  const blob = await resizeImage(file, maxSize)
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = reject
    reader.readAsDataURL(blob)
  })
}
