<script setup>
import { ref, watch, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import StatusRail from './StatusRail.vue'
import SlideGate from './SlideGate.vue'
import SuccessOverlay from './SuccessOverlay.vue'
import { useAuthForm } from '../composables/useAuthForm'
import { useSession } from '../composables/useSession'

const router = useRouter()
const { setSession } = useSession()
const slideRef = ref(null)
const cardRef = ref(null)

const {
  mode,
  isLogin,
  isRegister,
  email,
  password,
  confirmPassword,
  displayName,
  verificationCode,
  sendCountdown,
  codeHint,
  codeError,
  canSendCode,
  sendCodeBusy,
  remember,
  showPassword,
  slideDone,
  busy,
  successVisible,
  successMessage,
  authUser,
  emailFocused,
  pwdFocused,
  emailValid,
  pwdValid,
  pwdLevel,
  pwdLevelLabel,
  showEmailChips,
  emailError,
  pwdError,
  confirmError,
  authError,
  fieldsReady,
  canSubmitLogin,
  canSubmitRegister,
  emailSuffixes,
  switchMode,
  onEmailInput,
  onEmailBlur,
  onEmailFocus,
  onPwdInput,
  onPwdBlur,
  onPwdFocus,
  onConfirmBlur,
  onCodeBlur,
  onCodeInput,
  sendCode,
  applySuffix,
  onSlideComplete,
  submit,
  setStatus,
} = useAuthForm()

// 3D Tilt Effect
const mouseX = ref(0)
const mouseY = ref(0)
let tiltRaf = null

function onMouseMove(e) {
  if (successVisible.value || busy.value || isRegister.value) return
  if (emailFocused.value || pwdFocused.value) return
  if (e.target?.closest?.('input, button, a, label, .field, .tabs, .slide-gate')) {
    mouseX.value = 0
    mouseY.value = 0
    return
  }
  if (!cardRef.value) return
  if (tiltRaf) cancelAnimationFrame(tiltRaf)
  tiltRaf = requestAnimationFrame(() => {
    const rect = cardRef.value.getBoundingClientRect()
    const x = e.clientX - rect.left
    const y = e.clientY - rect.top
    const centerX = rect.width / 2
    const centerY = rect.height / 2
    mouseX.value = ((x - centerX) / centerX) * 5 // max 5deg tilt
    mouseY.value = ((y - centerY) / centerY) * 5
  })
}

function onMouseLeave() {
  if (tiltRaf) cancelAnimationFrame(tiltRaf)
  mouseX.value = 0
  mouseY.value = 0
}

const cardStyle = computed(() => ({
  transform: `perspective(1200px) rotateX(${-mouseY.value}deg) rotateY(${mouseX.value}deg)`,
  transition: mouseX.value === 0 && mouseY.value === 0 ? 'transform 0.5s var(--ease-spring)' : 'none'
}))

watch(slideDone, (done, prev) => {
  if (prev && !done) slideRef.value?.reset()
})

watch(successVisible, (visible) => {
  if (!visible || !authUser.value) return
  setSession(authUser.value)
  window.setTimeout(() => {
    successVisible.value = false
    router.push('/home')
  }, 1200) // longer wait to show flashy success
})

async function handleSubmit(e) {
  e.preventDefault()
  await submit()
}
</script>

<template>
  <div class="page" @mousemove="onMouseMove" @mouseleave="onMouseLeave">
    <!-- Cool animated background -->
    <div class="page__ambient" aria-hidden="true">
      <div class="mesh-grid"></div>
      <div class="blob blob--1" />
      <div class="blob blob--2" />
      <div class="blob blob--3" />
      <div class="blob blob--4" />
    </div>

    <main class="stage">
      <section
        ref="cardRef"
        class="card"
        :class="{ 'card--loading': busy, 'card--hidden': successVisible }"
        :style="cardStyle"
      >
        <div class="card__glow"></div>
        <div class="card__content">
          <header class="card__head">
            <div class="logo-wrapper" aria-hidden="true">
              <div class="logo">
                <svg viewBox="0 0 32 32" width="32" height="32">
                  <defs>
                    <linearGradient id="logoGrad" x1="0" y1="0" x2="32" y2="32">
                      <stop offset="0%" stop-color="#3b82f6" />
                      <stop offset="100%" stop-color="#8b5cf6" />
                    </linearGradient>
                  </defs>
                  <rect width="32" height="32" rx="10" fill="url(#logoGrad)" />
                  <path fill="#fff" d="M10 22V10h3.2l4.1 7.2L21.4 10H24v12h-2.6v-7.1l-3.9 7.1h-2.2l-3.9-7.1V22z"/>
                </svg>
              </div>
            </div>
            <h1 class="card__title">{{ isLogin ? '欢迎回来' : '开启新篇章' }}</h1>
            <p class="card__sub">
              {{ isLogin ? '登录你的 FrontStudy 账户' : '创建一个全新的账号' }}
            </p>
          </header>

          <nav class="tabs" role="tablist" aria-label="登录或注册">
            <div class="tabs__indicator" :style="{ transform: isLogin ? 'translateX(0)' : 'translateX(100%)' }"></div>
            <button
              type="button"
              class="tabs__btn"
              :class="{ 'tabs__btn--active': isLogin }"
              role="tab"
              :aria-selected="isLogin"
              @click="switchMode('login')"
            >
              登录
            </button>
            <button
              type="button"
              class="tabs__btn"
              :class="{ 'tabs__btn--active': isRegister }"
              role="tab"
              :aria-selected="isRegister"
              @click="switchMode('register')"
            >
              注册
            </button>
          </nav>

          <StatusRail />

          <form class="form" novalidate autocomplete="on" @submit="handleSubmit">
            <!-- 邮箱 -->
            <div
              class="field"
              :class="{
                'field--focus': emailFocused || email,
                'field--valid': emailValid && email,
                'field--invalid': emailError,
              }"
            >
              <div class="field__row">
                <input
                  id="email"
                  v-model="email"
                  class="field__input"
                  type="email"
                  inputmode="email"
                  autocomplete="username email"
                  placeholder=" "
                  required
                  :aria-invalid="!!emailError"
                  :aria-describedby="emailError ? 'emailError' : 'emailHint'"
                  @input="onEmailInput"
                  @focus="onEmailFocus"
                  @blur="onEmailBlur"
                />
                <label class="field__label" for="email">邮箱地址</label>
                <div class="field__border"></div>
                <Transition name="fade">
                  <span v-if="emailValid && email" class="field__check" aria-hidden="true">
                    <svg viewBox="0 0 24 24" width="18" height="18">
                      <path fill="currentColor" d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/>
                    </svg>
                  </span>
                </Transition>
              </div>
              <p v-if="emailError" id="emailError" class="field__error" role="alert">{{ emailError }}</p>
              <p v-else id="emailHint" class="field__hint">输入常用邮箱即可登录</p>
              
              <Transition name="chips">
                <div v-if="showEmailChips" class="chips">
                  <button
                    v-for="suffix in emailSuffixes"
                    :key="suffix"
                    type="button"
                    class="chip"
                    @click="applySuffix(suffix)"
                  >
                    {{ suffix }}
                  </button>
                </div>
              </Transition>
            </div>

            <!-- 注册：验证码 -->
            <div
              v-if="isRegister"
              class="field"
              :class="{ 'field--focus': verificationCode, 'field--invalid': codeError }"
            >
              <div class="field__row field__row--code">
                <div class="field__input-wrap">
                  <input
                    id="verificationCode"
                    v-model="verificationCode"
                    class="field__input field__input--code"
                    type="text"
                    inputmode="numeric"
                    autocomplete="one-time-code"
                    maxlength="6"
                    pattern="[0-9]{6}"
                    placeholder=" "
                    required
                    :aria-invalid="!!codeError"
                    :aria-describedby="codeError ? 'codeError' : 'codeHint'"
                    @input="onCodeInput"
                    @blur="onCodeBlur"
                  />
                  <label class="field__label" for="verificationCode">邮箱验证码</label>
                  <div class="field__border"></div>
                </div>
                <button
                  type="button"
                  class="send-code"
                  :disabled="!canSendCode"
                  :class="{ 'send-code--busy': sendCodeBusy }"
                  @click="sendCode"
                >
                  <span v-if="sendCodeBusy">发送中…</span>
                  <span v-else-if="sendCountdown > 0">{{ sendCountdown }}s</span>
                  <span v-else>获取验证码</span>
                  <div class="send-code__hover"></div>
                </button>
              </div>
              <p v-if="codeError" id="codeError" class="field__error" role="alert">{{ codeError }}</p>
              <p v-else-if="codeHint" id="codeHint" class="field__hint field__hint--ok">{{ codeHint }}</p>
            </div>

            <!-- 注册：昵称 -->
            <div v-if="isRegister" class="field" :class="{ 'field--focus': displayName }">
              <div class="field__row">
                <input
                  id="displayName"
                  v-model="displayName"
                  class="field__input"
                  type="text"
                  autocomplete="name"
                  maxlength="100"
                  placeholder=" "
                />
                <label class="field__label" for="displayName">个性昵称（选填）</label>
                <div class="field__border"></div>
              </div>
            </div>

            <!-- 密码 -->
            <div
              class="field"
              :class="{
                'field--focus': pwdFocused || password,
                'field--valid': pwdValid && password,
                'field--invalid': pwdError || authError,
              }"
            >
              <div class="field__row">
                <input
                  id="password"
                  v-model="password"
                  class="field__input"
                  :type="showPassword ? 'text' : 'password'"
                  :autocomplete="isLogin ? 'current-password' : 'new-password'"
                  placeholder=" "
                  required
                  minlength="8"
                  :aria-invalid="!!pwdError"
                  :aria-describedby="pwdError ? 'pwdError' : 'pwdHint'"
                  @input="onPwdInput"
                  @focus="onPwdFocus"
                  @blur="onPwdBlur"
                />
                <label class="field__label" for="password">账户密码</label>
                <div class="field__border"></div>
                <button
                  type="button"
                  class="field__toggle"
                  :aria-label="showPassword ? '隐藏密码' : '显示密码'"
                  :aria-pressed="showPassword"
                  @click="showPassword = !showPassword"
                >
                  <svg v-if="!showPassword" viewBox="0 0 24 24" width="20" height="20">
                    <path fill="currentColor" d="M12 4.5C7 4.5 2.7 7.6 1 12c1.7 4.4 6 7.5 11 7.5s9.3-3.1 11-7.5c-1.7-4.4-6-7.5-11-7.5M12 17a5 5 0 1 1 0-10 5 5 0 0 1 0 10m0-2.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5"/>
                  </svg>
                  <svg v-else viewBox="0 0 24 24" width="20" height="20">
                    <path fill="currentColor" d="M12 6.5a5.5 5.5 0 0 0-5.5 5.5 5.5 5.5 0 0 0 5.5 5.5 5.5 5.5 0 0 0 5.5-5.5 5.5 5.5 0 0 0-5.5-5.5M3.5 12 1 12c2.5-5 7-8 11-8s8.5 3 11 8l-2.5 0c-2-3.5-5.5-5.5-8.5-5.5S5.5 8.5 3.5 12m17 0 2.5 0c-2.5 5-7 8-11 8s-8.5-3-11-8l2.5 0c2 3.5 5.5 5.5 8.5 5.5s6.5-2 8.5-5.5"/>
                  </svg>
                </button>
              </div>
              <Transition name="fade">
                <div
                  v-show="pwdFocused || password"
                  class="meter"
                  :data-level="pwdLevel"
                >
                  <div class="meter__bars">
                    <span v-for="i in 4" :key="i" />
                  </div>
                  <span class="meter__label">{{ pwdLevelLabel }}</span>
                </div>
              </Transition>
              <p v-if="pwdError" id="pwdError" class="field__error" role="alert">{{ pwdError }}</p>
            </div>

            <!-- 注册：确认密码 -->
            <div
              v-if="isRegister"
              class="field"
              :class="{
                'field--focus': confirmPassword,
                'field--invalid': confirmError,
              }"
            >
              <div class="field__row">
                <input
                  id="confirmPassword"
                  v-model="confirmPassword"
                  class="field__input"
                  type="password"
                  autocomplete="new-password"
                  placeholder=" "
                  required
                  :aria-invalid="!!confirmError"
                  @blur="onConfirmBlur"
                />
                <label class="field__label" for="confirmPassword">再次确认密码</label>
                <div class="field__border"></div>
              </div>
              <p v-if="confirmError" class="field__error" role="alert">{{ confirmError }}</p>
            </div>

            <p v-if="authError && isRegister" class="field__error field__error--block" role="alert">
              {{ authError }}
            </p>

            <div v-if="isLogin" class="row">
              <label class="check">
                <input v-model="remember" type="checkbox" />
                <span class="check__box" aria-hidden="true" />
                <span>记住登录状态</span>
              </label>
              <a href="#" class="link" @click.prevent="setStatus('重置链接已准备', 'ok', 50)">
                忘记密码？
              </a>
            </div>

            <SlideGate
              v-if="isLogin"
              ref="slideRef"
              :locked="!fieldsReady"
              :done="slideDone"
              @complete="onSlideComplete"
            />

            <button
              type="submit"
              class="btn btn--primary"
              :disabled="isLogin ? !canSubmitLogin : !canSubmitRegister"
              :class="{ 'btn--loading': busy }"
            >
              <div class="btn__bg"></div>
              <span class="btn__text">{{ isLogin ? '进入系统' : '立即注册' }}</span>
              <span v-if="busy" class="btn__spinner" aria-hidden="true" />
            </button>
          </form>

          <footer class="card__foot">
            <div class="divider"><span>Or continue with</span></div>
            <div class="oauth">
              <button
                type="button"
                class="oauth__btn"
                aria-label="通过 Apple 登录"
                @click="setStatus('Apple 登录准备中', 'warn', 40)"
              >
                <svg viewBox="0 0 24 24" width="22" height="22">
                  <path fill="currentColor" d="M17.05 20.28c-.98.95-2.05 1.88-3.51 1.9-1.46.02-1.92-.86-3.58-.86-1.66 0-2.17.84-3.55.88-1.38.04-2.43-1.27-3.41-2.22-1.85-1.78-3.27-5.03-1.35-7.23 1.36-1.58 3.39-2.52 5.3-2.52 1.32-.02 2.57.88 3.58.88 1.01 0 2.9-1.08 4.88-.92.83.03 3.17.34 4.67 2.52-.12.07-2.79 1.63-2.76 4.85.03 3.84 3.36 5.12 3.4 5.14-.03.06-.53 1.82-1.65 3.58M12.03 7.25c-.15-2.23 1.66-4.07 3.74-4.25.29 2.58-2.34 4.5-3.74 4.25"/>
                </svg>
              </button>
              <button
                type="button"
                class="oauth__btn"
                aria-label="通过 Google 登录"
                @click="setStatus('Google 登录准备中', 'warn', 40)"
              >
                <svg viewBox="0 0 24 24" width="22" height="22">
                  <path fill="currentColor" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23m-7.27-4.53c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.95H2.18C1.43 9.45 1 11.18 1 13s.43 3.55 1.18 5.05zM12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.95l3.85 2.98c.87-2.6 3.3-4.53 6.16-4.53"/>
                </svg>
              </button>
            </div>
          </footer>
        </div>
      </section>
    </main>

    <SuccessOverlay :visible="successVisible" :message="successMessage" />
  </div>
