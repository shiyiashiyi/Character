/**
 * router/index.js — 登录 / 主页 / Persona 工坊
 */
import { createRouter, createWebHistory } from 'vue-router'
import { useSession } from '../composables/useSession'

const routes = [
  { path: '/', redirect: '/home' },
  { path: '/login', name: 'login', component: () => import('../views/LoginView.vue'), meta: { guest: true } },
  {
    path: '/home',
    name: 'home',
    component: () => import('../views/HomeView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/forge',
    name: 'forge',
    component: () => import('../views/PersonaForgeView.vue'),
    meta: { requiresAuth: true },
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  const { isLoggedIn } = useSession()
  if (to.meta.requiresAuth && !isLoggedIn.value) return { name: 'login' }
  if (to.meta.guest && isLoggedIn.value) return { name: 'home' }
  return true
})

export default router
