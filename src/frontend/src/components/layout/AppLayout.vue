<script setup lang="ts">
import { onMounted, onUnmounted } from 'vue'
import { useSettingsStore } from '@/stores/settings'
import AppHeader from './AppHeader.vue'
import AppFooter from './AppFooter.vue'

const settingsStore = useSettingsStore()

onMounted(async () => {
  await settingsStore.fetchPublicSettings()
  settingsStore.connectSSE()
})

onUnmounted(() => {
  settingsStore.disconnectSSE()
})
</script>

<template>
  <div class="app-layout">
    <AppHeader />
    <main class="app-layout__main">
      <slot />
    </main>
    <AppFooter />
  </div>
</template>

<style lang="scss" scoped>
.app-layout {
  display: flex;
  flex-direction: column;
  min-height: 100vh;

  &__main {
    flex: 1;
    padding-top: var(--header-height);
  }
}
</style>