</template>

<style scoped>
.page {
  min-height: 100dvh;
  position: relative;
  overflow-x: hidden;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  perspective: 1200px;
}

/* Ambient Animated Mesh & Blobs */
.page__ambient {
  position: absolute;
  inset: -100px;
  z-index: 0;
  pointer-events: none;
  background: #fdfdfd;
}

.mesh-grid {
  position: absolute;
  inset: 0;
  background-image: radial-gradient(rgba(0, 0, 0, 0.05) 1px, transparent 1px);
  background-size: 32px 32px;
  mask-image: radial-gradient(circle at center, black 40%, transparent 80%);
}

.blob {
  position: absolute;
  border-radius: 50%;
  filter: blur(100px);
  opacity: 0.6;
  mix-blend-mode: multiply;
  animation: float 20s ease-in-out infinite alternate;
}

.blob--1 {
  width: 600px; height: 600px;
  top: -10%; left: -10%;
  background: rgba(147, 197, 253, 0.5); /* Light blue */
}
.blob--2 {
  width: 500px; height: 500px;
  bottom: -20%; right: -10%;
  background: rgba(196, 181, 253, 0.5); /* Violet */
  animation-delay: -5s;
}
.blob--3 {
  width: 400px; height: 400px;
  top: 40%; right: 20%;
  background: rgba(244, 114, 182, 0.35); /* Pink/Rose */
  animation-delay: -10s;
}
.blob--4 {
  width: 450px; height: 450px;
  bottom: 20%; left: 20%;
  background: rgba(167, 243, 208, 0.4); /* Mint */
  animation-delay: -15s;
}

