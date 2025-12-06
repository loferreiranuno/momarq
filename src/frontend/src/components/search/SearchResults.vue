<script setup lang="ts">
import { computed } from 'vue'
import { useSearchStore } from '@/stores/search'
import { useSettingsStore } from '@/stores/settings'
import ProductCard from './ProductCard.vue'
import { Search, Zap } from 'lucide-vue-next'

const searchStore = useSearchStore()
const settingsStore = useSettingsStore()

const hasResults = computed(() => searchStore.results.length > 0)
const showSimilarity = computed(() => settingsStore.showSimilarityScore)
</script>

<template>
  <div class="search-results">
    <!-- Results Header -->
    <header v-if="hasResults" class="search-results__header">
      <h2 class="search-results__count">
        Found {{ searchStore.resultCount }} similar product{{ searchStore.resultCount !== 1 ? 's' : '' }}
      </h2>
      <span v-if="searchStore.processingTimeMs" class="search-results__time">
        <Zap :stroke-width="2" />
        {{ searchStore.processingTimeMs }}ms
      </span>
    </header>

    <!-- Products Grid -->
    <div v-if="hasResults" class="search-results__grid">
      <ProductCard
        v-for="product in searchStore.results"
        :key="product.productId"
        :product="product"
        :show-similarity="showSimilarity"
      />
    </div>

    <!-- Empty State -->
    <div v-else class="search-results__empty">
      <div class="search-results__empty-icon">
        <Search :stroke-width="1.5" />
      </div>
      <h3 class="search-results__empty-title">No products found</h3>
      <p class="search-results__empty-text">
        Try uploading a different image or adjust your search.
      </p>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.search-results {
  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding-bottom: var(--space-4);
    margin-bottom: var(--space-6);
    border-bottom: 1px solid var(--color-border);
  }

  &__count {
    font-size: var(--text-lg);
    font-weight: 600;
    color: var(--color-text-primary);
    margin: 0;
  }

  &__time {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    padding: var(--space-1) var(--space-3);
    font-size: var(--text-sm);
    font-weight: 500;
    color: var(--color-success);
    background-color: #ECFDF5;
    border-radius: var(--radius-full);

    svg {
      width: 14px;
      height: 14px;
    }
  }

  &__grid {
    display: grid;
    grid-template-columns: repeat(1, 1fr);
    gap: var(--space-6);

    @media (min-width: 640px) {
      grid-template-columns: repeat(2, 1fr);
    }

    @media (min-width: 1024px) {
      grid-template-columns: repeat(3, 1fr);
    }

    @media (min-width: 1280px) {
      grid-template-columns: repeat(4, 1fr);
    }
  }

  &__empty {
    text-align: center;
    padding: var(--space-16) var(--space-4);
    background-color: var(--color-surface);
    border-radius: var(--radius-lg);

    &-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 64px;
      background-color: var(--color-background);
      border-radius: var(--radius-full);
      margin-bottom: var(--space-4);
      color: var(--color-text-muted);

      svg {
        width: 32px;
        height: 32px;
      }
    }

    &-title {
      font-size: var(--text-lg);
      font-weight: 600;
      color: var(--color-text-primary);
      margin: 0 0 var(--space-2);
    }

    &-text {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
      margin: 0;
      max-width: 300px;
      margin-left: auto;
      margin-right: auto;
    }
  }
}
</style>
