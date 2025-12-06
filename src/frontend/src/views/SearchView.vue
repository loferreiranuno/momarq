<script setup lang="ts">
import { computed } from 'vue'
import { useSearchStore } from '@/stores/search'
import ImageUpload from '@/components/search/ImageUpload.vue'
import SearchResults from '@/components/search/SearchResults.vue'
import { Sparkles } from 'lucide-vue-next'

const searchStore = useSearchStore()

const showResults = computed(() => searchStore.hasSearched && !searchStore.isSearching)
</script>

<template>
  <div class="search-view">
    <div class="search-view__container">
      <!-- Header -->
      <header class="search-view__header">
        <h1 class="search-view__title">Visual Search</h1>
        <p class="search-view__subtitle">Upload an image to find similar furniture in our catalog.</p>
      </header>

      <!-- Upload Area -->
      <div class="search-view__upload">
        <ImageUpload />
      </div>

      <!-- Results Section -->
      <SearchResults v-if="showResults" />

      <!-- Empty State (no search yet) -->
      <div 
        v-else-if="!searchStore.hasSearched && !searchStore.isSearching"
        class="search-view__empty"
      >
        <div class="search-view__empty-icon">
          <Sparkles :stroke-width="1.5" />
        </div>
        <h3 class="search-view__empty-title">Ready to discover</h3>
        <p class="search-view__empty-text">
          Upload a photo of furniture you like, and we'll find similar pieces from our catalog.
        </p>
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.search-view {
  min-height: 100vh;
  padding: var(--space-8) 0 var(--space-16);
  background-color: var(--color-background);

  &__container {
    max-width: var(--max-width);
    margin: 0 auto;
    padding: 0 var(--space-6);
  }

  &__header {
    text-align: center;
    margin-bottom: var(--space-8);
  }

  &__title {
    font-size: var(--text-3xl);
    font-weight: 700;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-3);
  }

  &__subtitle {
    font-size: var(--text-lg);
    color: var(--color-text-secondary);
    margin: 0;
    max-width: 500px;
    margin-left: auto;
    margin-right: auto;
  }

  &__upload {
    max-width: 560px;
    margin: 0 auto var(--space-12);
  }

  &__empty {
    text-align: center;
    padding: var(--space-16) 0;

    &-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 80px;
      height: 80px;
      background-color: var(--color-surface);
      border-radius: var(--radius-full);
      margin-bottom: var(--space-6);
      color: var(--color-text-muted);

      svg {
        width: 40px;
        height: 40px;
      }
    }

    &-title {
      font-size: var(--text-xl);
      font-weight: 600;
      color: var(--color-text-primary);
      margin: 0 0 var(--space-2);
    }

    &-text {
      font-size: var(--text-base);
      color: var(--color-text-muted);
      margin: 0;
      max-width: 400px;
      margin-left: auto;
      margin-right: auto;
    }
  }
}
</style>
