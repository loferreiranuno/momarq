<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { LayoutDashboard, Package, ShoppingBag, ListChecks, Settings, LogOut, ArrowLeft, ImportIcon } from 'lucide-vue-next'

const route = useRoute()
const authStore = useAuthStore()

const isActive = (name: string) => route.name === name

const navLinks = [
  { name: 'admin-dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { name: 'admin-products', label: 'Products', icon: Package },
  { name: 'admin-providers', label: 'Providers', icon: ShoppingBag },
  { name: 'admin-jobs', label: 'Crawl Jobs', icon: ListChecks },
  { name: 'admin-settings', label: 'Settings', icon: Settings },
  { name: 'admin-import', label: 'Import', icon: ImportIcon },
]

const showBackButton = computed(() => {
  return route.name !== 'admin-dashboard' && route.name !== 'admin-login'
})
</script>

<template>
  <div class="admin-layout">
    <!-- Admin Header -->
    <header class="admin-header">
      <div class="admin-header__container">
        <!-- Logo / Brand -->
        <div class="admin-header__brand">
          <span class="admin-header__logo">🔧</span>
          <span class="admin-header__title">Admin Panel</span>
        </div>

        <!-- Navigation -->
        <nav class="admin-header__nav">
          <RouterLink
            v-for="link in navLinks"
            :key="link.name"
            :to="{ name: link.name }"
            class="admin-header__nav-link"
            :class="{ 'admin-header__nav-link--active': isActive(link.name) }"
          >
            <component :is="link.icon" class="admin-header__nav-icon" :stroke-width="1.5" />
            <span>{{ link.label }}</span>
          </RouterLink>
        </nav>

        <!-- User Actions -->
        <div class="admin-header__actions">
          <RouterLink to="/" class="admin-header__action" title="Back to site">
            <ArrowLeft class="admin-header__action-icon" :stroke-width="1.5" />
            <span class="admin-header__action-label">Site</span>
          </RouterLink>
          <button class="admin-header__action admin-header__action--danger" @click="authStore.logout()" title="Logout">
            <LogOut class="admin-header__action-icon" :stroke-width="1.5" />
            <span class="admin-header__action-label">Logout</span>
          </button>
        </div>
      </div>
    </header>

    <!-- Main Content -->
    <main class="admin-layout__main">
      <div class="admin-layout__container">
        <!-- Back Button for Sub-pages -->
        <div v-if="showBackButton" class="admin-layout__back">
          <button class="btn btn--ghost btn--sm" @click="$router.back()">
            <ArrowLeft :stroke-width="1.5" />
            Back
          </button>
        </div>

        <!-- Page Content -->
        <slot />
      </div>
    </main>
  </div>
</template>

<style lang="scss" scoped>
.admin-layout {
  min-height: 100vh;
  background: var(--color-bg-subtle, #f9fafb);
  display: flex;
  flex-direction: column;

  &__main {
    flex: 1;
    padding-top: var(--admin-header-height, 64px);
  }

  &__container {
    max-width: 1400px;
    margin: 0 auto;
    padding: var(--space-6) var(--space-6) var(--space-8);
  }

  &__back {
    margin-bottom: var(--space-4);
  }
}

.admin-header {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: var(--admin-header-height, 64px);
  background: var(--color-surface, #ffffff);
  border-bottom: 1px solid var(--color-border, #e5e7eb);
  z-index: 100;
  box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1);

  &__container {
    max-width: 1400px;
    margin: 0 auto;
    padding: 0 var(--space-6);
    height: 100%;
    display: flex;
    align-items: center;
    gap: var(--space-8);
  }

  &__brand {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    font-weight: 600;
    color: var(--color-text-primary);
    flex-shrink: 0;
  }

  &__logo {
    font-size: var(--text-2xl);
  }

  &__title {
    font-size: var(--text-lg);
  }

  &__nav {
    display: flex;
    align-items: center;
    gap: var(--space-1);
    flex: 1;
  }

  &__nav-link {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    text-decoration: none;
    color: var(--color-text-secondary, #6b7280);
    font-size: var(--text-sm);
    font-weight: 500;
    transition: all 0.2s ease;

    &:hover {
      background: var(--color-bg-hover, #f3f4f6);
      color: var(--color-text-primary);
    }

    &--active {
      background: var(--color-primary-light, #eff6ff);
      color: var(--color-primary, #3b82f6);

      .admin-header__nav-icon {
        color: var(--color-primary);
      }
    }
  }

  &__nav-icon {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    flex-shrink: 0;
  }

  &__action {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-md);
    text-decoration: none;
    background: transparent;
    border: none;
    color: var(--color-text-secondary);
    font-size: var(--text-sm);
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s ease;

    &:hover {
      background: var(--color-bg-hover);
      color: var(--color-text-primary);
    }

    &--danger:hover {
      background: var(--color-danger-bg, #fee2e2);
      color: var(--color-danger, #dc2626);
    }
  }

  &__action-icon {
    width: 18px;
    height: 18px;
    flex-shrink: 0;
  }

  &__action-label {
    @media (max-width: 768px) {
      display: none;
    }
  }
}

// Responsive
@media (max-width: 1024px) {
  .admin-header {
    &__nav {
      gap: 0;
    }

    &__nav-link {
      padding: var(--space-2);
      font-size: var(--text-xs);

      span {
        display: none;
      }
    }

    &__brand {
      &__title {
        display: none;
      }
    }
  }
}

@media (max-width: 640px) {
  .admin-layout {
    &__container {
      padding: var(--space-4);
    }
  }

  .admin-header {
    &__container {
      padding: 0 var(--space-4);
      gap: var(--space-4);
    }
  }
}
</style>
