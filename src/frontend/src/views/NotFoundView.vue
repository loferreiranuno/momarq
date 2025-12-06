<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

function goHome() {
  router.push('/')
}
</script>

<template>
  <div class="not-found">
    <span class="not-found__icon">🔍</span>
    <h1 class="not-found__title">Page Not Found</h1>
    <p class="not-found__message">
      The page you're looking for doesn't exist or has been moved.
    </p>
    <button class="btn btn--primary" @click="goHome">Go Home</button>
  </div>
</template>

<style lang="scss" scoped>
.not-found {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 60vh;
  text-align: center;
  padding: var(--space-8);

  &__icon {
    font-size: 5rem;
    margin-bottom: var(--space-6);
  }

  &__title {
    font-family: var(--font-heading);
    font-size: var(--text-4xl);
    margin-bottom: var(--space-4);
  }

  &__message {
    color: var(--color-text-muted);
    margin-bottom: var(--space-8);
    max-width: 400px;
  }
}
</style>
