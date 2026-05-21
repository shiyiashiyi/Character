<script setup>
import { useStatus } from '../composables/useStatus'

const { status } = useStatus()
</script>

<template>
  <div
    class="status-rail"
    :data-tone="status.tone"
    role="status"
    aria-live="polite"
    aria-atomic="true"
  >
    <div class="status-rail__track">
      <div
        class="status-rail__fill"
        :style="{ width: `${status.progress}%` }"
      />
    </div>
    <span class="status-rail__text">{{ status.text }}</span>
  </div>
</template>

<style scoped>
.status-rail {
  margin-bottom: 24px;
  padding: 12px 14px;
  border-radius: var(--radius-sm);
  background: var(--fill-tertiary);
  transition: background 0.3s var(--ease), box-shadow 0.3s var(--ease);
}

.status-rail[data-tone='ok'] {
  background: rgba(52, 199, 89, 0.08);
}

.status-rail[data-tone='warn'] {
  background: rgba(255, 149, 0, 0.08);
}

.status-rail[data-tone='error'] {
  background: rgba(255, 59, 48, 0.08);
}

.status-rail[data-tone='busy'] {
  background: rgba(0, 113, 227, 0.06);
}

.status-rail__track {
  height: 3px;
  border-radius: 99px;
  background: var(--separator);
  overflow: hidden;
  margin-bottom: 8px;
}

.status-rail__fill {
  height: 100%;
  border-radius: inherit;
  background: var(--accent);
  transition: width 0.4s var(--ease);
}

.status-rail__text {
  font-size: 12px;
  line-height: 1.4;
  color: var(--text-secondary);
  letter-spacing: -0.01em;
}

.status-rail[data-tone='busy'] .status-rail__text {
  color: var(--accent);
}
</style>
