<script setup>
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import StatusRail from './StatusRail.vue'
import SlideGate from './SlideGate.vue'
import SuccessOverlay from './SuccessOverlay.vue'
import { useAuthForm } from '../composables/useAuthForm'
import { useSession } from '../composables/useSession'

const router = useRouter()
const { setSession } = useSession()
const slideRef = ref(null)

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

watch(slideDone, (done, prev) => {
  if (prev && !done) slideRef.value?.reset()
})

watch(successVisible, (visible) => {
  if (!visible || !authUser.value) return
  setSession(authUser.value)
  window.setTimeout(() => {
    successVisible.value = false
    router.push('/home')
  }, 900)
})

async function handleSubmit(e) {
  e.preventDefault()
  await submit()
}
</script>

<template>
  <div class="page">
    <div class="page__ambient" aria-hidden="true">
      <div class="blob blob--1" />
      <div class="blob blob--2" />
      <div class="blob blob--3" />
    </div>

    <main class="stage">
      <section
        class="card"
        :class="{ 'card--loading': busy, 'card--hidden': successVisible }"
      >
        <header class="card__head">
          <div class="logo" aria-hidden="true">
            <svg viewBox="0 0 32 32" width="32" height="32">
              <rect width="32" height="32" rx="8" fill="currentColor" />
              <path fill="#fff" d="M10 22V10h3.2l4.1 7.2L21.4 10H24v12h-2.6v-7.1l-3.9 7.1h-2.2l-3.9-7.1V22z"/>
            </svg>
          </div>
          <h1 class="card__title">{{ isLogin ? '登录' : '注册' }} FrontStudy</h1>
          <p class="card__sub">
            {{ isLogin ? '使用邮箱登录你的账号' : '创建账号，开始使用 CharacterSkills' }}
          </p>
        </header>

        <nav class="tabs" role="tablist" aria-label="登录或注册">
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
              'field--focus': emailFocused,
              'field--valid': emailValid && email,
              'field--invalid': emailError,
            }"
          >
            <label class="field__label" for="email">邮箱地址</label>
            <div class="field__row">
              <input
                id="email"
                v-model="email"
                class="field__input"
                type="email"
                inputmode="email"
                autocomplete="username email"
                required
                :aria-invalid="!!emailError"
                :aria-describedby="emailError ? 'emailError' : 'emailHint'"
                @input="onEmailInput"
                @focus="onEmailFocus"
                @blur="onEmailBlur"
              />
              <Transition name="fade">
                <span v-if="emailValid && email" class="field__check" aria-hidden="true">
                  <svg viewBox="0 0 24 24" width="18" height="18">
                    <path fill="currentColor" d="M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/>
                  </svg>
                </span>
              </Transition>
            </div>
            <p v-if="emailError" id="emailError" class="field__error" role="alert">{{ emailError }}</p>
            <p v-else id="emailHint" class="field__hint">支持常见邮箱后缀快速补全</p>
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
            :class="{ 'field--invalid': codeError }"
          >
            <label class="field__label" for="verificationCode">邮箱验证码</label>
            <div class="field__row field__row--code">
              <input
                id="verificationCode"
                v-model="verificationCode"
                class="field__input field__input--code"
                type="text"
                inputmode="numeric"
                autocomplete="one-time-code"
                maxlength="6"
                pattern="[0-9]{6}"
                placeholder="6 位数字"
                :aria-invalid="!!codeError"
                :aria-describedby="codeError ? 'codeError' : 'codeHint'"
                @input="onCodeInput"
                @blur="onCodeBlur"
              />
              <button
                type="button"
                class="send-code"
                :disabled="!canSendCode"
                :class="{ 'send-code--busy': sendCodeBusy }"
                @click="sendCode"
              >
                <span v-if="sendCodeBusy">发送中…</span>
                <span v-else-if="sendCountdown > 0">{{ sendCountdown }}s</span>
                <span v-else>发送验证码</span>
              </button>
            </div>
            <p v-if="codeError" id="codeError" class="field__error" role="alert">{{ codeError }}</p>
            <p v-else-if="codeHint" id="codeHint" class="field__hint field__hint--ok">{{ codeHint }}</p>
            <p v-else id="codeHint" class="field__hint">
              点击发送后查收 characteryebby@163.com 邮件（可能在垃圾箱）
            </p>
          </div>

          <!-- 注册：昵称 -->
          <div v-if="isRegister" class="field field--focusable">
            <label class="field__label" for="displayName">昵称（可选）</label>
            <div class="field__row">
              <input
                id="displayName"
                v-model="displayName"
                class="field__input"
                type="text"
                autocomplete="name"
                maxlength="100"
                placeholder="不填则使用邮箱前缀"
              />
            </div>
          </div>

          <!-- 密码 -->
          <div
            class="field"
            :class="{
              'field--focus': pwdFocused,
              'field--valid': pwdValid && password,
              'field--invalid': pwdError || authError,
            }"
          >
            <label class="field__label" for="password">密码</label>
            <div class="field__row">
              <input
                id="password"
                v-model="password"
                class="field__input"
                :type="showPassword ? 'text' : 'password'"
                :autocomplete="isLogin ? 'current-password' : 'new-password'"
                required
                minlength="8"
                :aria-invalid="!!pwdError"
                :aria-describedby="pwdError ? 'pwdError' : 'pwdHint'"
                @input="onPwdInput"
                @focus="onPwdFocus"
                @blur="onPwdBlur"
              />
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
            <p v-if="pwdError" id="pwdError" class="field__error" role="alert">{{ pwdError }}</p>
            <p v-else id="pwdHint" class="field__hint">至少 8 位，建议包含大小写字母与数字</p>
          </div>

          <!-- 注册：确认密码 -->
          <div
            v-if="isRegister"
            class="field"
            :class="{
              'field--invalid': confirmError,
            }"
          >
            <label class="field__label" for="confirmPassword">确认密码</label>
            <div class="field__row">
              <input
                id="confirmPassword"
                v-model="confirmPassword"
                class="field__input"
                type="password"
                autocomplete="new-password"
                required
                :aria-invalid="!!confirmError"
                @blur="onConfirmBlur"
              />
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
              <span>记住我</span>
            </label>
            <a href="#" class="link" @click.prevent="setStatus('重置链接将发送到您的邮箱（演示）', '', 50)">
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
            class="btn"
            :disabled="isLogin ? !canSubmitLogin : !canSubmitRegister"
            :class="{ 'btn--loading': busy }"
          >
            <span class="btn__text">{{ isLogin ? '登录' : '注册' }}</span>
            <span v-if="busy" class="btn__spinner" aria-hidden="true" />
          </button>
        </form>

        <footer class="card__foot">
          <div class="divider"><span>或</span></div>
          <div class="oauth">
            <button
              type="button"
              class="oauth__btn"
              aria-label="通过 Apple 登录"
              @click="setStatus('Apple 登录即将开放', 'warn', 40)"
            >
              <svg viewBox="0 0 24 24" width="20" height="20">
                <path fill="currentColor" d="M17.05 20.28c-.98.95-2.05 1.88-3.51 1.9-1.46.02-1.92-.86-3.58-.86-1.66 0-2.17.84-3.55.88-1.38.04-2.43-1.27-3.41-2.22-1.85-1.78-3.27-5.03-1.35-7.23 1.36-1.58 3.39-2.52 5.3-2.52 1.32-.02 2.57.88 3.58.88 1.01 0 2.9-1.08 4.88-.92.83.03 3.17.34 4.67 2.52-.12.07-2.79 1.63-2.76 4.85.03 3.84 3.36 5.12 3.4 5.14-.03.06-.53 1.82-1.65 3.58M12.03 7.25c-.15-2.23 1.66-4.07 3.74-4.25.29 2.58-2.34 4.5-3.74 4.25"/>
              </svg>
            </button>
            <button
              type="button"
              class="oauth__btn"
              aria-label="通过 Google 登录"
              @click="setStatus('Google 登录即将开放', 'warn', 40)"
            >
              <svg viewBox="0 0 24 24" width="20" height="20">
                <path fill="currentColor" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23m-7.27-4.53c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.95H2.18C1.43 9.45 1 11.18 1 13s.43 3.55 1.18 5.05zM12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.95l3.85 2.98c.87-2.6 3.3-4.53 6.16-4.53"/>
              </svg>
            </button>
          </div>
        </footer>
      </section>
    </main>

    <SuccessOverlay :visible="successVisible" :message="successMessage" />
  </div>
