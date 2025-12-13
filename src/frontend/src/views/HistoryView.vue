<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ProductCard from '@/components/search/ProductCard.vue'
import { getSearchHistory, clearSearchHistory, type SearchHistoryItem } from '@/db'
import { Clock, Trash2, Search, Camera } from 'lucide-vue-next'
import ConfirmModal from '@/components/ConfirmModal.vue'

const history = ref<SearchHistoryItem[]>([])
const isLoading = ref(true)

// Clear history confirmation
const showClearConfirm = ref(false)

onMounted(async () => {
  try {
    history.value = await getSearchHistory()
  } finally {
    isLoading.value = false
  }
})

function handleClearHistoryClick() {
  showClearConfirm.value = true
}

async function handleClearHistory() {
  await clearSearchHistory()
  history.value = []
  showClearConfirm.value = false
}

function cancelClearHistory() {
  showClearConfirm.value = false
}

function formatDate(timestamp: number): string {
  return new Intl.DateTimeFormat('pt-PT', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(timestamp))
}
</script>

<template>
  <div class="history">
    <div class="history__container">
      <!-- Header -->
      <header class="history__header">
        <h1 class="history__title">Search History</h1>
        <button
          v-if="history.length > 0"
          class="history__clear-btn"
          @click="handleClearHistoryClick"
        >
          <Trash2 :stroke-width="1.5" />
          Clear History
        </button>
      </header>

      <!-- Loading -->
      <div v-if="isLoading" class="history__loading">
        <div class="history__spinner"></div>
        <p>Loading history...</p>
      </div>

      <!-- Empty State -->
      <div v-else-if="history.length === 0" class="history__empty">
        <div class="history__empty-icon">
          <Camera :stroke-width="1.5" />
        </div>
        <h2 class="history__empty-title">No searches yet</h2>
        <p class="history__empty-text">Your visual search history will appear here</p>
        <router-link to="/search" class="history__start-btn">
          <Search :stroke-width="2" />
          Start Searching
        </router-link>
      </div>

      <!-- History List -->
      <div v-else class="history__list">
        <article v-for="item in history" :key="item.id" class="history__item">
          <div class="history__item-content">
            <!-- Thumbnail -->
            <div class="history__thumbnail">
              <img
                :src="item.thumbnail"
                :alt="`Search from ${formatDate(item.timestamp)}`"
              />
            </div>

            <!-- Info -->
            <div class="history__info">
              <div class="history__meta">
                <Clock :stroke-width="1.5" />
                <time>{{ formatDate(item.timestamp) }}</time>
              </div>
              <p class="history__results-count">{{ item.resultsCount }} results found</p>

              <!-- Preview Grid -->
              <div v-if="item.results && item.results.length > 0" class="history__preview">
                <h3 class="history__preview-title">Top Results</h3>
                <div class="history__preview-grid">
                  <ProductCard
                    v-for="product in item.results.slice(0, 3)"
                    :key="product.productId"
                    :product="product"
                    :show-similarity="true"
                  />
                </div>
              </div>
            </div>
          </div>
        </article>
      </div>
    </div>
  </div>

  <!-- Clear History Confirmation Modal -->
  <ConfirmModal
    v-model="showClearConfirm"
    title="Clear Search History"
    message="Are you sure you want to clear your search history? This action cannot be undone."
    confirm-text="Clear"
    cancel-text="Cancel"
    variant="danger"
    @confirm="handleClearHistory"
    @cancel="cancelClearHistory"
  />
</template>

<style lang="scss" scoped>
.history {
  min-height: 100vh;
  padding: var(--space-8) 0;
  background-color: var(--color-background);

  &__container {
    max-width: var(--max-width);
    margin: 0 auto;
    padding: 0 var(--space-6);
  }

  &__header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: var(--space-8);
  }

  &__title {
    font-size: var(--text-2xl);
    font-weight: 700;
    color: var(--color-text-primary);
    margin: 0;
  }

  &__clear-btn {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-2) var(--space-4);
    font-size: var(--text-sm);
    font-weight: 500;
    color: var(--color-text-secondary);
    background: none;
    border: 1px solid var(--color-border);
    border-radius: var(--radius-md);
    cursor: pointer;
    transition: all var(--transition-fast);

    svg {
      width: 16px;
      height: 16px;
    }

    &:hover {
      color: var(--color-error);
      border-color: var(--color-error);
    }
  }

  &__loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: var(--space-16) 0;
    color: var(--color-text-muted);

    p {
      margin: 0;
    }
  }

  &__spinner {
    width: 40px;
    height: 40px;
    border: 3px solid var(--color-border);
    border-top-color: var(--color-primary);
    border-radius: 50%;
    margin-bottom: var(--space-4);
    animation: spin 0.8s linear infinite;
  }

  &__empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: var(--space-16) 0;
    text-align: center;

    &-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 80px;
      height: 80px;
      background-color: var(--color-primary-light);
      border-radius: var(--radius-full);
      margin-bottom: var(--space-6);
      color: var(--color-primary);

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
      margin: 0 0 var(--space-6);
    }
  }

  &__start-btn {
    display: inline-flex;
    align-items: center;
    gap: var(--space-2);
    padding: var(--space-3) var(--space-6);
    font-size: var(--text-sm);
    font-weight: 600;
    color: #fff;
    background-color: var(--color-primary);
    border-radius: var(--radius-md);
    text-decoration: none;
    transition: background-color var(--transition-fast);

    svg {
      width: 18px;
      height: 18px;
    }

    &:hover {
      background-color: var(--color-primary-hover);
    }
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: var(--space-6);
  }

  &__item {
    background-color: var(--color-surface);
    border-radius: var(--radius-lg);
    padding: var(--space-6);

    &-content {
      display: flex;
      gap: var(--space-6);
      flex-direction: column;

      @media (min-width: 640px) {
        flex-direction: row;
      }
    }
  }

  &__thumbnail {
    width: 112px;
    height: 112px;
    flex-shrink: 0;
    border-radius: var(--radius-md);
    overflow: hidden;
    background-color: var(--color-background);

    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
  }

  &__info {
    flex: 1;
  }

  &__meta {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin-bottom: var(--space-2);

    svg {
      width: 16px;
      height: 16px;
    }
  }

  &__results-count {
    font-size: var(--text-base);
    font-weight: 600;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-4);
  }

  &__preview {
    margin-top: var(--space-4);

    &-title {
      font-size: var(--text-sm);
      font-weight: 500;
      color: var(--color-text-secondary);
      margin: 0 0 var(--space-3);
    }

    &-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: var(--space-4);

      @media (min-width: 768px) {
        grid-template-columns: repeat(3, 1fr);
      }
    }
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
