<script setup>
import { ref, computed, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { forgePersona, forgeEventsUrl, forgeStatusUrl } from '../api/personaApi'

const router = useRouter()

// 表单状态只负责收集生成所需的最小信息，具体请求组装放在 api 层处理。
const characterName = ref('')
const workTitle = ref('')
const chapterRange = ref('')
const mode = ref('rule')
const file = ref(null)
const fileName = ref('')
const dragging = ref(false)
const loading = ref(false)
const error = ref('')
const result = ref(null)
const previewTab = ref('skill')

// 真实进度（后端上报；SSE 优先，轮询兜底）
const stageList = ref([]) // [{ key, title, description }]
const currentStageIndex = ref(0)
const progressPercent = ref(0)
let sseSource = null
let sseTimer = null
let pollTimer = null
let timeoutTimer = null

const canSubmit = computed(
  () => characterName.value.trim() && file.value && !loading.value,
)
const currentStage = computed(() => stageList.value[currentStageIndex.value])

const hasCard = computed(() => !!result.value?.cardJson)
const previewContent = computed(() => {
  if (!result.value) return ''
  if (previewTab.value === 'evidence') return result.value.evidenceMarkdown || ''
  if (previewTab.value === 'card') return result.value.cardJson || ''
  return result.value.skillMarkdown || ''
})

function normalizeResult(data) {
  const s = data.summary
  const summaryText =
    typeof s === 'string'
      ? s
      : s?.oneLiner
        ? `${s.oneLiner}（抽取台词 ${s.quoteCount ?? 0} 条）`
        : data.message || '生成完成'
  return {
    slug: data.slug,
    skillMarkdown: data.skillMarkdown,
    evidenceMarkdown: data.evidenceMarkdown,
    cardJson: data.cardJson,
    summary: summaryText,
  }
}

function clearTimers() {
  if (sseTimer) { clearTimeout(sseTimer); sseTimer = null }
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null }
  if (timeoutTimer) { clearTimeout(timeoutTimer); timeoutTimer = null }
}

function closeSse() {
  if (sseSource) {
    sseSource.close()
    sseSource = null
  }
  clearTimers()
}

function handleProgress(data) {
  if (data.error) {
    closeSse()
    loading.value = false
    error.value = data.message || '任务已失效，请重新提交'
    return
  }

  if (Array.isArray(data.stages) && data.stages.length) {
    stageList.value = data.stages
  }
  const idx = stageList.value.findIndex((s) => s.key === data.currentStage?.key)
  if (idx >= 0) currentStageIndex.value = idx
  if (typeof data.percent === 'number') progressPercent.value = data.percent

  if (data.done) {
    closeSse()
    loading.value = false
    if (data.result) {
      result.value = normalizeResult(data.result)
      previewTab.value = 'skill'
    } else {
      error.value = data.message || '生成失败'
    }
  }
}

function startPolling(jobId) {
  pollTimer = setInterval(async () => {
    try {
      const res = await fetch(forgeStatusUrl(jobId))
      if (res.status === 404) {
        // 任务不存在（后端重启/过期）：明确报错而不是无限轮询
        closeSse()
        loading.value = false
        error.value = '生成任务不存在或已失效，请重新提交'
        return
      }
      const data = await res.json()
      handleProgress(data)
    } catch {
      // 单次轮询失败忽略，继续下一次
    }
  }, 1000)
}

function openSse(jobId) {
  closeSse()
  const source = new EventSource(forgeEventsUrl(jobId))
  sseSource = source
  let received = false

  // 整体超时兜底：10 分钟未完成视为超时
  timeoutTimer = setTimeout(() => {
    if (loading.value) {
      closeSse()
      loading.value = false
      error.value = '生成超时（超过 10 分钟），请重试'
    }
  }, 600_000)

  // 3 秒内未收到 SSE 事件（部分环境 EventSource 被拦截）→ 切换轮询兜底
  sseTimer = setTimeout(() => {
    if (!received && loading.value) {
      source.close()
      sseSource = null
      startPolling(jobId)
    }
  }, 3000)

  source.onmessage = (e) => {
    received = true
    let data
    try {
      data = JSON.parse(e.data)
    } catch {
      return
    }
    handleProgress(data)
  }

  source.onerror = () => {
    // EventSource 会自动重连；长时间无事件由 sseTimer 切换轮询兜底
  }
}

function onPick(e) {
  const f = e.target.files?.[0]
  if (f) setFile(f)
}