</template>

<style scoped>
.page {
  min-height: 100dvh;
  position: relative;
  overflow: hidden;
}

.page__ambient {
  position: fixed;
  inset: 0;
  z-index: 0;
  background: var(--bg);
  pointer-events: none;
}

.blob {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  opacity: 0.55;
  animation: float 18s ease-in-out infinite;
}

.blob--1 {
  width: 55vw;
  height: 55vw;
  max-width: 520px;
  max-height: 520px;
  top: -15%;
  right: -10%;
  background: radial-gradient(circle, rgba(0, 113, 227, 0.12) 0%, transparent 70%);
}

.blob--2 {
  width: 45vw;
  height: 45vw;
  max-width: 420px;
  max-height: 420px;
  bottom: -10%;
  left: -8%;
  background: radial-gradient(circle, rgba(175, 180, 190, 0.2) 0%, transparent 70%);
  animation-delay: -6s;
}

.blob--3 {
  width: 30vw;
  height: 30vw;
  max-width: 280px;
  max-height: 280px;
  top: 40%;
  left: 50%;
  transform: translateX(-50%);
  background: radial-gradient(circle, rgba(0, 113, 227, 0.06) 0%, transparent 70%);
  animation-delay: -12s;
}

@keyframes float {
  0%, 100% { transform: translate(0, 0) scale(1); }
  50% { transform: translate(2%, 3%) scale(1.04); }
}

