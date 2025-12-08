import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiClient } from '@/api/client'

interface LoginResponse {
  token: string
  username: string
  mustChangePassword: boolean
  expiresAt: string
}

interface LoginResult {
  success: boolean
  mustChangePassword?: boolean
  error?: string
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('auth_token'))
  const username = ref<string | null>(localStorage.getItem('auth_username'))
  const mustChangePassword = ref(localStorage.getItem('auth_must_change') === 'true')
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const isAuthenticated = computed(() => !!token.value)

  async function login(usernameInput: string, password: string): Promise<LoginResult> {
    isLoading.value = true
    error.value = null

    try {
      const response = await apiClient<LoginResponse>({
        url: '/api/auth/login',
        method: 'POST',
        data: { username: usernameInput, password },
      })

      token.value = response.token
      username.value = response.username
      mustChangePassword.value = response.mustChangePassword

      localStorage.setItem('auth_token', response.token)
      localStorage.setItem('auth_username', response.username)
      localStorage.setItem('auth_must_change', String(response.mustChangePassword))

      return { success: true, mustChangePassword: response.mustChangePassword }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Login failed'
      error.value = errorMessage
      return { success: false, error: errorMessage }
    } finally {
      isLoading.value = false
    }
  }

  async function changePassword(usernameInput: string, currentPassword: string, newPassword: string): Promise<boolean> {
    isLoading.value = true
    error.value = null

    try {
      const response = await apiClient<{ success: boolean; token?: string; expiresAt?: string }>({
        url: '/api/auth/change-password',
        method: 'POST',
        data: { username: usernameInput, currentPassword, newPassword },
      })

      if (response.success && response.token) {
        // Store the new token
        token.value = response.token
        mustChangePassword.value = false
        localStorage.setItem('auth_token', response.token)
        localStorage.setItem('auth_must_change', 'false')
      }

      return true
    } catch (err) {
      error.value = err instanceof Error ? err.message : 'Password change failed'
      return false
    } finally {
      isLoading.value = false
    }
  }

  function logout() {
    token.value = null
    username.value = null
    mustChangePassword.value = false

    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_username')
    localStorage.removeItem('auth_must_change')
  }

  function getAuthHeader(): Record<string, string> {
    if (token.value) {
      return { Authorization: `Bearer ${token.value}` }
    }
    return {}
  }

  return {
    token,
    username,
    mustChangePassword,
    isLoading,
    error,
    isAuthenticated,
    login,
    changePassword,
    logout,
    getAuthHeader,
  }
})
