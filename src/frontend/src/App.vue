<script setup lang="ts">
import { computed } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import AppLayout from '@/components/layout/AppLayout.vue'
import AdminLayout from '@/components/layout/AdminLayout.vue'

const route = useRoute()

const isAdminRoute = computed(() => {
  return route.path.startsWith('/admin') && route.name !== 'admin-login'
})
</script>

<template>
  <AdminLayout v-if="isAdminRoute">
    <RouterView />
  </AdminLayout>
  <AppLayout v-else>
    <RouterView />
  </AppLayout>
</template>