@keyframes float {
  0% { transform: translate(0, 0) scale(1) rotate(0deg); }
  33% { transform: translate(30px, -50px) scale(1.1) rotate(10deg); }
  66% { transform: translate(-20px, 20px) scale(0.9) rotate(-5deg); }
  100% { transform: translate(0, 0) scale(1) rotate(0deg); }
}

.stage {
  position: relative;
  z-index: 1;
  width: 100%;
  max-width: 440px;
  margin: auto;
  padding: 24px 20px 40px;
  flex-shrink: 0;
  transform-style: preserve-3d;
}

/* Glassmorphic Card with Tilt */
.card {
  position: relative;
  border-radius: var(--radius-xl);
  background: var(--surface);
  backdrop-filter: blur(40px) saturate(200%);
  -webkit-backdrop-filter: blur(40px) saturate(200%);
  box-shadow: var(--shadow-lg);
  border: 1px solid rgba(255, 255, 255, 0.8);
  will-change: transform;
}

.card::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  box-shadow: inset 0 1px 1px rgba(255, 255, 255, 0.9);
  pointer-events: none;
}

.card--loading {
  pointer-events: none;
}

.card--hidden {
  opacity: 0;
  transform: scale(0.95) translateY(20px) rotateX(10deg) !important;
}

.card__content {
  padding: 48px 40px 40px;
  position: relative;
  z-index: 2;
}