function onDrop(e) {
  e.preventDefault()
  dragging.value = false
  const f = e.dataTransfer.files?.[0]
  if (f) setFile(f)
}

function setFile(f) {
  const ok = /\.(txt|md)$/i.test(f.name)
  if (!ok) {
    error.value = '请上传 .txt 或 .md 文本文件'
    return
  }
  file.value = f
  fileName.value = f.name
  error.value = ''
}

async function submit() {
  if (!canSubmit.value) return
  loading.value = true
  error.value = ''
  result.value = null
  stageList.value = []
  currentStageIndex.value = 0
  progressPercent.value = 0
  closeSse()
  try {
    const data = await forgePersona({
      file: file.value,
      characterName: characterName.value.trim(),
      workTitle: workTitle.value.trim(),
      chapterRange: chapterRange.value.trim(),
      mode: mode.value,
    })
    openSse(data.jobId)
  } catch (e) {
    closeSse()
    loading.value = false
    error.value = e.message || '提交失败'
  }
}

function download(content, filename) {
  const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

function downloadSkill() {
  if (!result.value?.skillMarkdown) return
  download(result.value.skillMarkdown, 'SKILL.md')
}

function downloadEvidence() {
  if (!result.value?.evidenceMarkdown) return
  download(result.value.evidenceMarkdown, 'source-evidence.md')
}

function downloadCard() {
  if (!result.value?.cardJson) return
  download(result.value.cardJson, 'character-card.json')
}

onBeforeUnmount(closeSse)
</script>

<template>
  <div class="forge">
    <header class="forge__head">
      <button type="button" class="back" @click="router.push('/home')">← 主页</button>
      <div>
        <h1 class="forge__title">Persona 工坊</h1>
        <p class="forge__sub">依据 novel-character-persona-forge 工作流，从小说文本生成角色 Skill</p>
      </div>
    </header>

    <form class="forge__form" @submit.prevent="submit">
      <label class="field">
        <span>角色姓名 *</span>
        <input v-model="characterName" type="text" placeholder="如：孙悟空" required />
      </label>

      <div class="field-row">
        <label class="field">
          <span>作品名</span>
          <input v-model="workTitle" type="text" placeholder="如：西游记" />
        </label>
        <label class="field">
          <span>章节范围</span>
          <input v-model="chapterRange" type="text" placeholder="如：第1-10章" />
        </label>
      </div>

      <div class="mode">
        <span class="mode__label">生成模式</span>
        <div class="mode__choices">
          <label class="mode__choice" :class="{ 'mode__choice--active': mode === 'rule' }">
            <input v-model="mode" type="radio" value="rule" :disabled="loading" />
            <span>规则模式</span>
          </label>
          <label class="mode__choice" :class="{ 'mode__choice--active': mode === 'ai' }">
            <input v-model="mode" type="radio" value="ai" :disabled="loading" />
            <span>AI 精修</span>
          </label>
        </div>
        <p class="mode__hint">
          AI 模式会通读原文、抽取证据并综合人格与示例对话；需后端配置 AI Provider API Key。
        </p>
      </div>

      <div
        class="drop"
        :class="{ 'drop--drag': dragging, 'drop--has': fileName }"
        @dragover.prevent="dragging = true"
        @dragleave="dragging = false"
        @drop="onDrop"
      >
        <input
          id="file"
          type="file"
          accept=".txt,.md,text/plain"
          hidden
          :disabled="loading"
          @change="onPick"
        />
        <label for="file" class="drop__label">
          <span v-if="!fileName">拖入或点击上传 .txt / .md</span>
          <span v-else class="drop__name">{{ fileName }}</span>
        </label>
      </div>

      <p v-if="error" class="err">{{ error }}</p>
      <div v-if="loading" class="loading-panel" role="status" aria-live="polite">
        <div class="loading-panel__head">
          <span class="spinner" aria-hidden="true"></span>
          <div>
            <p class="stage">{{ currentStage?.title || '准备中…' }}</p>
            <p class="stage-detail">{{ currentStage?.description || '等待生成服务响应…' }}</p>
          </div>
          <span class="stage-count">{{ stageList.length ? `${currentStageIndex + 1}/${stageList.length}` : '…' }}</span>
        </div>
        <div class="progress" aria-hidden="true">
          <span :style="{ width: `${progressPercent}%` }"></span>
        </div>
        <ol class="stage-list">
          <li
            v-for="(item, index) in stageList"
            :key="item.key"
            :class="{
              'stage-list__item--done': index < currentStageIndex,
              'stage-list__item--active': index === currentStageIndex,
            }"
          >
            {{ item.title }}
          </li>
        </ol>
      </div>

      <button type="submit" class="btn-primary" :disabled="!canSubmit">
        {{ loading ? '生成中…' : '生成 Persona Skill' }}
      </button>
    </form>

    <section v-if="result" class="result">
      <div class="result__meta">
        <h2>生成完成</h2>
        <p>{{ result.summary }}</p>
        <p class="slug">persona-{{ result.slug }}</p>
      </div>

      <div class="result__actions">
        <button type="button" class="btn-dl" @click="downloadSkill">下载 SKILL.md</button>
        <button type="button" class="btn-dl btn-dl--alt" @click="downloadEvidence">
          下载 source-evidence.md
        </button>
        <button v-if="hasCard" type="button" class="btn-dl btn-dl--alt" @click="downloadCard">
          下载 character-card.json
        </button>
      </div>

      <div class="tabs">
        <button
          type="button"
          :class="{ active: previewTab === 'skill' }"
          @click="previewTab = 'skill'"
        >
          SKILL.md
        </button>
        <button
          type="button"
          :class="{ active: previewTab === 'evidence' }"
          @click="previewTab = 'evidence'"
        >
          source-evidence.md
        </button>
        <button
          v-if="hasCard"
          type="button"
          :class="{ active: previewTab === 'card' }"
          @click="previewTab = 'card'"
        >
          character-card.json
        </button>
      </div>
      <pre class="preview">{{ previewContent }}</pre>

      <p class="note">
        规则模式为模板填充；AI 模式由大模型通读原文综合人格。生成结果仅供个人/私有使用，请人工复核。
      </p>
    </section>
  </div>
