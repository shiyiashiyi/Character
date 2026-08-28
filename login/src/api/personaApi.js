/**
 * personaApi.js — Persona Skill 生成接口（异步任务 + SSE 真实进度）
 */

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5050'

/** 提交生成任务，返回 { jobId, status, statusUrl, eventsUrl } */
export async function forgePersona({ file, characterName, workTitle, chapterRange, mode }) {
  const form = new FormData()
  form.append('file', file)
  form.append('characterName', characterName)
  if (mode) form.append('mode', mode)
  if (workTitle) form.append('workTitle', workTitle)
  if (chapterRange) form.append('chapterRange', chapterRange)

  // 提交只等任务入队，30 秒足够；生成过程由 SSE 跟进
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), 30_000)

  try {
    const res = await fetch(`${API_BASE}/api/persona/forge`, {
      method: 'POST',
      body: form,
      signal: controller.signal,
    })

    const data = await res.json().catch(() => ({}))
    if (!res.ok) {
      const err = new Error(data.message || `提交失败 (${res.status})`)
      err.data = data
      throw err
    }
    return data
  } catch (e) {
    if (e.name === 'AbortError') throw new Error('提交超时，请重试')
    throw e
  } finally {
    clearTimeout(timer)
  }
}

export function forgeStatusUrl(jobId) {
  return `${API_BASE}/api/persona/forge/${jobId}`
}

export function forgeEventsUrl(jobId) {
  return `${API_BASE}/api/persona/forge/${jobId}/events`
}