/* Optional glowing halo around the card */
.card__glow {
  position: absolute;
  inset: -1px;
  border-radius: var(--radius-xl);
  background: var(--accent-gradient);
  opacity: 0;
  transition: opacity 0.5s var(--ease);
  z-index: 0;
  filter: blur(20px);
}
.card:hover .card__glow {
  opacity: 0.15;
}

.card__head {
  text-align: center;
  margin-bottom: 32px;
}

.logo-wrapper {
  display: flex;
  justify-content: center;
  margin-bottom: 20px;
}

.logo {
  box-shadow: var(--shadow-md);
  border-radius: 12px;
  display: flex;
  background: #fff;
  padding: 2px;
}

.card__title {
  margin: 0 0 8px;
  font-size: 26px;
  font-weight: 700;
  letter-spacing: -0.04em;
  color: var(--text);
}

.card__sub {
  margin: 0;
  font-size: 15px;
  color: var(--text-secondary);
}

/* Tabs */
.tabs {
  position: relative;
  display: flex;
  padding: 4px;
  margin-bottom: 28px;
  background: rgba(0, 0, 0, 0.03);
  border-radius: var(--radius-sm);
  box-shadow: inset 0 1px 3px rgba(0,0,0,0.02);
}

.tabs__indicator {
  position: absolute;
  top: 4px; left: 4px; bottom: 4px;
  width: calc(50% - 4px);
  background: #fff;
  border-radius: 10px;
  box-shadow: var(--shadow-sm), inset 0 1px 1px rgba(255,255,255,1);
  transition: transform 0.4s var(--ease-spring);
  z-index: 1;
}

