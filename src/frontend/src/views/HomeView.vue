<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useSettingsStore } from '@/stores/settings'
import ImageUpload from '@/components/search/ImageUpload.vue'
import { Camera, Sparkles, ShoppingBag, ArrowRight, Clock } from 'lucide-vue-next'

const settingsStore = useSettingsStore()

const features = [
  { 
    icon: Camera, 
    title: 'Upload Image', 
    description: 'Drop or select any furniture photo' 
  },
  { 
    icon: Sparkles, 
    title: 'AI Analysis', 
    description: 'Advanced ML finds similar pieces' 
  },
  { 
    icon: ShoppingBag, 
    title: 'Browse Results', 
    description: 'Discover matching items instantly' 
  },
]
</script>

<template>
  <div class="home">
    <!-- Hero Section -->
    <section class="home__hero">
      <div class="home__hero-container">
        <h1 class="home__title">{{ settingsStore.siteName }}</h1>
        <p class="home__subtitle">{{ settingsStore.welcomeMessage }}</p>

        <!-- Upload Area -->
        <div class="home__upload">
          <ImageUpload />
        </div>

        <!-- Features Grid -->
        <div class="home__features">
          <div 
            v-for="feature in features" 
            :key="feature.title"
            class="home__feature"
          >
            <div class="home__feature-icon">
              <component :is="feature.icon" :stroke-width="1.5" />
            </div>
            <h3 class="home__feature-title">{{ feature.title }}</h3>
            <p class="home__feature-desc">{{ feature.description }}</p>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA Section -->
    <section class="home__cta">
      <div class="home__cta-container">
        <h2 class="home__cta-title">Ready to explore?</h2>
        <p class="home__cta-text">Start searching now or browse your previous searches.</p>
        <div class="home__cta-buttons">
          <RouterLink to="/search" class="btn btn--primary">
            Start Searching
            <ArrowRight :stroke-width="2" />
          </RouterLink>
          <RouterLink to="/history" class="btn btn--secondary">
            <Clock :stroke-width="2" />
            View History
          </RouterLink>
        </div>
      </div>
    </section>
  </div>
</template>

<style lang="scss" scoped>
.home {
  &__hero {
    padding: var(--space-16) 0 var(--space-12);
    background-color: var(--color-background);
  }

  &__hero-container {
    max-width: 800px;
    margin: 0 auto;
    padding: 0 var(--space-6);
    text-align: center;
  }

  &__title {
    font-family: var(--font-display);
    font-size: var(--text-4xl);
    font-weight: 600;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-4);
    
    @media (min-width: 640px) {
      font-size: 3rem;
    }
  }

  &__subtitle {
    font-size: var(--text-lg);
    color: var(--color-text-secondary);
    margin: 0 0 var(--space-10);
    max-width: 500px;
    margin-left: auto;
    margin-right: auto;
  }

  &__upload {
    max-width: 560px;
    margin: 0 auto var(--space-16);
  }

  &__features {
    display: grid;
    grid-template-columns: repeat(1, 1fr);
    gap: var(--space-8);
    
    @media (min-width: 640px) {
      grid-template-columns: repeat(3, 1fr);
    }
  }

  &__feature {
    padding: var(--space-6);
    text-align: center;

    &-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 56px;
      height: 56px;
      background-color: var(--color-primary-light);
      border-radius: var(--radius-full);
      margin-bottom: var(--space-4);
      color: var(--color-primary);

      svg {
        width: 28px;
        height: 28px;
      }
    }

    &-title {
      font-size: var(--text-base);
      font-weight: 600;
      color: var(--color-text-primary);
      margin: 0 0 var(--space-2);
    }

    &-desc {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
      margin: 0;
    }
  }

  &__cta {
    padding: var(--space-16) 0;
    background-color: var(--color-primary-light);
  }

  &__cta-container {
    max-width: 600px;
    margin: 0 auto;
    padding: 0 var(--space-6);
    text-align: center;
  }

  &__cta-title {
    font-size: var(--text-2xl);
    font-weight: 700;
    color: var(--color-text-primary);
    margin: 0 0 var(--space-4);
  }

  &__cta-text {
    font-size: var(--text-base);
    color: var(--color-text-secondary);
    margin: 0 0 var(--space-8);
  }

  &__cta-buttons {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: var(--space-4);
  }
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-6);
  font-family: var(--font-body);
  font-size: var(--text-sm);
  font-weight: 600;
  border-radius: var(--radius-md);
  text-decoration: none;
  cursor: pointer;
  transition: all var(--transition-fast);
  border: none;

  svg {
    width: 18px;
    height: 18px;
  }

  &--primary {
    background-color: var(--color-primary);
    color: #fff;

    &:hover {
      background-color: var(--color-primary-hover);
    }
  }

  &--secondary {
    background-color: transparent;
    color: var(--color-text-primary);
    border: 1px solid var(--color-border);

    &:hover {
      border-color: var(--color-primary);
      color: var(--color-primary);
    }
  }
}
</style>
