<script setup lang="ts">
defineProps<{
  title: string
  subtitle?: string
}>()
</script>

<template>
  <header class="admin-page-header">
    <div class="admin-page-header__content">
      <div>
        <h1 class="admin-page-header__title">{{ title }}</h1>
        <p v-if="subtitle" class="admin-page-header__subtitle">{{ subtitle }}</p>
      </div>
      <div v-if="$slots.actions" class="admin-page-header__actions">
        <slot name="actions" />
      </div>
    </div>
  </header>
</template>

<style lang="scss" scoped>
.admin-page-header {
  margin-bottom: var(--space-8);

  &__content {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: var(--space-6);
    flex-wrap: wrap;
  }

  &__title {
    font-size: var(--text-3xl);
    font-weight: 700;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-2) 0;
  }

  &__subtitle {
    font-size: var(--text-base);
    color: var(--color-text-muted);
    margin: 0;
  }

  &__actions {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    flex-shrink: 0;
  }
}

@media (max-width: 640px) {
  .admin-page-header {
    &__content {
      flex-direction: column;
      align-items: stretch;
    }

    &__actions {
      width: 100%;
      
      :deep(.btn) {
        flex: 1;
      }
    }
  }
}
</style>