</template>

<style scoped>
.forge {
  min-height: 100dvh;
  padding: 24px;
  max-width: 720px;
  margin: 0 auto;
}

.forge__head {
  margin-bottom: 28px;
}

.back {
  border: none;
  background: none;
  color: var(--accent);
  font: inherit;
  font-size: 14px;
  cursor: pointer;
  padding: 4px 0;
  margin-bottom: 12px;
  border-radius: 6px;
  transition: opacity 0.16s var(--ease-out), transform 0.16s var(--ease-out);
}

.back:active {
  transform: scale(0.97);
  opacity: 0.75;
}

.forge__title {
  margin: 0 0 6px;
  font-size: 28px;
  font-weight: 600;
  letter-spacing: 0;
}

.forge__sub {
  margin: 0;
  font-size: 14px;
  color: var(--text-secondary);
  line-height: 1.5;
}

.forge__form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.field span {
  display: block;
  font-size: 13px;
  font-weight: 500;
  margin-bottom: 6px;
  color: var(--text-secondary);
}

.field input {
  width: 100%;
  padding: 12px 14px;
  border: 1px solid var(--separator);
  border-radius: var(--radius-md);
  font: inherit;
  background: var(--surface);
}

.field-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.mode {
  display: grid;
  gap: 8px;
}

.mode__label {
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
}

