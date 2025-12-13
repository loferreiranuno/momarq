<script setup lang="ts">
import { ref, onMounted } from 'vue'
import ProductCard from '@/components/search/ProductCard.vue'
import { getFavorites, clearFavorites, type FavoriteItem } from '@/db'
import { Heart, Trash2, Search } from 'lucide-vue-next'
import ConfirmModal from '@/components/ConfirmModal.vue'

const favorites = ref<FavoriteItem[]>([])
const isLoading = ref(true)

// Clear favorites confirmation
const showClearConfirm = ref(false)

onMounted(async () => {
  try {
    favorites.value = await getFavorites()
  } finally {
    isLoading.value = false
  }
})

function handleClearFavoritesClick() {
  showClearConfirm.value = true
}

async function handleClearFavorites() {
  await clearFavorites()
  favorites.value = []
  showClearConfirm.value = false
}

function cancelClearFavorites() {
  showClearConfirm.value = false
}
</script>

<template>
  <div class="favorites">
    <div class="favorites__container">
      <!-- Header -->
      <header class="favorites__header">
        <h1 class="favorites__title">Favorites</h1>
        <button
          v-if="favorites.length > 0"
          class="favorites__clear-btn"
          @click="handleClearFavoritesClick"
        >
          <Trash2 :stroke-width="1.5" />
          Clear All
        </button>
      </header>

      <!-- Loading -->
      <div v-if="isLoading" class="favorites__loading">
        <div class="favorites__spinner"></div>
        <p>Loading favorites...</p>
      </div>

      <!-- Empty State -->
      <div v-else-if="favorites.length === 0" class="favorites__empty">
        <div class="favorites__empty-icon favorites__empty-icon--heart">
          <Heart :stroke-width="1.5" />
        </div>
        <h2 class="favorites__empty-title">No favorites yet</h2>
        <p class="favorites__empty-text">Items you love will appear here</p>
        <router-link to="/search" class="favorites__start-btn">
          <Search :stroke-width="2" />
          Start Searching
        </router-link>
      </div>

      <!-- Grid -->
      <div v-else class="favorites__grid">
        <ProductCard
          v-for="item in favorites"
          :key="item.id"
          :product="{
            productId: item.productId,
            name: item.name,
            price: item.price,
            imageUrl: item.imageUrl,
            providerName: item.providerName,
            similarity: 1,
          }"
        />
      </div>
    </div>
  </div>

  <!-- Clear Favorites Confirmation Modal -->
  <ConfirmModal
    v-model="showClearConfirm"
    title="Clear All Favorites"
    message="Are you sure you want to clear all favorites? This action cannot be undone."
    confirm-text="Clear"
    cancel-text="Cancel"
    variant="danger"
    @confirm="handleClearFavorites"
    @cancel="cancelClearFavorites"
  />
</template>

<style lang="scss" scoped>
.favorites {
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
      border-radius: var(--radius-full);
      margin-bottom: var(--space-6);

      svg {
        width: 40px;
        height: 40px;
      }

      &--heart {
        background-color: #FEF2F2;
        color: #FCA5A5;
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
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
