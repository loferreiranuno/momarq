<script setup lang="ts">
defineProps<{
  message: string
}>()

const emit = defineEmits<{
  retry: []
}>()
</script>

<template>
  <div class="error-state card">
    <span class="error-state__icon">⚠️</span>
    <p class="error-state__message">{{ message }}</p>
    <button class="btn btn--outline btn--sm" @click="emit('retry')">
      Retry
    </button>
  </div>
</template>

<style lang="scss" scoped>
.error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-6);
  background: var(--color-danger-bg, #fee2e2);
  border-color: var(--color-danger, #dc2626);

  &__icon {
    font-size: 2rem;
    line-height: 1;
  }

  &__message {
    font-size: var(--text-base);
    color: var(--color-danger, #dc2626);
    font-weight: 500;
    margin: 0;
    text-align: center;
  }
}
</style>
