/**
 * useStatus.js — 全局状态轨：即时反馈与登录进度
 */
import { reactive, computed } from 'vue'

const state = reactive({
  text: '准备就绪 — 演示账号 demo@front.study / demo12345',
  tone: '',
  progress: 0,
})

export function useStatus() {
  function setStatus(text, tone = 'neutral', progress = null) {
    state.text = text
    state.tone = tone === 'neutral' ? '' : tone
    if (progress !== null) {
      state.progress = Math.min(100, Math.max(0, progress))
    }
  }

  return {
    status: computed(() => state),
    setStatus,
  }
}
