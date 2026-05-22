<script setup>
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'

const props = defineProps({
  locked: { type: Boolean, default: true },
  done: { type: Boolean, default: false },
})

const emit = defineEmits(['complete', 'reset'])

const trackRef = ref(null)
const thumbX = ref(0)
const dragging = ref(false)
const thumbTransition = ref(false)

const THUMB = 40
const PAD = 4

const maxLeft = computed(() => {
  const w = trackRef.value?.clientWidth ?? 0
  return Math.max(0, w - THUMB - PAD * 2)
})

const progressPct = computed(() =>
  maxLeft.value > 0 ? (thumbX.value / maxLeft.value) * 100 : 0
)

function setThumb(px, animate = false) {
  thumbTransition.value = animate
  const x = Math.max(0, Math.min(px, maxLeft.value))
  thumbX.value = x
  return x
}

function complete() {
  setThumb(maxLeft.value)
  emit('complete')
}

function snapBack() {
  if (props.done) return
  setThumb(0, true)
  setTimeout(() => {
    thumbTransition.value = false
  }, 220)
}

let startX = 0
let startLeft = 0

function onStart(clientX) {
  if (props.locked || props.done) return
  dragging.value = true
  startX = clientX
  startLeft = thumbX.value
}

function onMove(clientX) {
  if (!dragging.value) return
  const x = setThumb(startLeft + (clientX - startX))
  if (x >= maxLeft.value * 0.92) complete()
}

function onEnd() {
  if (!dragging.value) return
  dragging.value = false
  if (!props.done) snapBack()
}

function onKeydown(e) {
  if (props.locked || props.done) return
  const step = maxLeft.value / 5
  if (e.key === 'ArrowRight') {
    const x = setThumb(thumbX.value + step)
    if (x >= maxLeft.value * 0.92) complete()
    e.preventDefault()
  } else if (e.key === 'ArrowLeft') {
    setThumb(thumbX.value - step)
    e.preventDefault()
  }
}

function onMouseDown(e) {
  onStart(e.clientX)
}

function onTouchStart(e) {
  onStart(e.touches[0].clientX)
}

function onMouseMove(e) {
  onMove(e.clientX)
}

function onTouchMove(e) {
  onMove(e.touches[0].clientX)
}

watch(
  () => props.done,
  (d) => {
    if (!d) thumbX.value = 0
    else setThumb(maxLeft.value)
  }
)

onMounted(() => {
  window.addEventListener('mousemove', onMouseMove)
  window.addEventListener('mouseup', onEnd)
  window.addEventListener('touchmove', onTouchMove, { passive: true })
  window.addEventListener('touchend', onEnd)
})

onUnmounted(() => {
  window.removeEventListener('mousemove', onMouseMove)
  window.removeEventListener('mouseup', onEnd)
  window.removeEventListener('touchmove', onTouchMove)
  window.removeEventListener('touchend', onEnd)
})

defineExpose({
  reset() {
    thumbX.value = 0
    emit('reset')
  },
})
</script>

<template>
  <div
    class="slide-gate"
    :class="{ 'is-locked': locked, 'is-done': done }"
    aria-label="滑动以确认登录"
  >
    <div ref="trackRef" class="slide-gate__track">
      <div class="slide-gate__fill" :style="{ width: `${progressPct}%` }" />
      <span class="slide-gate__hint">{{ done ? '已确认' : '滑动以登录' }}</span>
      <div
        class="slide-gate__thumb"
        role="slider"
        tabindex="0"
        :aria-valuenow="Math.round(progressPct)"
        aria-valuemin="0"
        aria-valuemax="100"
        aria-label="登录确认滑块"
        :style="{
          transform: `translateX(${thumbX}px)`,
          transition: thumbTransition ? 'transform 0.22s var(--ease-out)' : 'none',
        }"
        @mousedown="onMouseDown"
        @touchstart.passive="onTouchStart"
        @keydown="onKeydown"
      >
        <svg v-if="!done" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path fill="currentColor" d="M8.59 16.59 13.17 12 8.59 7.41 10 6l6 6-6 6z"/>
        </svg>
        <svg v-else viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
          <path fill="currentColor" d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/>
        </svg>
      </div>
    </div>
  </div>
</template>

<style scoped>
.slide-gate {
  margin-top: 4px;
}

.slide-gate.is-locked {
  opacity: 0.45;
  pointer-events: none;
}

.slide-gate__track {
  position: relative;
  height: 48px;
  border-radius: 99px;
  background: var(--fill-tertiary);
  border: 1px solid var(--separator);
  overflow: hidden;
  touch-action: none;
  user-select: none;
}

.slide-gate__fill {
  position: absolute;
  inset: 0 auto 0 0;
  background: rgba(0, 113, 227, 0.1);
  pointer-events: none;
  transition: width 0.05s linear;
}

.slide-gate__hint {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  pointer-events: none;
  letter-spacing: -0.02em;
  transition: opacity 0.3s var(--ease);
}

.slide-gate.is-done .slide-gate__hint {
  color: var(--success);
  opacity: 0.9;
}

.slide-gate__thumb {
  position: absolute;
  left: 4px;
  top: 4px;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--surface);
  color: var(--accent);
  display: grid;
  place-items: center;
  cursor: grab;
  box-shadow: var(--shadow-sm);
  border: 1px solid var(--separator);
  z-index: 2;
  will-change: transform;
}

.slide-gate__thumb:active {
  cursor: grabbing;
  box-shadow: var(--shadow-md);
}

.slide-gate__thumb:focus-visible {
  outline: 3px solid rgba(0, 113, 227, 0.35);
  outline-offset: 2px;
}

.slide-gate.is-done .slide-gate__thumb {
  background: var(--success);
  color: #fff;
  border-color: transparent;
}
</style>