.tabs__btn {
  flex: 1;
  position: relative;
  z-index: 2;
  height: 40px;
  border: none;
  background: transparent;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-secondary);
  cursor: pointer;
  border-radius: 10px;
  transition: color 0.3s;
}

.tabs__btn--active {
  color: var(--text);
}

/* Form Fields */
.form {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.field {
  position: relative;
  display: flex;
  flex-direction: column;
}

/* Floating label design */
.field__row {
  position: relative;
  display: flex;
  align-items: center;
  background: rgba(255,255,255,0.5);
  border-radius: var(--radius-sm);
  box-shadow: inset 0 1px 2px rgba(0,0,0,0.02);
}

.field__input {
  width: 100%;
  height: 56px;
  padding: 24px 16px 8px;
  font-size: 16px;
  color: var(--text);
  background: transparent;
  border: none;
  border-radius: var(--radius-sm);
  outline: none;
  z-index: 2;
}

.field__label {
  position: absolute;
  left: 16px;
  top: 18px;
  font-size: 16px;
  color: var(--text-tertiary);
  pointer-events: none;
  transition: transform 0.3s var(--ease-out), font-size 0.3s var(--ease-out), color 0.3s var(--ease-out);
  transform-origin: left top;
  z-index: 1;
}

/* Floating Label Action */
.field--focus .field__label,
.field:focus-within .field__label,
.field__input:focus + .field__label,
.field__input:not(:placeholder-shown) + .field__label {
  transform: translateY(-10px);
  font-size: 12px;
  color: var(--accent);
  font-weight: 500;
}

/* Animated Border */
.field__border {
  position: absolute;
  inset: 0;
  border-radius: var(--radius-sm);
  border: 1px solid var(--separator-opaque);
  pointer-events: none;
  transition: border-color 0.3s, box-shadow 0.3s;
}

.field--focus .field__border,
.field:focus-within .field__border {
  border-color: var(--accent);
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.1);
}