.stage {
  position: relative;
  z-index: 1;
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: 32px 20px;
}

.card {
  width: min(400px, 100%);
  padding: 36px 32px 28px;
  background: var(--surface);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-lg);
  border: 1px solid var(--separator-opaque);
  transition: opacity 0.5s var(--ease), transform 0.5s var(--ease);
}

.card--loading {
  pointer-events: none;
}

.card--hidden {
  opacity: 0;
  transform: scale(0.98) translateY(8px);
}

.card__head {
  text-align: center;
  margin-bottom: 28px;
}

.logo {
  display: inline-flex;
  margin-bottom: 16px;
  color: var(--text);
}

.card__title {
  margin: 0 0 6px;
  font-size: 28px;
  font-weight: 600;
  letter-spacing: -0.03em;
  color: var(--text);
}

.card__sub {
  margin: 0;
  font-size: 15px;
  color: var(--text-secondary);
  letter-spacing: -0.01em;
}

.tabs {
  display: flex;
  gap: 4px;
  padding: 4px;
  margin-bottom: 20px;
  background: var(--fill-tertiary);
  border-radius: var(--radius-sm);
}

.tabs__btn {
  flex: 1;
  height: 36px;
  border: none;
  border-radius: 8px;
  background: transparent;
  font: inherit;
  font-size: 14px;
  font-weight: 500;
  color: var(--text-secondary);
  cursor: pointer;
  transition: background 0.25s var(--ease), color 0.25s, box-shadow 0.25s;
}

.tabs__btn--active {
  background: var(--surface);
  color: var(--text);
  box-shadow: var(--shadow-sm);
}

.tabs__btn:focus-visible {
  outline: 3px solid rgba(0, 113, 227, 0.35);
  outline-offset: 1px;
}

.field__error--block {
  margin: -8px 0 0;
  padding-left: 2px;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.field__label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  letter-spacing: -0.01em;
  padding-left: 2px;
}

.field__row {
  position: relative;
  display: flex;
  align-items: center;
}

.field__row--code {
  gap: 8px;
}

.field__input--code {
  flex: 1;
  min-width: 0;
  padding-right: 14px;
  letter-spacing: 0.2em;
  font-variant-numeric: tabular-nums;
}

.send-code {
  flex-shrink: 0;
  height: 48px;
  padding: 0 14px;
  border: none;
  border-radius: var(--radius-sm);
  background: var(--accent);
  color: #fff;
  font: inherit;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: -0.02em;
  white-space: nowrap;
  cursor: pointer;
  transition: opacity 0.2s var(--ease), transform 0.2s var(--ease);
}

.send-code:hover:not(:disabled) {
  opacity: 0.92;
}

.send-code:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.send-code--busy {
  pointer-events: none;
}

.field__hint--ok {
  color: var(--success);
}

.field__input {
  width: 100%;
  height: 48px;
  padding: 0 44px 0 14px;
  font: inherit;
  font-size: 17px;
  letter-spacing: -0.02em;
  color: var(--text);
  background: var(--fill-tertiary);
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  outline: none;
  transition: background 0.25s var(--ease), border-color 0.25s, box-shadow 0.25s;
}

.field__input::placeholder {
  color: var(--text-tertiary);
}

.field--focus .field__input {
  background: var(--surface);
  border-color: var(--accent);
  box-shadow: 0 0 0 4px rgba(0, 113, 227, 0.15);
}

.field--valid .field__input {
  border-color: rgba(52, 199, 89, 0.5);
}

.field--invalid .field__input {
  border-color: var(--error);
  animation: shake 0.45s var(--ease);
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  25% { transform: translateX(-5px); }
  50% { transform: translateX(5px); }
  75% { transform: translateX(-3px); }
}

.field__check {
  position: absolute;
  right: 12px;
  color: var(--success);
  display: flex;
}

