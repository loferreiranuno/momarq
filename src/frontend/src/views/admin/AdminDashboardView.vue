<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useSettingsStore } from '@/stores/settings'
import { api } from '@/api/client'

interface ApiStats {
  products: number
  providers: number
  images: number
  vectorizedImages: number
  vectorizationProgress: number
}

interface Stats {
  totalProducts: number
  totalProviders: number
  totalImages: number
  vectorizedImages: number
  vectorizationProgress: number
}

const settingsStore = useSettingsStore()

const stats = ref<Stats>({
  totalProducts: 0,
  totalProviders: 0,
  totalImages: 0,
  vectorizedImages: 0,
  vectorizationProgress: 0,
})
const isLoading = ref(true)

onMounted(async () => {
  try {
    const response = await api.get<ApiStats>('/api/admin/stats')
    stats.value = {
      totalProducts: response?.products ?? 0,
      totalProviders: response?.providers ?? 0,
      totalImages: response?.images ?? 0,
      vectorizedImages: response?.vectorizedImages ?? 0,
      vectorizationProgress: response?.vectorizationProgress ?? 0,
    }
  } catch (e) {
    console.error('Failed to load stats:', e)
  } finally {
    isLoading.value = false
  }
})
</script>

<template>
  <div class="admin-dashboard"> 
    <header class="admin-dashboard__header">
      <h1 class="page-title">Dashboard</h1>
      <p class="admin-dashboard__subtitle">Welcome to the Visual Search admin panel</p>
    </header>

    <div v-if="isLoading" class="admin-dashboard__loading">
      <span class="spinner"></span>
      <p>Loading statistics...</p>
    </div>

    <template v-else>
      <section class="admin-dashboard__stats">
        <div class="stat-card card">
          <span class="stat-card__icon">📦</span>
          <div class="stat-card__content">
            <span class="stat-card__value">{{ stats.totalProducts.toLocaleString() }}</span>
            <span class="stat-card__label">Products</span>
          </div>
        </div>

        <div class="stat-card card">
          <span class="stat-card__icon">🏪</span>
          <div class="stat-card__content">
            <span class="stat-card__value">{{ stats.totalProviders.toLocaleString() }}</span>
            <span class="stat-card__label">Providers</span>
          </div>
        </div>

        <div class="stat-card card">
          <span class="stat-card__icon">🖼️</span>
          <div class="stat-card__content">
            <span class="stat-card__value">{{ stats.totalImages.toLocaleString() }}</span>
            <span class="stat-card__label">Images</span>
          </div>
        </div>

        <div class="stat-card card">
          <span class="stat-card__icon">🔍</span>
          <div class="stat-card__content">
            <span class="stat-card__value">{{ stats.vectorizedImages.toLocaleString() }}</span>
            <span class="stat-card__label">Vectorized</span>
          </div>
        </div>
      </section>

      <section class="admin-dashboard__quick-settings">
        <h2 class="section-title">Quick Settings</h2>
        <div class="settings-preview card">
          <div class="settings-preview__item">
            <span class="settings-preview__label">Site Name</span>
            <span class="settings-preview__value">
              {{ settingsStore.getSetting('ui.siteName') || 'Visual Search' }}
            </span>
          </div>
          <div class="settings-preview__item">
            <span class="settings-preview__label">Max Results</span>
            <span class="settings-preview__value">
              {{ settingsStore.getSetting('search.maxResults') || '20' }}
            </span>
          </div>
          <div class="settings-preview__item">
            <span class="settings-preview__label">Image Quality</span>
            <span class="settings-preview__value">
              {{ settingsStore.getSetting('search.jpegQuality') || '85' }}%
            </span>
          </div>
          <router-link to="/admin/settings" class="btn btn--outline btn--sm">
            Manage Settings
          </router-link>
        </div>
      </section>

      <section class="admin-dashboard__actions">
        <h2 class="section-title">Quick Actions</h2>
        <div class="actions-grid">
          <router-link to="/admin/providers" class="action-card card card--hoverable">
            <span class="action-card__icon">🏪</span>
            <h3 class="action-card__title">Providers</h3>
            <p class="action-card__description">Manage product providers</p>
          </router-link>

          <router-link to="/admin/products" class="action-card card card--hoverable">
            <span class="action-card__icon">📦</span>
            <h3 class="action-card__title">Products</h3>
            <p class="action-card__description">Manage products and images</p>
          </router-link>

          <router-link to="/admin/jobs" class="action-card card card--hoverable">
            <span class="action-card__icon">🕷️</span>
            <h3 class="action-card__title">Crawl Jobs</h3>
            <p class="action-card__description">Manage crawl jobs</p>
          </router-link>

          <router-link to="/admin/settings" class="action-card card card--hoverable">
            <span class="action-card__icon">⚙️</span>
            <h3 class="action-card__title">Settings</h3>
            <p class="action-card__description">Configure application settings</p>
          </router-link>

          <a href="/swagger" target="_blank" class="action-card card card--hoverable">
            <span class="action-card__icon">📚</span>
            <h3 class="action-card__title">API Docs</h3>
            <p class="action-card__description">View API documentation</p>
          </a>

          <router-link to="/" class="action-card card card--hoverable">
            <span class="action-card__icon">🏠</span>
            <h3 class="action-card__title">View Site</h3>
            <p class="action-card__description">Go to public website</p>
          </router-link>
        </div>
      </section>
    </template>
  </div>
</template>

<style lang="scss" scoped>
.admin-dashboard {
  &__header {
    margin-bottom: var(--space-8);
  }

  &__subtitle {
    color: var(--color-text-muted);
    margin-bottom: 0;
  }

  &__loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: var(--space-16);
    color: var(--color-text-muted);
  }

  &__stats {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: var(--space-6);
    margin-bottom: var(--space-10);
  }

  &__quick-settings {
    margin-bottom: var(--space-10);
  }

  &__actions {
    margin-bottom: var(--space-10);
  }
}

.section-title {
  font-family: var(--font-heading);
  font-size: var(--text-xl);
  margin-bottom: var(--space-4);
}

.stat-card {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  padding: var(--space-6);

  &__icon {
    font-size: 2.5rem;
  }

  &__content {
    display: flex;
    flex-direction: column;
  }

  &__value {
    font-family: var(--font-heading);
    font-size: var(--text-3xl);
    font-weight: 600;
    line-height: 1;
  }

  &__label {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin-top: var(--space-1);
  }
}

.settings-preview {
  padding: var(--space-6);
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-6);
  align-items: center;

  &__item {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
  }

  &__label {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  &__value {
    font-weight: 600;
  }

  .btn {
    margin-left: auto;
  }
}

.actions-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: var(--space-6);
}

.action-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: var(--space-8);
  text-decoration: none;
  color: inherit;

  &__icon {
    font-size: 2.5rem;
    margin-bottom: var(--space-4);
  }

  &__title {
    font-family: var(--font-heading);
    font-size: var(--text-lg);
    margin-bottom: var(--space-2);
  }

  &__description {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin-bottom: 0;
  }
}
</style>