.field--valid .field__border {
  border-color: var(--success);
}
.field--invalid .field__border {
  border-color: var(--error);
  animation: shake 0.4s var(--ease);
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-4px); }
  50% { transform: translateX(4px); }
  75% { transform: translateX(-2px); }
}

.field__check {
  position: absolute;
  right: 16px;
  color: var(--success);
  display: flex;
}

.field__toggle {
  position: absolute;
  right: 12px;
  padding: 8px;
  border: none;
  background: transparent;
  color: var(--text-tertiary);
  cursor: pointer;
  border-radius: 8px;
  z-index: 3;
  transition: color 0.2s, transform 0.1s;
}

.field__toggle:hover {
  color: var(--text);
}
.field__toggle:active {
  transform: scale(0.9);
}

.field__hint, .field__error {
  margin: 6px 0 0 4px;
  font-size: 12px;
}
.field__hint { color: var(--text-tertiary); }
.field__hint--ok { color: var(--success); }
.field__error { color: var(--error); font-weight: 500;}

/* Chips */
.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}
.chip {
  padding: 6px 14px;
  font-size: 12px;
  font-weight: 600;
  border-radius: 99px;
  border: 1px solid var(--separator-opaque);
  background: rgba(255,255,255,0.6);
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s var(--ease-out);
}
.chip:hover {
  background: #fff;
  color: var(--accent);
  border-color: var(--accent);
  box-shadow: 0 4px 12px rgba(37, 99, 235, 0.1);
  transform: translateY(-1px);
}

/* Verification Code */
.field__row--code {
  background: transparent;
  box-shadow: none;
  gap: 12px;
}
.field__input-wrap {
  position: relative;
  flex: 1;
  background: rgba(255,255,255,0.5);
  border-radius: var(--radius-sm);
}

.send-code {
  position: relative;
  height: 56px;
  padding: 0 20px;
  border: none;
  border-radius: var(--radius-sm);
  background: var(--surface-solid);
  border: 1px solid var(--separator-opaque);
  color: var(--text);
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  overflow: hidden;
  transition: all 0.2s;
  box-shadow: var(--shadow-sm);
}
.send-code__hover {
  position: absolute;
  inset: 0;
  background: var(--fill-tertiary);
  opacity: 0;
  transition: opacity 0.2s;
  pointer-events: none;
}
.send-code:hover:not(:disabled) .send-code__hover {
  opacity: 1;
}
.send-code:hover:not(:disabled) {
  border-color: var(--text-tertiary);
  transform: translateY(-1px);
  box-shadow: var(--shadow-md);
}
.send-code:active:not(:disabled) {
  transform: scale(0.97);
}

