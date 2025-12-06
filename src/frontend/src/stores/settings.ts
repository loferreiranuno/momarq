import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

export interface Setting {
  key: string
  value: string
  type: string
  category: string
  description?: string
  updatedAt?: string
}

export const useSettingsStore = defineStore('settings', () => {
  const settings = ref<Map<string, Setting>>(new Map())
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const sseConnection = ref<EventSource | null>(null)

  // Computed getters for common settings
  const siteName = computed(() => getSetting('ui.siteName', 'Monmarq'))
  const welcomeMessage = computed(
    () => getSetting('ui.welcomeMessage', 'Discover furniture through visual search.')
  )
  const primaryColor = computed(() => getSetting('ui.primaryColor', '#8B7355'))
  const showSimilarityScore = computed(() => getSetting('ui.showSimilarityScore', 'true') === 'true')
  const maxResults = computed(() => parseInt(getSetting('search.maxResults', '20'), 10))

  function getSetting(key: string, defaultValue = ''): string {
    return settings.value.get(key)?.value ?? defaultValue
  }

  async function updateSetting(key: string, value: string): Promise<void> {
    const authStore = useAuthStore()
    await apiClient({
      url: `/api/settings/${encodeURIComponent(key)}`,
      method: 'PUT',
      data: { value },
      headers: authStore.getAuthHeader(),
    })

    const existing = settings.value.get(key)
    if (existing) {
      existing.value = value
      settings.value.set(key, { ...existing })
    }
  }

  async function fetchPublicSettings() {
    isLoading.value = true
    error.value = null

    try {
      const response = await apiClient<Setting[]>({
        url: '/api/settings/public',
        method: 'GET',
      })

      settings.value.clear()
      for (const setting of response) {
        settings.value.set(setting.key, setting)
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Failed to fetch settings'
    } finally {
      isLoading.value = false
    }
  }

  function connectSSE() {
    if (sseConnection.value) {
      return
    }

    const eventSource = new EventSource('/api/settings/sse')

    eventSource.addEventListener('setting-change', (event) => {
      try {
        const data = JSON.parse(event.data) as { key: string; value: string }
        const existing = settings.value.get(data.key)
        if (existing) {
          existing.value = data.value
          settings.value.set(data.key, { ...existing })
        }
      } catch {
        console.error('Failed to parse SSE setting change')
      }
    })

    eventSource.addEventListener('cache-invalidated', () => {
      // Refetch all settings when cache is invalidated
      fetchPublicSettings()
    })

    eventSource.addEventListener('error', () => {
      // Reconnect after a delay
      eventSource.close()
      sseConnection.value = null
      setTimeout(() => connectSSE(), 5000)
    })

    sseConnection.value = eventSource
  }

  function disconnectSSE() {
    if (sseConnection.value) {
      sseConnection.value.close()
      sseConnection.value = null
    }
  }

  return {
    settings,
    isLoading,
    error,
    siteName,
    welcomeMessage,
    primaryColor,
    showSimilarityScore,
    maxResults,
    getSetting,
    updateSetting,
    fetchPublicSettings,
    connectSSE,
    disconnectSSE,
  }
})
