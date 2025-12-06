<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useSettingsStore } from '@/stores/settings'

interface SettingDefinition {
  key: string
  label: string
  description: string
  type: 'text' | 'number' | 'boolean'
  category: string
  min?: number
  max?: number
}

const settingsStore = useSettingsStore()

const settingDefinitions: SettingDefinition[] = [
  {
    key: 'ui.siteName',
    label: 'Site Name',
    description: 'The name displayed in the header and browser title',
    type: 'text',
    category: 'UI',
  },
  {
    key: 'ui.showProviderFilter',
    label: 'Show Provider Filter',
    description: 'Display provider filter in search results',
    type: 'boolean',
    category: 'UI',
  },
  {
    key: 'search.maxImageSize',
    label: 'Max Image Size',
    description: 'Maximum dimension (width/height) for uploaded images',
    type: 'number',
    category: 'Search',
    min: 100,
    max: 2000,
  },
  {
    key: 'search.jpegQuality',
    label: 'JPEG Quality',
    description: 'Quality level for image compression (1-100)',
    type: 'number',
    category: 'Search',
    min: 1,
    max: 100,
  },
  {
    key: 'search.maxResults',
    label: 'Max Results',
    description: 'Maximum number of search results to return',
    type: 'number',
    category: 'Search',
    min: 5,
    max: 100,
  },
]

const localValues = ref<Record<string, string>>({})
const isSaving = ref<Record<string, boolean>>({})
const saveSuccess = ref<Record<string, boolean>>({})

const categories = computed(() => {
  const cats = new Set(settingDefinitions.map(s => s.category))
  return Array.from(cats)
})

function getSettingsByCategory(category: string): SettingDefinition[] {
  return settingDefinitions.filter(s => s.category === category)
}

onMounted(() => {
  settingDefinitions.forEach(def => {
    localValues.value[def.key] = settingsStore.getSetting(def.key) || ''
  })
})

async function saveSetting(key: string) {
  isSaving.value[key] = true
  saveSuccess.value[key] = false

  try {
    await settingsStore.updateSetting(key, localValues.value[key] ?? '')
    saveSuccess.value[key] = true
    setTimeout(() => {
      saveSuccess.value[key] = false
    }, 2000)
  } catch (e) {
    console.error(`Failed to save ${key}:`, e)
  } finally {
    isSaving.value[key] = false
  }
}

function handleBooleanChange(key: string) {
  localValues.value[key] = localValues.value[key] === 'true' ? 'false' : 'true'
  saveSetting(key)
}
</script>

<template>
  <div class="admin-settings">
    <header class="admin-settings__header">
      <h1 class="page-title">Settings</h1>
      <p class="admin-settings__subtitle">
        Configure application behavior and appearance
      </p>
    </header>

    <div class="admin-settings__content">
      <section
        v-for="category in categories"
        :key="category"
        class="settings-section"
      >
        <h2 class="settings-section__title">{{ category }}</h2>

        <div class="settings-section__items">
          <div
            v-for="setting in getSettingsByCategory(category)"
            :key="setting.key"
            class="setting-item card"
          >
            <div class="setting-item__info">
              <label :for="setting.key" class="setting-item__label">
                {{ setting.label }}
              </label>
              <p class="setting-item__description">{{ setting.description }}</p>
            </div>

            <div class="setting-item__control">
              <template v-if="setting.type === 'boolean'">
                <button
                  :id="setting.key"
                  class="toggle"
                  :class="{ 'toggle--active': localValues[setting.key] === 'true' }"
                  :aria-pressed="localValues[setting.key] === 'true'"
                  @click="handleBooleanChange(setting.key)"
                >
                  <span class="toggle__thumb"></span>
                </button>
              </template>

              <template v-else-if="setting.type === 'number'">
                <input
                  :id="setting.key"
                  v-model="localValues[setting.key]"
                  type="number"
                  class="form-input form-input--sm"
                  :min="setting.min"
                  :max="setting.max"
                  @blur="saveSetting(setting.key)"
                  @keyup.enter="saveSetting(setting.key)"
                />
              </template>

              <template v-else>
                <input
                  :id="setting.key"
                  v-model="localValues[setting.key]"
                  type="text"
                  class="form-input"
                  @blur="saveSetting(setting.key)"
                  @keyup.enter="saveSetting(setting.key)"
                />
              </template>

              <span
                v-if="isSaving[setting.key]"
                class="setting-item__status setting-item__status--saving"
              >
                Saving...
              </span>
              <span
                v-else-if="saveSuccess[setting.key]"
                class="setting-item__status setting-item__status--success"
              >
                ✓ Saved
              </span>
            </div>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.admin-settings {
  &__header {
    margin-bottom: var(--space-8);
  }

  &__subtitle {
    color: var(--color-text-muted);
    margin-bottom: 0;
  }

  &__content {
    display: flex;
    flex-direction: column;
    gap: var(--space-10);
  }
}

.settings-section {
  &__title {
    font-family: var(--font-heading);
    font-size: var(--text-xl);
    margin-bottom: var(--space-4);
    padding-bottom: var(--space-2);
    border-bottom: 1px solid var(--color-border);
  }

  &__items {
    display: flex;
    flex-direction: column;
    gap: var(--space-4);
  }
}

.setting-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--space-6);
  padding: var(--space-5);

  @media (max-width: 640px) {
    flex-direction: column;
    align-items: stretch;
  }

  &__info {
    flex: 1;
  }

  &__label {
    display: block;
    font-weight: 600;
    margin-bottom: var(--space-1);
  }

  &__description {
    font-size: var(--text-sm);
    color: var(--color-text-muted);
    margin-bottom: 0;
  }

  &__control {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    flex-shrink: 0;

    @media (max-width: 640px) {
      justify-content: flex-end;
    }
  }

  &__status {
    font-size: var(--text-sm);
    white-space: nowrap;

    &--saving {
      color: var(--color-text-muted);
    }

    &--success {
      color: var(--color-success);
    }
  }
}

.form-input {
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  font-size: var(--text-base);
  min-width: 200px;
  transition: border-color var(--transition-fast), box-shadow var(--transition-fast);

  &:focus {
    outline: none;
    border-color: var(--color-accent);
    box-shadow: 0 0 0 3px var(--color-accent-light);
  }

  &--sm {
    min-width: 100px;
    width: 100px;
  }
}

.toggle {
  position: relative;
  width: 48px;
  height: 26px;
  background-color: var(--color-border);
  border: none;
  border-radius: var(--radius-full);
  cursor: pointer;
  transition: background-color var(--transition-fast);

  &--active {
    background-color: var(--color-success);
  }

  &__thumb {
    position: absolute;
    top: 3px;
    left: 3px;
    width: 20px;
    height: 20px;
    background-color: white;
    border-radius: var(--radius-full);
    transition: transform var(--transition-fast);
    box-shadow: var(--shadow-sm);
  }

  &--active &__thumb {
    transform: translateX(22px);
  }
}
</style>