.field__toggle {
  position: absolute;
  right: 8px;
  padding: 8px;
  border: none;
  background: none;
  color: var(--text-secondary);
  cursor: pointer;
  border-radius: 8px;
  transition: color 0.2s, background 0.2s;
}

.field__toggle:hover {
  color: var(--text);
  background: var(--fill-tertiary);
}

.field__hint,
.field__error {
  margin: 0;
  padding-left: 2px;
  font-size: 12px;
  letter-spacing: -0.01em;
}

.field__hint {
  color: var(--text-tertiary);
}

.field__error {
  color: var(--error);
}

.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 4px;
}

.chip {
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 500;
  border-radius: 99px;
  border: 1px solid var(--separator);
  background: var(--surface);
  color: var(--text-secondary);
  cursor: pointer;
  transition: background 0.2s, color 0.2s, border-color 0.2s, transform 0.2s var(--ease-spring);
}

.chip:hover {
  background: var(--fill-tertiary);
  color: var(--accent);
  border-color: rgba(0, 113, 227, 0.3);
  transform: scale(1.02);
}

.meter {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 2px;
}

.meter__bars {
  display: flex;
  gap: 4px;
  flex: 1;
}

.meter__bars span {
  flex: 1;
  height: 3px;
  border-radius: 99px;
  background: var(--separator);
  transition: background 0.35s var(--ease), transform 0.35s var(--ease-spring);
}

.meter[data-level='1'] .meter__bars span:nth-child(1) {
  background: var(--error);
  transform: scaleY(1.4);
}
.meter[data-level='2'] .meter__bars span:nth-child(-n+2) { background: var(--warn); }
.meter[data-level='3'] .meter__bars span:nth-child(-n+3) { background: var(--accent); }
.meter[data-level='4'] .meter__bars span { background: var(--success); }

.meter__label {
  font-size: 11px;
  color: var(--text-tertiary);
  min-width: 2em;
}

.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 14px;
}

.check {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  color: var(--text-secondary);
  user-select: none;
}

.check input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

.check__box {
  width: 18px;
  height: 18px;
  border-radius: 5px;
  border: 1.5px solid var(--separator-opaque);
  background: var(--surface);
  transition: background 0.2s, border-color 0.2s;
}

.check input:checked + .check__box {
  background: var(--accent);
  border-color: var(--accent);
}

.check input:checked + .check__box::after {
  content: '';
  display: block;
  width: 5px;
  height: 9px;
  margin: 1px auto;
  border: solid #fff;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.check input:focus-visible + .check__box {
  outline: 3px solid rgba(0, 113, 227, 0.35);
  outline-offset: 2px;
}

.link {
  color: var(--accent);
  text-decoration: none;
  font-size: 14px;
}

.link:hover {
  text-decoration: underline;
}

.btn {
  position: relative;
  height: 50px;
  border: none;
  border-radius: var(--radius-sm);
  font: inherit;
  font-size: 17px;
  font-weight: 500;
  letter-spacing: -0.02em;
  color: #fff;
  background: var(--accent);
  cursor: pointer;
  transition: background 0.2s, transform 0.2s var(--ease-spring), opacity 0.2s;
}

.btn:hover:not(:disabled) {
  background: var(--accent-hover);
}

.btn:active:not(:disabled) {
  transform: scale(0.98);
}

.btn:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.btn--loading .btn__text {
  opacity: 0;
}

.btn__spinner {
  position: absolute;
  inset: 0;
  margin: auto;
  width: 20px;
  height: 20px;
  border: 2px solid rgba(255, 255, 255, 0.35);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.65s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.card__foot {
  margin-top: 28px;
}

.divider {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  color: var(--text-tertiary);
  font-size: 13px;
}

.divider::before,
.divider::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--separator);
}

.oauth {
  display: flex;
  justify-content: center;
  gap: 12px;
}

.oauth__btn {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  border: 1px solid var(--separator);
  background: var(--surface);
  color: var(--text);
  cursor: pointer;
  display: grid;
  place-items: center;
  transition: background 0.2s, transform 0.25s var(--ease-spring), box-shadow 0.2s;
}

.oauth__btn:hover {
  background: var(--fill-tertiary);
  transform: scale(1.04);
  box-shadow: var(--shadow-sm);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s, transform 0.2s var(--ease-spring);
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: scale(0.8);
}

.chips-enter-active,
.chips-leave-active {
  transition: opacity 0.3s var(--ease), transform 0.3s var(--ease);
}

.chips-enter-from,
.chips-leave-to {
  opacity: 0;
  transform: translateY(-6px);
}
</style>
