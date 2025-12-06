<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useSettingsStore } from '@/stores/settings'
import { useAuthStore } from '@/stores/auth'
import { Home, Search, Clock, Heart, Settings, LogOut, User } from 'lucide-vue-next'

const route = useRoute()
const settingsStore = useSettingsStore()
const authStore = useAuthStore()

const siteName = computed(() => settingsStore.siteName)
const isActive = (name: string) => route.name === name

const navLinks = [
  { name: 'home', label: 'Home', icon: Home },
  { name: 'search', label: 'Search', icon: Search },
  { name: 'history', label: 'History', icon: Clock },
  { name: 'favorites', label: 'Favorites', icon: Heart },
]
</script>

<template>
  <header class="app-header">
    <div class="app-header__container">
      <!-- Logo -->
      <RouterLink to="/" class="app-header__logo">
        <span class="app-header__logo-text">{{ siteName }}</span>
      </RouterLink>

      <!-- Navigation -->
      <nav class="app-header__nav">
        <RouterLink
          v-for="link in navLinks"
          :key="link.name"
          :to="{ name: link.name }"
          class="app-header__nav-link"
          :class="{ 'app-header__nav-link--active': isActive(link.name) }"
        >
          <component :is="link.icon" class="app-header__nav-icon" :stroke-width="1.5" />
          <span>{{ link.label }}</span>
        </RouterLink>
      </nav>

      <!-- Actions -->
      <div class="app-header__actions">
        <template v-if="authStore.isAuthenticated">
          <RouterLink to="/admin" class="app-header__action">
            <Settings class="app-header__action-icon" :stroke-width="1.5" />
            <span class="app-header__action-label">Admin</span>
          </RouterLink>
          <button class="app-header__action" @click="authStore.logout()">
            <LogOut class="app-header__action-icon" :stroke-width="1.5" />
            <span class="app-header__action-label">Logout</span>
          </button>
        </template>
        <template v-else>
          <RouterLink to="/admin/login" class="app-header__admin-btn">
            <User class="app-header__action-icon" :stroke-width="1.5" />
            <span>Admin</span>
          </RouterLink>
        </template>
      </div>
    </div>
  </header>
</template>

<style lang="scss" scoped>
.app-header {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: var(--header-height);
  background-color: var(--color-surface);
  border-bottom: 1px solid var(--color-border);
  z-index: 100;

  &__container {
    max-width: var(--max-width);
    margin: 0 auto;
    padding: 0 var(--space-6);
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-8);
  }

  &__logo {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    text-decoration: none;
    
    &-text {
      font-family: var(--font-display);
      font-size: var(--text-xl);
      font-weight: 600;
      color: var(--color-primary);
      letter-spacing: -0.5px;
    }
  }

  &__nav {
    display: none;
    align-items: center;
    gap: var(--space-1);

    @media (min-width: 768px) {
      display: flex;
    }
  }

  &__nav-link {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-4);
    font-size: var(--text-sm);
    font-weight: 500;
    color: var(--color-text-secondary);
    text-decoration: none;
    border-radius: var(--radius-md);
    transition: all var(--transition-fast);

    &:hover {
      color: var(--color-text-primary);
      background-color: var(--color-primary-light);
    }

    &--active {
      color: var(--color-primary);
      background-color: var(--color-primary-light);
    }
  }

  &__nav-icon {
    width: 18px;
    height: 18px;
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  &__action {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2);
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    background: none;
    border: none;
    cursor: pointer;
    transition: color var(--transition-fast);

    &:hover {
      color: var(--color-primary);
    }

    &-icon {
      width: 18px;
      height: 18px;
    }

    &-label {
      display: none;
      @media (min-width: 640px) {
        display: inline;
      }
    }
  }

  &__admin-btn {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-4);
    font-size: var(--text-sm);
    font-weight: 500;
    color: var(--color-surface);
    background-color: var(--color-primary);
    border-radius: var(--radius-md);
    text-decoration: none;
    transition: background-color var(--transition-fast);

    &:hover {
      background-color: var(--color-primary-hover);
    }
  }
}
</style>