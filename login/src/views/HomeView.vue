<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useSession } from '../composables/useSession'

const router = useRouter()
const { user, clearSession } = useSession()
const activeIndex = ref(-1)
const entered = ref(false)

const menuItems = [
  {
    id: 'forge',
    title: 'Persona 工坊',
    desc: '上传小说文本，生成可复用的角色扮演 Skill',
    icon: 'forge',
    route: '/forge',
    enabled: true,
  },
  {
    id: 'chat',
    title: '角色对话',
    desc: '加载 Skill 后与角色实时对话（开发中）',
    icon: 'chat',
    enabled: false,
  },
  {
    id: 'library',
    title: 'Skill 库',
    desc: '管理已生成的人物 Skill（开发中）',
    icon: 'library',
    enabled: false,
  },
  {
    id: 'settings',
    title: '账户设置',
    desc: '偏好与安全（开发中）',
    icon: 'settings',
    enabled: false,
  },
]

onMounted(() => {
  requestAnimationFrame(() => {
    entered.value = true
  })
})

function openItem(item, index) {
  if (!item.enabled) return
  activeIndex.value = index
  setTimeout(() => router.push(item.route), 180)
}

function logout() {
  clearSession()
  router.push('/login')
}
</script>

<template>
  <div class="home" :class="{ 'home--in': entered }">
    <div class="home__bg" aria-hidden="true">
      <div class="orb orb--1" />
      <div class="orb orb--2" />
      <div class="grid-glow" />
    </div>

    <header class="home__top">
      <div>
        <p class="home__eyebrow">Character Studio</p>
        <h1 class="home__title">你好，{{ user?.displayName || user?.email?.split('@')[0] }}</h1>
      </div>
      <button type="button" class="btn-ghost" @click="logout">退出</button>
    </header>

    <p class="home__hint">悬停探索 · 点击进入（首个功能已开放）</p>

    <div class="menu">
      <button
        v-for="(item, i) in menuItems"
        :key="item.id"
        type="button"
        class="menu-card"
        :class="{
          'menu-card--on': activeIndex === i,
          'menu-card--off': !item.enabled,
          'menu-card--delay': true,
        }"
        :style="{ '--i': i }"
        :disabled="!item.enabled"
        @click="openItem(item, i)"
        @mouseenter="item.enabled && (activeIndex = i)"
        @mouseleave="activeIndex = -1"
      >
        <div class="menu-card__shine" aria-hidden="true" />
        <div class="menu-card__icon" :data-icon="item.icon" aria-hidden="true" />
        <h2 class="menu-card__title">{{ item.title }}</h2>
        <p class="menu-card__desc">{{ item.desc }}</p>
        <span v-if="item.enabled" class="menu-card__cta">进入 →</span>
        <span v-else class="menu-card__badge">即将推出</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.home {
  min-height: 100dvh;
  padding: 28px 24px 40px;
  position: relative;
  overflow: hidden;
  opacity: 0;
  transform: translateY(12px);
  transition: opacity 0.6s var(--ease), transform 0.6s var(--ease);
}

.home--in {
  opacity: 1;
  transform: none;
}

.home__bg {
  position: fixed;
  inset: 0;
  z-index: 0;
  pointer-events: none;
}

.orb {
  position: absolute;
  border-radius: 50%;
  filter: blur(90px);
  opacity: 0.5;
  animation: drift 16s ease-in-out infinite;
}

.orb--1 {
  width: 420px;
  height: 420px;
  top: -120px;
  right: -80px;
  background: radial-gradient(circle, rgba(0, 113, 227, 0.2), transparent 70%);
}

.orb--2 {
  width: 360px;
  height: 360px;
  bottom: -100px;
  left: -60px;
  background: radial-gradient(circle, rgba(99, 102, 241, 0.15), transparent 70%);
  animation-delay: -5s;
}

.grid-glow {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(0, 0, 0, 0.04) 1px, transparent 1px),
    linear-gradient(90deg, rgba(0, 0, 0, 0.04) 1px, transparent 1px);
  background-size: 48px 48px;
  mask-image: radial-gradient(ellipse at center, black 20%, transparent 75%);
}

@keyframes drift {
  50% { transform: translate(2%, 3%) scale(1.05); }
}

.home__top,
.home__hint,
.menu {
  position: relative;
  z-index: 1;
}

.home__top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.home__eyebrow {
  margin: 0 0 4px;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--accent);
}

.home__title {
  margin: 0;
  font-size: clamp(26px, 5vw, 34px);
  font-weight: 600;
  letter-spacing: -0.03em;
}

.home__hint {
  margin: 0 0 24px;
  font-size: 14px;
  color: var(--text-secondary);
}

.btn-ghost {
  border: 1px solid var(--separator);
  background: var(--surface);
  padding: 8px 14px;
  border-radius: 99px;
  font: inherit;
  font-size: 14px;
  cursor: pointer;
  transition: background 0.2s, transform 0.2s var(--ease-spring);
}

.btn-ghost:hover {
  background: var(--fill-tertiary);
  transform: scale(1.02);
}

.menu {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 16px;
}

.menu-card {
  position: relative;
  text-align: left;
  padding: 22px 20px 18px;
  border: 1px solid var(--separator);
  border-radius: var(--radius-xl);
  background: rgba(255, 255, 255, 0.72);
  backdrop-filter: blur(16px);
  cursor: pointer;
  overflow: hidden;
  transition:
    transform 0.35s var(--ease-spring),
    box-shadow 0.35s var(--ease),
    border-color 0.25s;
  opacity: 0;
  transform: translateY(16px);
}

.home--in .menu-card--delay {
  animation: card-in 0.55s var(--ease) forwards;
  animation-delay: calc(0.08s * var(--i));
}

@keyframes card-in {
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.menu-card--off {
  cursor: not-allowed;
  opacity: 0.55;
}

.menu-card:not(:disabled):hover,
.menu-card--on {
  transform: translateY(-6px) scale(1.02);
  border-color: rgba(0, 113, 227, 0.35);
  box-shadow: 0 20px 50px rgba(0, 113, 227, 0.12);
}

.menu-card__shine {
  position: absolute;
  inset: -50% auto auto -50%;
  width: 80%;
  height: 80%;
  background: conic-gradient(from 0deg, transparent, rgba(0, 113, 227, 0.15), transparent);
  opacity: 0;
  transition: opacity 0.35s;
  pointer-events: none;
}

.menu-card:not(:disabled):hover .menu-card__shine {
  opacity: 1;
  animation: spin 4s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.menu-card__icon {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  margin-bottom: 14px;
  background: linear-gradient(135deg, var(--accent), #6366f1);
  box-shadow: 0 8px 24px rgba(0, 113, 227, 0.25);
}

.menu-card__title {
  margin: 0 0 6px;
  font-size: 18px;
  font-weight: 600;
}

.menu-card__desc {
  margin: 0 0 14px;
  font-size: 13px;
  line-height: 1.5;
  color: var(--text-secondary);
}

.menu-card__cta {
  font-size: 13px;
  font-weight: 600;
  color: var(--accent);
}

.menu-card__badge {
  font-size: 11px;
  padding: 4px 10px;
  border-radius: 99px;
  background: var(--fill-tertiary);
  color: var(--text-tertiary);
}
</style>
