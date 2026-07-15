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

const THUMB = 44
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
  setThumb(maxLeft.value, true)
  emit('complete')
}

function snapBack() {
  if (props.done) return
  setThumb(0, true)
  setTimeout(() => {
    thumbTransition.value = false
  }, 300)
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
  if (x >= maxLeft.value * 0.95) complete()
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
    const x = setThumb(thumbX.value + step, true)
    if (x >= maxLeft.value * 0.95) complete()
    e.preventDefault()
  } else if (e.key === 'ArrowLeft') {
    setThumb(thumbX.value - step, true)
    e.preventDefault()
  }
}

function onMouseDown(e) { onStart(e.clientX) }
function onTouchStart(e) { onStart(e.touches[0].clientX) }
function onMouseMove(e) { onMove(e.clientX) }
function onTouchMove(e) { onMove(e.touches[0].clientX) }

watch(
  () => props.done,
  (d) => {
    if (!d) setThumb(0, true)
    else setThumb(maxLeft.value, true)
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
    setThumb(0, true)
    emit('reset')
  },
})
</script>

<template>
  <div
    class="slide-gate"
    :class="{ 'is-locked': locked, 'is-done': done, 'is-dragging': dragging }"
    aria-label="滑动以确认登录"
  >
    <div ref="trackRef" class="slide-gate__track">
      <div class="slide-gate__fill" :style="{ width: `calc(${progressPct}% + ${THUMB/2}px)` }">
        <div class="fill-sparkle"></div>
      </div>
      <span class="slide-gate__hint" :class="{ 'hint-hidden': progressPct > 20 }">
        {{ done ? '验证通过' : '向右滑动完成验证' }}
      </span>
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
          transition: thumbTransition ? 'transform 0.4s var(--ease-spring)' : 'none',
        }"
        @mousedown="onMouseDown"
        @touchstart.passive="onTouchStart"
        @keydown="onKeydown"
      >
        <div class="thumb-bg"></div>
        <svg v-if="!done" class="thumb-icon" viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
          <path fill="currentColor" d="M8.59 16.59 13.17 12 8.59 7.41 10 6l6 6-6 6z"/>
        </svg>
        <svg v-else class="thumb-icon thumb-icon--done" viewBox="0 0 24 24" width="20" height="20" aria-hidden="true">
          <path fill="currentColor" d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/>
        </svg>
      </div>
    </div>
  </div>
</template>

<style scoped>
.slide-gate {
  margin-top: 8px;
  position: relative;
}

.slide-gate.is-locked {
  opacity: 0.5;
  pointer-events: none;
  filter: grayscale(1);
}

.slide-gate__track {
  position: relative;
  height: 52px;
  border-radius: 26px;
  background: rgba(255, 255, 255, 0.4);
  border: 1px solid var(--separator-opaque);
  box-shadow: inset 0 2px 4px rgba(0,0,0,0.03);
  overflow: hidden;
  touch-action: none;
  user-select: none;
}

/* Shimmering Fill */
.slide-gate__fill {
  position: absolute;
  inset: 0 auto 0 0;
  background: var(--success);
  opacity: 0.15;
  pointer-events: none;
  transition: width 0.1s linear;
}

.slide-gate.is-done .slide-gate__fill {
  opacity: 0.25;
}

.slide-gate__hint {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-tertiary);
  pointer-events: none;
  letter-spacing: 2px;
  transition: opacity 0.3s, transform 0.3s;
  background: linear-gradient(90deg, transparent, rgba(255,255,255,0.8), transparent);
  background-size: 200% 100%;
  animation: shimmer 3s infinite;
  -webkit-background-clip: text;
}

@keyframes shimmer {
  0% { background-position: -200% 0; }
  100% { background-position: 200% 0; }
}

.hint-hidden {
  opacity: 0;
  transform: translateX(10px);
}

.slide-gate.is-done .slide-gate__hint {
  color: var(--success);
  opacity: 1;
  transform: none;
  animation: none;
  -webkit-text-fill-color: var(--success);
}

/* Glassy Thumb */
.slide-gate__thumb {
  position: absolute;
  left: 4px;
  top: 4px;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  color: var(--accent);
  display: grid;
  place-items: center;
  cursor: grab;
  z-index: 2;
  will-change: transform;
}

.thumb-bg {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: #fff;
  box-shadow: 0 4px 12px rgba(0,0,0,0.1), inset 0 -2px 4px rgba(0,0,0,0.05);
  transition: transform 0.3s var(--ease-spring), box-shadow 0.3s;
}

.slide-gate__thumb:hover .thumb-bg {
  transform: scale(1.05);
  box-shadow: 0 6px 16px rgba(0,0,0,0.15);
}

.slide-gate.is-dragging .slide-gate__thumb {
  cursor: grabbing;
}
.slide-gate.is-dragging .thumb-bg {
  transform: scale(0.95);
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.slide-gate__thumb:focus-visible .thumb-bg {
  outline: 3px solid rgba(37, 99, 235, 0.4);
  outline-offset: 2px;
}

.thumb-icon {
  position: relative;
  z-index: 1;
  transition: color 0.3s, transform 0.5s var(--ease-spring);
}

.slide-gate.is-done .thumb-bg {
  background: var(--success);
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
}

.slide-gate.is-done .thumb-icon {
  color: #fff;
  animation: pop-in 0.5s var(--ease-spring);
}

@keyframes pop-in {
  0% { transform: scale(0.5); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}
</style>
