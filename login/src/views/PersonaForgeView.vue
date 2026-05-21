<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { forgePersona } from '../api/personaApi'

const router = useRouter()

const characterName = ref('')
const workTitle = ref('')
const chapterRange = ref('')
const file = ref(null)
const fileName = ref('')
const dragging = ref(false)
const loading = ref(false)
const stage = ref('')
const error = ref('')
const result = ref(null)
const previewTab = ref('skill')

const canSubmit = computed(
  () => characterName.value.trim() && file.value && !loading.value,
)

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
    summary: summaryText,
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
  stage.value = '正在读取文本…'
  try {
    stage.value = '抽取台词与证据…'
    const data = await forgePersona({
      file: file.value,
      characterName: characterName.value.trim(),
      workTitle: workTitle.value.trim(),
      chapterRange: chapterRange.value.trim(),
    })
    stage.value = '生成 Skill 文件…'
    result.value = normalizeResult(data)
    previewTab.value = 'skill'
  } catch (e) {
    error.value = e.message || '生成失败'
  } finally {
    loading.value = false
    stage.value = ''
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

      <div
        class="drop"
        :class="{ 'drop--drag': dragging, 'drop--has': fileName }"
        @dragover.prevent="dragging = true"
        @dragleave="dragging = false"
        @drop="onDrop"
      >
        <input id="file" type="file" accept=".txt,.md,text/plain" hidden @change="onPick" />
        <label for="file" class="drop__label">
          <span v-if="!fileName">拖入或点击上传 .txt / .md</span>
          <span v-else class="drop__name">{{ fileName }}</span>
        </label>
      </div>

      <p v-if="error" class="err">{{ error }}</p>
      <p v-if="loading" class="stage">{{ stage }}</p>

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
      </div>
      <pre class="preview">{{
        previewTab === 'skill' ? result.skillMarkdown : result.evidenceMarkdown
      }}</pre>

      <p class="note">
        当前为规则抽取 + 模板填充（非大模型）。台词不足 8 条时会在证据文件中标注。仅供个人/私有使用。
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
  padding: 0;
  margin-bottom: 12px;
}

.forge__title {
  margin: 0 0 6px;
  font-size: 28px;
  font-weight: 600;
  letter-spacing: -0.03em;
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

@media (max-width: 520px) {
  .field-row {
    grid-template-columns: 1fr;
  }
}

.drop {
  border: 2px dashed var(--separator);
  border-radius: var(--radius-lg);
  padding: 36px 20px;
  text-align: center;
  transition: border-color 0.2s, background 0.2s;
}

.drop--drag,
.drop--has {
  border-color: var(--accent);
  background: rgba(0, 113, 227, 0.04);
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

.stage {
  color: var(--accent);
  font-size: 14px;
  margin: 0;
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
  transition: opacity 0.2s, transform 0.2s var(--ease-spring);
}

.btn-primary:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.btn-primary:not(:disabled):hover {
  transform: scale(1.01);
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
