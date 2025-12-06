<script setup lang="ts">
import { useSettingsStore } from '@/stores/settings'

const settingsStore = useSettingsStore()
const currentYear = new Date().getFullYear()
</script>

<template>
  <footer class="app-footer">
    <div class="app-footer__container">
      <!-- Brand -->
      <div class="app-footer__brand">
        <span class="app-footer__logo">{{ settingsStore.siteName }}</span>
        <p class="app-footer__tagline">Discover furniture through visual search</p>
      </div>

      <!-- Links -->
      <nav class="app-footer__nav">
        <RouterLink to="/" class="app-footer__link">Home</RouterLink>
        <RouterLink to="/search" class="app-footer__link">Search</RouterLink>
        <RouterLink to="/history" class="app-footer__link">History</RouterLink>
        <RouterLink to="/favorites" class="app-footer__link">Favorites</RouterLink>
      </nav>

      <!-- Copyright -->
      <p class="app-footer__copyright">
        © {{ currentYear }} {{ settingsStore.siteName }}. All rights reserved.
      </p>
    </div>
  </footer>
</template>

<style lang="scss" scoped>
.app-footer {
  margin-top: auto;
  padding: var(--space-12) 0;
  background-color: var(--color-primary-light);
  border-top: 1px solid var(--color-border);

  &__container {
    max-width: var(--max-width);
    margin: 0 auto;
    padding: 0 var(--space-6);
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-6);
    text-align: center;
  }

  &__brand {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: var(--space-2);
  }

  &__logo {
    font-family: var(--font-display);
    font-size: var(--text-lg);
    font-weight: 700;
    color: var(--color-primary);
  }

  &__tagline {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin: 0;
  }

  &__nav {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: var(--space-4);
  }

  &__link {
    font-size: var(--text-sm);
    color: var(--color-text-secondary);
    text-decoration: none;
    transition: color var(--transition-fast);

    &:hover {
      color: var(--color-primary);
    }
  }

  &__copyright {
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    margin: 0;
  }
}
</style>
