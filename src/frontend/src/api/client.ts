import { useAuthStore } from '@/stores/auth'

export interface ApiClientOptions {
  url: string
  method: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH'
  data?: unknown
  headers?: Record<string, string>
  params?: Record<string, string | number | boolean>
}

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public data?: unknown
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

export async function apiClient<T>(options: ApiClientOptions): Promise<T> {
  const { url, method, data, headers = {}, params } = options

  // Build URL with query params
  let fullUrl = url
  if (params) {
    const searchParams = new URLSearchParams()
    for (const [key, value] of Object.entries(params)) {
      searchParams.append(key, String(value))
    }
    fullUrl = `${url}?${searchParams.toString()}`
  }

  // Get auth headers
  const authStore = useAuthStore()
  const authHeaders = authStore.getAuthHeader()

  // Build request options
  const requestOptions: RequestInit = {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...authHeaders,
      ...headers,
    },
  }

  if (data && method !== 'GET') {
    requestOptions.body = JSON.stringify(data)
  }

  const response = await fetch(fullUrl, requestOptions)

  if (!response.ok) {
    let errorData: unknown
    try {
      errorData = await response.json()
    } catch {
      errorData = { error: response.statusText }
    }

    const errorMessage =
      (errorData as { error?: string })?.error ?? response.statusText

    if (response.status === 401) {
      authStore.logout()
    }

    throw new ApiError(errorMessage, response.status, errorData)
  }

  // Handle empty responses
  const contentType = response.headers.get('Content-Type')
  if (!contentType?.includes('application/json')) {
    return {} as T
  }

  return response.json() as Promise<T>
}

export async function uploadImage(file: Blob): Promise<ImageSearchResponse> {
  const formData = new FormData()
  formData.append('image', file, 'image.jpg')

  const response = await fetch('/api/search/image', {
    method: 'POST',
    body: formData,
  })

  if (!response.ok) {
    let errorData: unknown
    try {
      errorData = await response.json()
    } catch {
      errorData = { error: response.statusText }
    }
    throw new ApiError(
      (errorData as { error?: string })?.error ?? 'Search failed',
      response.status,
      errorData
    )
  }

  return response.json() as Promise<ImageSearchResponse>
}

export interface ProductResult {
  productId: number
  name: string
  price: number
  imageUrl: string
  providerName: string
  similarity: number
}

export interface ImageSearchResponse {
  results: ProductResult[]
  processingTimeMs: number
  embeddingTimeMs?: number
}

// Convenience wrapper with shorthand methods
export const api = {
  async get<T>(url: string, params?: Record<string, string | number | boolean>): Promise<T> {
    return apiClient<T>({ url, method: 'GET', params })
  },

  async post<T>(url: string, data?: unknown): Promise<T> {
    return apiClient<T>({ url, method: 'POST', data })
  },

  async put<T>(url: string, data?: unknown): Promise<T> {
    return apiClient<T>({ url, method: 'PUT', data })
  },

  async delete<T>(url: string): Promise<T> {
    return apiClient<T>({ url, method: 'DELETE' })
  },
}
