<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const username = ref('')
const password = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const error = ref('')
const isLoading = ref(false)

const requiresPasswordChange = computed(() => authStore.mustChangePassword)

async function handleLogin() {
  error.value = ''

  if (!username.value || !password.value) {
    error.value = 'Please enter username and password'
    return
  }

  isLoading.value = true
  try {
    const result = await authStore.login(username.value, password.value)
    if (result.success) {
      if (result.mustChangePassword) {
        // Stay on page to show password change form
      } else {
        router.push('/admin/dashboard')
      }
    } else {
      error.value = result.error || 'Login failed'
    }
  } catch (e) {
    error.value = 'An error occurred. Please try again.'
  } finally {
    isLoading.value = false
  }
}

async function handlePasswordChange() {
  error.value = ''

  if (!newPassword.value || !confirmPassword.value) {
    error.value = 'Please fill in all fields'
    return
  }

  if (newPassword.value !== confirmPassword.value) {
    error.value = 'Passwords do not match'
    return
  }

  if (newPassword.value.length < 8) {
    error.value = 'Password must be at least 8 characters'
    return
  }

  isLoading.value = true
  try {
    const success = await authStore.changePassword(password.value, newPassword.value)
    if (success) {
      router.push('/admin/dashboard')
    } else {
      error.value = 'Failed to change password'
    }
  } catch (e) {
    error.value = 'An error occurred. Please try again.'
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="admin-login">
    <div class="admin-login__card card">
      <div class="admin-login__header">
        <h1 class="admin-login__title">Admin Panel</h1>
        <p class="admin-login__subtitle">
          {{ requiresPasswordChange ? 'Please set a new password' : 'Sign in to continue' }}
        </p>
      </div>

      <form
        v-if="!requiresPasswordChange"
        class="admin-login__form"
        @submit.prevent="handleLogin"
      >
        <div class="form-group">
          <label for="username" class="form-label">Username</label>
          <input
            id="username"
            v-model="username"
            type="text"
            class="form-input"
            placeholder="Enter username"
            autocomplete="username"
            required
          />
        </div>

        <div class="form-group">
          <label for="password" class="form-label">Password</label>
          <input
            id="password"
            v-model="password"
            type="password"
            class="form-input"
            placeholder="Enter password"
            autocomplete="current-password"
            required
          />
        </div>

        <p v-if="error" class="admin-login__error">{{ error }}</p>

        <button
          type="submit"
          class="btn btn--primary btn--block"
          :disabled="isLoading"
        >
          {{ isLoading ? 'Signing in...' : 'Sign In' }}
        </button>
      </form>

      <form
        v-else
        class="admin-login__form"
        @submit.prevent="handlePasswordChange"
      >
        <div class="admin-login__notice">
          <strong>First-time login detected</strong>
          <p>For security, please set a new password before continuing.</p>
        </div>

        <div class="form-group">
          <label for="newPassword" class="form-label">New Password</label>
          <input
            id="newPassword"
            v-model="newPassword"
            type="password"
            class="form-input"
            placeholder="Enter new password"
            autocomplete="new-password"
            required
          />
        </div>

        <div class="form-group">
          <label for="confirmPassword" class="form-label">Confirm Password</label>
          <input
            id="confirmPassword"
            v-model="confirmPassword"
            type="password"
            class="form-input"
            placeholder="Confirm new password"
            autocomplete="new-password"
            required
          />
        </div>

        <p v-if="error" class="admin-login__error">{{ error }}</p>

        <button
          type="submit"
          class="btn btn--primary btn--block"
          :disabled="isLoading"
        >
          {{ isLoading ? 'Saving...' : 'Set Password & Continue' }}
        </button>
      </form>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.admin-login {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-4);
  background-color: var(--color-background);

  &__card {
    width: 100%;
    max-width: 400px;
    padding: var(--space-8);
  }

  &__header {
    text-align: center;
    margin-bottom: var(--space-8);
  }

  &__title {
    font-family: var(--font-heading);
    font-size: var(--text-3xl);
    margin-bottom: var(--space-2);
  }

  &__subtitle {
    color: var(--color-text-muted);
    margin-bottom: 0;
  }

  &__form {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }

  &__error {
    color: var(--color-error);
    font-size: var(--text-sm);
    text-align: center;
    margin-bottom: 0;
  }

  &__notice {
    padding: var(--space-4);
    background-color: var(--color-warning-bg, #FFF3CD);
    border-radius: var(--radius-md);
    text-align: center;
    margin-bottom: var(--space-4);

    strong {
      display: block;
      margin-bottom: var(--space-1);
    }

    p {
      font-size: var(--text-sm);
      color: var(--color-text-secondary);
      margin-bottom: 0;
    }
  }
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.form-label {
  font-size: var(--text-sm);
  font-weight: 500;
}

.form-input {
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);

  &:focus {
    outline: none;
    border-color: var(--color-accent);
    box-shadow: 0 0 0 3px var(--color-accent-light);
  }

  &::placeholder {
    color: var(--color-text-muted);
  }
}

.btn--block {
  width: 100%;
}
</style>
