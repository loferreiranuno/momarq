import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'home',
    component: () => import('@/views/HomeView.vue'),
    meta: { title: 'Home' },
  },
  {
    path: '/search',
    name: 'search',
    component: () => import('@/views/SearchView.vue'),
    meta: { title: 'Search' },
  },
  {
    path: '/history',
    name: 'history',
    component: () => import('@/views/HistoryView.vue'),
    meta: { title: 'History' },
  },
  {
    path: '/favorites',
    name: 'favorites',
    component: () => import('@/views/FavoritesView.vue'),
    meta: { title: 'Favorites' },
  },
  {
    path: '/admin/login',
    name: 'admin-login',
    component: () => import('@/views/admin/AdminLoginView.vue'),
    meta: { title: 'Admin Login', guest: true },
  },
  {
    path: '/admin',
    name: 'admin',
    redirect: '/admin/dashboard',
    meta: { title: 'Admin', requiresAuth: true },
  },
  {
    path: '/admin/dashboard',
    name: 'admin-dashboard',
    component: () => import('@/views/admin/AdminDashboardView.vue'),
    meta: { title: 'Dashboard', requiresAuth: true },
  },
  {
    path: '/admin/settings',
    name: 'admin-settings',
    component: () => import('@/views/admin/AdminSettingsView.vue'),
    meta: { title: 'Settings', requiresAuth: true },
  },
  {
    path: '/admin/providers',
    name: 'admin-providers',
    component: () => import('@/views/admin/AdminProvidersView.vue'),
    meta: { title: 'Providers', requiresAuth: true },
  },
  {
    path: '/admin/products',
    name: 'admin-products',
    component: () => import('@/views/admin/AdminProductsView.vue'),
    meta: { title: 'Products', requiresAuth: true },
  },
  {
    path: '/admin/products/new',
    name: 'admin-product-new',
    component: () => import('@/views/admin/AdminProductFormView.vue'),
    meta: { title: 'New Product', requiresAuth: true },
  },
  {
    path: '/admin/products/:id/edit',
    name: 'admin-product-edit',
    component: () => import('@/views/admin/AdminProductFormView.vue'),
    meta: { title: 'Edit Product', requiresAuth: true },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: { title: 'Not Found' },
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior(_to, _from, savedPosition) {
    if (savedPosition) {
      return savedPosition
    }
    return { top: 0 }
  },
})

router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore()

  // Update document title
  const siteName = 'Visual Search'
  document.title = to.meta.title ? `${to.meta.title} | ${siteName}` : siteName

  // Check authentication for protected routes
  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    next({ name: 'admin-login', query: { redirect: to.fullPath } })
    return
  }

  // Redirect authenticated users away from guest-only routes
  if (to.meta.guest && authStore.isAuthenticated) {
    next({ name: 'admin-dashboard' })
    return
  }

  // Force password change
  if (
    authStore.isAuthenticated &&
    authStore.mustChangePassword &&
    to.name !== 'admin-login' &&
    to.meta.requiresAuth
  ) {
    // Password change is handled within the login view
    next()
    return
  }

  next()
})

export default router
