/**
 * useAuthForm.js — 登录/注册表单、校验与 API 调用
 */
import { ref, computed, watch } from 'vue'
import { useStatus } from './useStatus'
import * as authApi from '../api/authApi'

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/
const STRENGTH_LABELS = ['', '弱', '一般', '良好', '强']
const EMAIL_SUFFIXES = ['@gmail.com', '@outlook.com', '@icloud.com']
const LOGIN_STAGES = [
  { label: '正在验证凭据…', progress: 35, ms: 420 },
  { label: '同步用户配置…', progress: 62, ms: 380 },
  { label: '加载工作台…', progress: 88, ms: 340 },
  { label: '即将完成…', progress: 96, ms: 280 },
]

function scorePassword(pwd) {
  let s = 0
  if (pwd.length >= 8) s++
  if (/[a-z]/.test(pwd) && /[A-Z]/.test(pwd)) s++
  if (/\d/.test(pwd)) s++
  if (/[^a-zA-Z0-9]/.test(pwd)) s++
  return Math.min(4, s)
}

export function useAuthForm() {
  const { setStatus } = useStatus()

  const mode = ref('login')
  const email = ref('')
  const password = ref('')
  const confirmPassword = ref('')
  const displayName = ref('')
  const remember = ref(false)
  const showPassword = ref(false)
  const slideDone = ref(false)
  const busy = ref(false)
  const successVisible = ref(false)
  const successMessage = ref('')
  const authError = ref('')

  const emailFocused = ref(false)
  const pwdFocused = ref(false)
  const emailTouched = ref(false)
  const pwdTouched = ref(false)
  const confirmTouched = ref(false)

  const isLogin = computed(() => mode.value === 'login')
  const isRegister = computed(() => mode.value === 'register')

  const emailValid = computed(() => EMAIL_RE.test(email.value.trim()))
  const pwdValid = computed(() => password.value.length >= 8)
  const confirmValid = computed(
    () => !isRegister.value || password.value === confirmPassword.value
  )
  const pwdLevel = computed(() => scorePassword(password.value))
  const pwdLevelLabel = computed(() =>
    password.value ? STRENGTH_LABELS[pwdLevel.value] : '强度'
  )

  const showEmailChips = computed(() => {
    const val = email.value
    const parts = val.split('@')
    return val.length > 0 && (parts.length === 1 || (parts.length === 2 && !parts[1]))
  })

  const emailError = computed(() => {
    if (!emailTouched.value || !email.value.trim()) return ''
    return emailValid.value ? '' : '请输入有效的邮箱地址'
  })

  const pwdError = computed(() => {
    if (authError.value && isLogin.value) return authError.value
    if (!pwdTouched.value || !password.value) return ''
    return pwdValid.value ? '' : '密码至少需要 8 个字符'
  })

  const confirmError = computed(() => {
    if (!confirmTouched.value || !confirmPassword.value) return ''
    return confirmValid.value ? '' : '两次输入的密码不一致'
  })

  const fieldsReady = computed(() => emailValid.value && pwdValid.value)
  const canSubmitLogin = computed(
    () => isLogin.value && fieldsReady.value && slideDone.value && !busy.value
  )
  const canSubmitRegister = computed(
    () =>
      isRegister.value &&
      fieldsReady.value &&
      confirmValid.value &&
      confirmPassword.value.length > 0 &&
      !busy.value
  )

  watch(mode, () => {
    authError.value = ''
    slideDone.value = false
    emailTouched.value = false
    pwdTouched.value = false
    confirmTouched.value = false
    setStatus(
      mode.value === 'login'
        ? '登录 — 请先填写邮箱与密码，并完成滑动确认'
        : '注册 — 填写邮箱、昵称与密码',
      '',
      0
    )
  })

  function switchMode(next) {
    mode.value = next
  }

  function onEmailInput() {
    authError.value = ''
    if (emailValid.value) setStatus('邮箱格式正确', 'ok', 35)
    else if (email.value) setStatus('正在检查邮箱格式', '', 20)
  }

  function onEmailBlur() {
    emailTouched.value = true
    emailFocused.value = false
    if (email.value && !emailValid.value) setStatus('邮箱格式不正确', 'error', 15)
    else if (emailValid.value) setStatus('邮箱格式正确', 'ok', 35)
  }

  function onEmailFocus() {
    emailFocused.value = true
    setStatus('正在编辑邮箱', '', null)
  }

  function onPwdInput() {
    authError.value = ''
    const level = pwdLevel.value
    if (pwdValid.value) {
      setStatus(`密码强度：${STRENGTH_LABELS[level]}`, level >= 3 ? 'ok' : 'warn', 55)
    }
  }

  function onPwdBlur() {
    pwdTouched.value = true
    pwdFocused.value = false
    if (password.value && !pwdValid.value) setStatus('密码长度不足', 'warn', 25)
  }

  function onPwdFocus() {
    pwdFocused.value = true
    setStatus('正在编辑密码', '', null)
  }

  function onConfirmBlur() {
    confirmTouched.value = true
  }

  function applySuffix(suffix) {
    const base = email.value.split('@')[0] || email.value
    email.value = base + suffix
    onEmailInput()
    setStatus('已补全邮箱后缀', 'ok', 40)
  }

  function onSlideComplete() {
    slideDone.value = true
    setStatus('已确认，可以登录', 'ok', 75)
  }

  async function runStagedProgress() {
    for (const stage of LOGIN_STAGES) {
      setStatus(stage.label, 'busy', stage.progress)
      await new Promise((r) => setTimeout(r, stage.ms))
    }
  }

  function showSuccess(user, actionLabel) {
    const name = user.displayName || user.email.split('@')[0]
    successMessage.value = `${actionLabel}成功 — 你好，${name}`
    successVisible.value = true
    setStatus(`${actionLabel}成功`, 'ok', 100)
  }

  async function submitLogin() {
    if (busy.value || !canSubmitLogin.value) return

    emailTouched.value = true
    pwdTouched.value = true

    if (!emailValid.value || !pwdValid.value || !slideDone.value) {
      setStatus('请先完成所有必填项与滑动确认', 'warn', 20)
      return
    }

    busy.value = true
    authError.value = ''
    setStatus('正在连接服务器…', 'busy', 12)

    try {
      await Promise.all([
        runStagedProgress(),
        authApi.login({
          email: email.value.trim(),
          password: password.value,
        }).then((data) => {
          showSuccess(data.user, '登录')
        }),
      ])
    } catch (e) {
      slideDone.value = false
      authError.value = e.message || '登录失败'
      setStatus(authError.value, 'error', 0)
    } finally {
      busy.value = false
    }
  }

  async function submitRegister() {
    if (busy.value || !canSubmitRegister.value) return

    emailTouched.value = true
    pwdTouched.value = true
    confirmTouched.value = true

    if (!emailValid.value || !pwdValid.value || !confirmValid.value) {
      setStatus('请检查表单填写', 'warn', 20)
      return
    }

    busy.value = true
    authError.value = ''
    setStatus('正在创建账号…', 'busy', 30)

    try {
      const data = await authApi.register({
        email: email.value.trim(),
        password: password.value,
        displayName: displayName.value.trim() || null,
      })
      showSuccess(data.user, '注册')
      mode.value = 'login'
      password.value = ''
      confirmPassword.value = ''
    } catch (e) {
      authError.value = e.message || '注册失败'
      setStatus(authError.value, e.status === 409 ? 'warn' : 'error', 0)
    } finally {
      busy.value = false
    }
  }

  function submit() {
    if (isLogin.value) return submitLogin()
    return submitRegister()
  }

  return {
    mode,
    isLogin,
    isRegister,
    email,
    password,
    confirmPassword,
    displayName,
    remember,
    showPassword,
    slideDone,
    busy,
    successVisible,
    successMessage,
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
    emailSuffixes: EMAIL_SUFFIXES,
    switchMode,
    onEmailInput,
    onEmailBlur,
    onEmailFocus,
    onPwdInput,
    onPwdBlur,
    onPwdFocus,
    onConfirmBlur,
    applySuffix,
    onSlideComplete,
    submit,
    submitLogin,
    submitRegister,
    setStatus,
  }
}