.mode__choices {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.mode__choice {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 44px;
  border: 1px solid var(--separator);
  border-radius: var(--radius-md);
  background: var(--surface);
  font-size: 14px;
  cursor: pointer;
  transition: border-color 0.2s var(--ease-out), color 0.2s var(--ease-out),
    background 0.2s var(--ease-out);
}

.mode__choice input {
  accent-color: var(--accent);
}

.mode__choice--active {
  border-color: var(--accent);
  color: var(--accent);
  background: rgba(26, 26, 28, 0.03);
}

.mode__hint {
  margin: 0;
  font-size: 12px;
  color: var(--text-tertiary);
  line-height: 1.5;
}

@media (max-width: 520px) {
  .field-row {
    grid-template-columns: 1fr;
  }

  .mode__choices {
    grid-template-columns: 1fr;
  }
}

.drop {
  border: 2px dashed var(--separator);
  border-radius: var(--radius-lg);
  padding: 36px 20px;
  text-align: center;
  transition: border-color 0.2s var(--ease-out), background 0.2s var(--ease-out);
}

.drop--drag,
.drop--has {
  border-color: var(--accent);
  background: rgba(26, 26, 28, 0.03);
}

.drop__label {
  cursor: pointer;
  font-size: 15px;
  color: var(--text-secondary);
}

.drop__name {
  color: var(--text-primary);
  font-weight: 500;
}

.err {
  color: #ff3b30;
  font-size: 14px;
  margin: 0;
}

.loading-panel {
  display: grid;
  gap: 12px;
  padding: 14px;
  border: 1px solid rgba(26, 26, 28, 0.12);
  border-radius: var(--radius-md);
  background: rgba(26, 26, 28, 0.03);
}

.loading-panel__head {
  display: grid;
  grid-template-columns: 24px 1fr auto;
  align-items: center;
  gap: 10px;
}

.spinner {
  width: 20px;
  height: 20px;
  border: 2px solid rgba(26, 26, 28, 0.1);
  border-top-color: var(--accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.stage {
  color: var(--accent);
  font-size: 14px;
  font-weight: 600;
  margin: 0 0 2px;
}

.stage-detail {
  color: var(--text-secondary);
  font-size: 12px;
  line-height: 1.45;
  margin: 0;
}

.stage-count {
  color: var(--text-tertiary);
  font-size: 12px;
  white-space: nowrap;
}

.progress {
  height: 4px;
  overflow: hidden;
  border-radius: 999px;
  background: rgba(26, 26, 28, 0.08);
}

.progress span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: var(--accent);
  transition: width 0.32s var(--ease-out);
}

.stage-list {
  display: grid;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.stage-list li {
  position: relative;
  min-height: 18px;
  padding-left: 20px;
  color: var(--text-tertiary);
  font-size: 12px;
  line-height: 1.5;
}

.stage-list li::before {
  content: '';
  position: absolute;
  left: 0;
  top: 6px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--separator);
}

.stage-list__item--done,
.stage-list__item--active {
  color: var(--text-primary);
}

.stage-list__item--done::before,
.stage-list__item--active::before {
  background: var(--accent);
}

.stage-list__item--active::before {
  box-shadow: 0 0 0 4px rgba(26, 26, 28, 0.08);
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.btn-primary {
  padding: 14px;
  border: none;
  border-radius: var(--radius-md);
  background: var(--accent);
  color: #fff;
  font: inherit;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.2s var(--ease-out), transform 0.16s var(--ease-out),
    background 0.2s var(--ease-out);
}

.btn-primary:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

@media (hover: hover) and (pointer: fine) {
  .btn-primary:not(:disabled):hover {
    opacity: 0.92;
  }
}

.btn-primary:not(:disabled):active {
  transform: scale(0.97);
}

.result {
  margin-top: 36px;
  padding-top: 28px;
  border-top: 1px solid var(--separator);
}

.result__meta h2 {
  margin: 0 0 8px;
  font-size: 20px;
}

.result__meta p {
  margin: 0 0 6px;
  font-size: 14px;
  color: var(--text-secondary);
}

.slug {
  font-family: ui-monospace, monospace;
  font-size: 13px;
  color: var(--accent);
}

.result__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin: 16px 0;
}

.btn-dl {
  padding: 10px 16px;
  border-radius: var(--radius-md);
  border: none;
  background: var(--accent);
  color: #fff;
  font: inherit;
  font-size: 14px;
  cursor: pointer;
  transition: transform 0.16s var(--ease-out), opacity 0.2s var(--ease-out);
}

.btn-dl:active {
  transform: scale(0.97);
}

.btn-dl--alt {
  background: var(--fill-tertiary);
  color: var(--text-primary);
}

.tabs {
  display: flex;
  gap: 8px;
  margin-bottom: 10px;
}

.tabs button {
  padding: 8px 14px;
  border: 1px solid var(--separator);
  border-radius: 99px;
  background: var(--surface);
  font: inherit;
  font-size: 13px;
  cursor: pointer;
  transition: border-color 0.2s var(--ease-out), color 0.2s var(--ease-out),
    transform 0.16s var(--ease-out);
}

.tabs button:active {
  transform: scale(0.97);
}

.tabs button.active {
  border-color: var(--accent);
  color: var(--accent);
}

.preview {
  max-height: 360px;
  overflow: auto;
  padding: 16px;
  border-radius: var(--radius-md);
  background: #1a1a1a;
  color: #e8e8e8;
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}

.note {
  margin-top: 14px;
  font-size: 12px;
  color: var(--text-tertiary);
  line-height: 1.5;
}
</style>
