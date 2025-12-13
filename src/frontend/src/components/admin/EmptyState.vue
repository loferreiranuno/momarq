<script setup lang="ts">
defineProps<{
  icon?: string
  title: string
  message?: string
  actionLabel?: string
}>()

const emit = defineEmits<{
  action: []
}>()
</script>

<template>
  <div class="empty-state card">
    <span v-if="icon" class="empty-state__icon">{{ icon }}</span>
    <h3 class="empty-state__title">{{ title }}</h3>
    <p v-if="message" class="empty-state__message">{{ message }}</p>
    <button v-if="actionLabel" class="btn btn--primary" @click="emit('action')">
      {{ actionLabel }}
    </button>
  </div>
</template>

<style lang="scss" scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--space-12) var(--space-6);
  text-align: center;
  gap: var(--space-4);

  &__icon {
    font-size: 4rem;
    line-height: 1;
    opacity: 0.5;
  }

  &__title {
    font-size: var(--text-xl);
    font-weight: 600;
    color: var(--color-text-primary);
    margin: 0;
  }

  &__message {
    font-size: var(--text-base);
    color: var(--color-text-muted);
    max-width: 400px;
    margin: 0;
  }
}
</style>
