<script setup>
defineProps({
  visible: { type: Boolean, default: false },
  message: { type: String, default: '' },
})
</script>

<template>
  <Transition name="overlay">
    <div v-if="visible" class="success-overlay" role="dialog" aria-modal="true" aria-labelledby="successTitle">
      <div class="success-overlay__card">
        <div class="success-overlay__icon" aria-hidden="true">
          <svg viewBox="0 0 52 52" width="56" height="56">
            <circle class="ring" cx="26" cy="26" r="24" fill="none" stroke-width="2.5" />
            <path class="check" fill="none" stroke-width="2.5" d="M14 27l8 8 16-18" />
          </svg>
        </div>
        <h2 id="successTitle" class="success-overlay__title">登录成功</h2>
        <p class="success-overlay__sub">{{ message }}</p>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.success-overlay {
  position: fixed;
  inset: 0;
  z-index: 100;
  display: grid;
  place-items: center;
  padding: 24px;
  background: rgba(251, 251, 253, 0.72);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
}

.success-overlay__card {
  text-align: center;
  padding: 40px 48px;
  background: var(--surface);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--separator);
  max-width: 360px;
  width: 100%;
}

.success-overlay__icon {
  margin: 0 auto 20px;
}

.ring {
  stroke: var(--success);
  stroke-dasharray: 151;
  stroke-dashoffset: 151;
  animation: draw-ring 0.6s var(--ease) forwards;
}

.check {
  stroke: var(--success);
  stroke-dasharray: 48;
  stroke-dashoffset: 48;
  animation: draw-check 0.4s 0.35s var(--ease) forwards;
}

@keyframes draw-ring {
  to { stroke-dashoffset: 0; }
}

@keyframes draw-check {
  to { stroke-dashoffset: 0; }
}

.success-overlay__title {
  margin: 0 0 8px;
  font-size: 28px;
  font-weight: 600;
  letter-spacing: -0.03em;
  color: var(--text);
}

.success-overlay__sub {
  margin: 0;
  font-size: 15px;
  color: var(--text-secondary);
  letter-spacing: -0.01em;
}

.overlay-enter-active,
.overlay-leave-active {
  transition: opacity 0.45s var(--ease);
}

.overlay-enter-active .success-overlay__card,
.overlay-leave-active .success-overlay__card {
  transition: transform 0.5s var(--ease-spring), opacity 0.45s var(--ease);
}

.overlay-enter-from,
.overlay-leave-to {
  opacity: 0;
}

.overlay-enter-from .success-overlay__card,
.overlay-leave-to .success-overlay__card {
  opacity: 0;
  transform: scale(0.94) translateY(12px);
}
</style>