/* Password Meter */
.meter {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 8px 4px 0;
}
.meter__bars {
  display: flex;
  gap: 4px;
  flex: 1;
}
.meter__bars span {
  flex: 1;
  height: 4px;
  border-radius: 2px;
  background: var(--separator-opaque);
  transition: background 0.4s var(--ease), transform 0.4s var(--ease-spring);
}
.meter[data-level='1'] .meter__bars span:nth-child(1) { background: var(--error); transform: scaleY(1.5); }
.meter[data-level='2'] .meter__bars span:nth-child(-n+2) { background: var(--warn); }
.meter[data-level='3'] .meter__bars span:nth-child(-n+3) { background: var(--accent); }
.meter[data-level='4'] .meter__bars span { background: var(--success); }
.meter__label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-tertiary);
  min-width: 3em;
}

/* Checkbox */
.row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 14px;
  padding: 0 4px;
}
.check {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  color: var(--text-secondary);
  font-weight: 500;
}
.check input { opacity: 0; width: 0; height: 0; position: absolute; }
.check__box {
  width: 20px; height: 20px;
  border-radius: 6px;
  border: 2px solid var(--separator-opaque);
  background: var(--surface-solid);
  transition: all 0.2s;
  display: grid;
  place-items: center;
}
.check input:checked + .check__box {
  background: var(--accent);
  border-color: var(--accent);
}
.check input:checked + .check__box::after {
  content: '';
  width: 4px; height: 8px;
  border: solid #fff;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg) translate(-1px, -1px);
}

.link {
  color: var(--accent);
  text-decoration: none;
  font-weight: 600;
  transition: color 0.2s;
}
.link:hover { color: var(--accent-hover); }

/* Primary Button */
.btn--primary {
  position: relative;
  height: 56px;
  border: none;
  border-radius: var(--radius-sm);
  color: #fff;
  font-size: 18px;
  font-weight: 600;
  cursor: pointer;
  overflow: hidden;
  box-shadow: 0 8px 20px rgba(37, 99, 235, 0.25);
  transition: transform 0.2s var(--ease-spring), box-shadow 0.2s;
}
.btn__bg {
  position: absolute;
  inset: 0;
  background: var(--accent-gradient);
  background-size: 200% 200%;
  animation: bg-pan 4s ease infinite;
  transition: opacity 0.3s;
}
@keyframes bg-pan {
  0% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
  100% { background-position: 0% 50%; }
}

.btn__text {
  position: relative;
  z-index: 1;
}

.btn--primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 12px 28px rgba(37, 99, 235, 0.35);
}
.btn--primary:active:not(:disabled) {
  transform: translateY(1px) scale(0.98);
}
.btn--primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  filter: grayscale(0.5);
  box-shadow: none;
}

.btn--loading .btn__text { opacity: 0; }
.btn__spinner {
  position: absolute;
  inset: 0;
  margin: auto;
  width: 24px; height: 24px;
  border: 3px solid rgba(255,255,255,0.3);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
  z-index: 1;
}

/* Footer & OAuth */
.card__foot { margin-top: 36px; }
.divider {
  display: flex;
  align-items: center;
  gap: 16px;
  color: var(--text-tertiary);
  font-size: 13px;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 1px;
}
.divider::before, .divider::after {
  content: ''; flex: 1; height: 1px;
  background: var(--separator-opaque);
}

.oauth {
  margin-top: 20px;
  display: flex;
  justify-content: center;
  gap: 16px;
}
.oauth__btn {
  width: 56px; height: 56px;
  border-radius: 16px;
  border: 1px solid var(--separator-opaque);
  background: rgba(255,255,255,0.8);
  color: var(--text);
  cursor: pointer;
  display: grid;
  place-items: center;
  transition: all 0.3s var(--ease-spring);
  box-shadow: var(--shadow-sm);
}
.oauth__btn:hover {
  background: #fff;
  transform: translateY(-3px) scale(1.05);
  box-shadow: var(--shadow-md);
  border-color: var(--text-tertiary);
}
.oauth__btn:active {
  transform: scale(0.95);
}

.fade-enter-active, .fade-leave-active { transition: opacity 0.2s, transform 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; transform: scale(0.95); }
.chips-enter-active, .chips-leave-active { transition: opacity 0.3s var(--ease-out), transform 0.3s var(--ease-out); }
.chips-enter-from, .chips-leave-to { opacity: 0; transform: translateY(-8px); }
</style>
