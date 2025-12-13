<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  modelValue: boolean
  title: string
  message: string
  confirmText?: string
  cancelText?: string
  isLoading?: boolean
  variant?: 'primary' | 'secondary' | 'danger'
  size?: 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), {
  confirmText: 'Confirm',
  cancelText: 'Cancel',
  isLoading: false,
  variant: 'primary',
  size: 'sm',
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
  cancel: []
}>()

const modalSizeClass = computed(() => {
  switch (props.size) {
    case 'sm': return 'modal--sm'
    case 'md': return ''
    case 'lg': return 'modal--lg'
    default: return 'modal--sm'
  }
})

const confirmButtonClass = computed(() => {
  switch (props.variant) {
    case 'primary': return 'btn btn--primary'
    case 'secondary': return 'btn btn--secondary'
    case 'danger': return 'btn btn--danger'
    default: return 'btn btn--primary'
  }
})

function handleConfirm() {
  emit('confirm')
}

function handleCancel() {
  emit('cancel')
  emit('update:modelValue', false)
}

function handleOverlayClick() {
  if (!props.isLoading) {
    handleCancel()
  }
}
</script>

<template>
  <Teleport to="body">
    <div v-if="modelValue" class="modal-overlay" @click.self="handleOverlayClick">
      <div :class="['modal', modalSizeClass]">
        <div class="modal__header">
          <h2 class="modal__title">{{ title }}</h2>
        </div>
        <div class="modal__body">
          <slot name="body">
            <p v-html="message"></p>
          </slot>
        </div>
        <div class="modal__footer">
          <slot name="footer">
            <button class="btn btn--secondary" @click="handleCancel" :disabled="isLoading">
              {{ cancelText }}
            </button>
            <button
              :class="confirmButtonClass"
              :disabled="isLoading"
              @click="handleConfirm"
            >
              <span v-if="isLoading" class="spinner spinner--sm"></span>
              {{ isLoading ? `${confirmText}ing...` : confirmText }}
            </button>
          </slot>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style lang="scss" scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: var(--space-4);
}

.modal {
  background: var(--color-background);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-lg);
  width: 100%;
  max-width: 480px;
  max-height: 90vh;
  overflow: auto;

  &--sm {
    max-width: 360px;
  }

  &--lg {
    max-width: 600px;
  }

  &__header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: var(--space-4) var(--space-6);
    border-bottom: 1px solid var(--color-border);
  }

  &__title {
    font-size: var(--text-lg);
    font-weight: 600;
    margin: 0;
  }

  &__body {
    padding: var(--space-6);
  }

  &__footer {
    display: flex;
    justify-content: flex-end;
    gap: var(--space-3);
    padding: var(--space-4) var(--space-6);
    border-top: 1px solid var(--color-border);
    background: var(--color-surface);
  }
}

.btn--danger {
  background: var(--color-error, #dc2626);
  color: white;

  &:hover:not(:disabled) {
    background: #b91c1c;
  }
}

.spinner--sm {
  width: 14px;
  height: 14px;
  margin-right: var(--space-2);
}
</style>