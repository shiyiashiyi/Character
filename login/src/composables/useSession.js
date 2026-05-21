/**
 * useSession.js — 登录会话（localStorage）
 */
import { ref, computed } from 'vue'

const STORAGE_KEY = 'character_session'

const user = ref(load())

function load() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
}

export function useSession() {
  const isLoggedIn = computed(() => !!user.value?.email)

  function setSession(u) {
    user.value = u
    localStorage.setItem(STORAGE_KEY, JSON.stringify(u))
  }

  function clearSession() {
    user.value = null
    localStorage.removeItem(STORAGE_KEY)
  }

  return { user, isLoggedIn, setSession, clearSession }
}
