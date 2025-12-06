<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import type { ProductResult } from '@/api/client'
import { addFavorite, removeFavorite, isFavorite, addRecentlyViewed } from '@/db'
import { Heart } from 'lucide-vue-next'

const props = defineProps<{
  product: ProductResult
  showSimilarity?: boolean
}>()

const isLiked = ref(false)

const similarityPercent = computed(() => Math.round(props.product.similarity * 100))

const formattedPrice = computed(() => {
  return new Intl.NumberFormat('pt-PT', {
    style: 'currency',
    currency: 'EUR',
  }).format(props.product.price)
})

onMounted(async () => {
  isLiked.value = await isFavorite(props.product.productId)
})

async function toggleFavorite() {
  if (isLiked.value) {
    await removeFavorite(props.product.productId)
    isLiked.value = false
  } else {
    await addFavorite({
      productId: props.product.productId,
      name: props.product.name,
      price: props.product.price,
      imageUrl: props.product.imageUrl,
      providerName: props.product.providerName,
    })
    isLiked.value = true
  }
}

async function handleClick() {
  await addRecentlyViewed({
    productId: props.product.productId,
    name: props.product.name,
    price: props.product.price,
    imageUrl: props.product.imageUrl,
    providerName: props.product.providerName,
  })
}
</script>

<template>
  <article class="product-card" @click="handleClick">
    <!-- Image Container -->
    <div class="product-card__image-wrapper">
      <img
        :src="product.imageUrl"
        :alt="product.name"
        class="product-card__image"
        loading="lazy"
      />
      
      <!-- Similarity Badge -->
      <span v-if="showSimilarity" class="product-card__similarity">
        {{ similarityPercent }}% match
      </span>
      
      <!-- Favorite Button -->
      <button
        class="product-card__favorite"
        :class="{ 'product-card__favorite--active': isLiked }"
        @click.stop="toggleFavorite"
        :aria-label="isLiked ? 'Remove from favorites' : 'Add to favorites'"
      >
        <Heart :stroke-width="2" :class="{ 'filled': isLiked }" />
      </button>
    </div>

    <!-- Content -->
    <div class="product-card__content">
      <span class="product-card__provider">{{ product.providerName }}</span>
      <h3 class="product-card__name">{{ product.name }}</h3>
      <p class="product-card__price">{{ formattedPrice }}</p>
    </div>
  </article>
</template>

<style lang="scss" scoped>
.product-card {
  background-color: var(--color-background);
  border-radius: var(--radius-md);
  overflow: hidden;
  cursor: pointer;
  transition: box-shadow var(--transition-normal);

  &:hover {
    box-shadow: var(--shadow-lg);

    .product-card__image {
      transform: scale(1.03);
    }

    .product-card__favorite {
      opacity: 1;
    }
  }

  &__image-wrapper {
    position: relative;
    aspect-ratio: 1;
    overflow: hidden;
    background-color: var(--color-surface);
  }

  &__image {
    width: 100%;
    height: 100%;
    object-fit: cover;
    transition: transform var(--transition-slow);
  }

  &__similarity {
    position: absolute;
    top: var(--space-3);
    left: var(--space-3);
    padding: var(--space-1) var(--space-2);
    font-size: var(--text-xs);
    font-weight: 600;
    color: var(--color-success);
    background-color: rgba(255, 255, 255, 0.95);
    border-radius: var(--radius-sm);
  }

  &__favorite {
    position: absolute;
    top: var(--space-3);
    right: var(--space-3);
    display: flex;
    align-items: center;
    justify-content: center;
    width: 36px;
    height: 36px;
    background-color: rgba(255, 255, 255, 0.95);
    border: none;
    border-radius: var(--radius-full);
    cursor: pointer;
    opacity: 0;
    transition: opacity var(--transition-fast), background-color var(--transition-fast);

    svg {
      width: 18px;
      height: 18px;
      color: var(--color-text-secondary);
      transition: color var(--transition-fast);

      &.filled {
        fill: var(--color-error);
        color: var(--color-error);
      }
    }

    &:hover {
      background-color: #fff;

      svg:not(.filled) {
        color: var(--color-error);
      }
    }

    &--active {
      opacity: 1;
    }
  }

  &__content {
    padding: var(--space-4);
  }

  &__provider {
    display: block;
    font-size: var(--text-xs);
    color: var(--color-text-muted);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    margin-bottom: var(--space-1);
  }

  &__name {
    font-size: var(--text-sm);
    font-weight: 500;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-2);
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  &__price {
    font-size: var(--text-base);
    font-weight: 700;
    color: var(--color-text-primary);
    margin: 0;
  }
}
</style>
