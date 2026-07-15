<script setup>
defineProps({
  visible: { type: Boolean, default: false },
  message: { type: String, default: '' },
})
</script>

<template>
  <Transition name="overlay">
    <div v-if="visible" class="success-overlay" role="dialog" aria-modal="true" aria-labelledby="successTitle">
      <!-- Magical background burst -->
      <div class="success-overlay__bg" aria-hidden="true">
        <div class="burst burst-1"></div>
        <div class="burst burst-2"></div>
      </div>
      
      <div class="success-overlay__card">
        <div class="success-overlay__icon" aria-hidden="true">
          <svg viewBox="0 0 80 80" width="80" height="80">
            <defs>
              <linearGradient id="successGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                <stop offset="0%" stop-color="#34d399" />
                <stop offset="100%" stop-color="#10b981" />
              </linearGradient>
            </defs>
            <circle class="ring" cx="40" cy="40" r="36" fill="none" stroke="url(#successGrad)" stroke-width="4" stroke-linecap="round" />
            <path class="check" fill="none" stroke="url(#successGrad)" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" d="M24 42l10 10 22-24" />
          </svg>
        </div>
        <h2 id="successTitle" class="success-overlay__title">验证通过</h2>
        <p class="success-overlay__sub">{{ message || '即将进入系统' }}</p>
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
  background: rgba(255, 255, 255, 0.4);
  backdrop-filter: blur(24px) saturate(200%);
  -webkit-backdrop-filter: blur(24px) saturate(200%);
  perspective: 1000px;
}

.success-overlay__bg {
  position: absolute;
  inset: 0;
  overflow: hidden;
  pointer-events: none;
}

.burst {
  position: absolute;
  top: 50%; left: 50%;
  border-radius: 50%;
  transform: translate(-50%, -50%);
  filter: blur(60px);
  opacity: 0;
}

.burst-1 {
  width: 400px; height: 400px;
  background: rgba(16, 185, 129, 0.4);
}
.burst-2 {
  width: 300px; height: 300px;
  background: rgba(59, 130, 246, 0.3);
}

.overlay-enter-active .burst-1 {
  animation: burst-pop 1s var(--ease-out) forwards;
}
.overlay-enter-active .burst-2 {
  animation: burst-pop 1s 0.2s var(--ease-out) forwards;
}

@keyframes burst-pop {
  0% { transform: translate(-50%, -50%) scale(0.5); opacity: 0; }
  50% { opacity: 1; }
  100% { transform: translate(-50%, -50%) scale(1.5); opacity: 0; }
}

.success-overlay__card {
  position: relative;
  text-align: center;
  padding: 48px 56px;
  background: rgba(255, 255, 255, 0.85);
  backdrop-filter: blur(40px);
  -webkit-backdrop-filter: blur(40px);
  border-radius: var(--radius-xl);
  box-shadow: 0 30px 80px rgba(16, 185, 129, 0.15), inset 0 1px 2px #fff;
  border: 1px solid rgba(255, 255, 255, 0.8);
  max-width: 400px;
  width: 100%;
  z-index: 1;
}

.success-overlay__icon {
  margin: 0 auto 24px;
  display: flex;
  justify-content: center;
}

.ring {
  stroke-dasharray: 227;
  stroke-dashoffset: 227;
  transform-origin: center;
  transform: rotate(-90deg);
}

.check {
  stroke-dasharray: 60;
  stroke-dashoffset: 60;
}

.overlay-enter-active .ring {
  animation: draw-ring 0.8s var(--ease-bouncy) forwards;
}

.overlay-enter-active .check {
  animation: draw-check 0.5s 0.4s var(--ease-spring) forwards;
}

@keyframes draw-ring {
  0% { stroke-dashoffset: 227; transform: rotate(-90deg) scale(0.8); }
  100% { stroke-dashoffset: 0; transform: rotate(0) scale(1); }
}

@keyframes draw-check {
  0% { stroke-dashoffset: 60; }
  100% { stroke-dashoffset: 0; }
}

.success-overlay__title {
  margin: 0 0 12px;
  font-size: 32px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--text);
  background: linear-gradient(135deg, #111, #444);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.success-overlay__sub {
  margin: 0;
  font-size: 16px;
  color: var(--text-secondary);
  font-weight: 500;
}

/* Transitions */
.overlay-enter-active,
.overlay-leave-active {
  transition: opacity 0.5s var(--ease);
}
.overlay-enter-from,
.overlay-leave-to {
  opacity: 0;
}

.overlay-enter-active .success-overlay__card {
  animation: card-pop 0.6s var(--ease-bouncy) forwards;
}

.overlay-leave-active .success-overlay__card {
  transition: transform 0.4s var(--ease), opacity 0.4s var(--ease);
  transform: scale(0.9) translateY(20px) rotateX(10deg);
  opacity: 0;
}

@keyframes card-pop {
  0% {
    opacity: 0;
    transform: scale(0.8) translateY(40px) rotateX(-20deg);
  }
  100% {
    opacity: 1;
    transform: scale(1) translateY(0) rotateX(0);
  }
}
</style>