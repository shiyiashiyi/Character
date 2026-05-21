/**
 * personaApi.js — Persona Skill 生成接口
 */

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5050'

export async function forgePersona({ file, characterName, workTitle, chapterRange }) {
  const form = new FormData()
  form.append('file', file)
  form.append('characterName', characterName)
  if (workTitle) form.append('workTitle', workTitle)
  if (chapterRange) form.append('chapterRange', chapterRange)

  const res = await fetch(`${API_BASE}/api/persona/forge`, {
    method: 'POST',
    body: form,
  })

  const data = await res.json().catch(() => ({}))
  if (!res.ok || data.success === false) {
    const err = new Error(data.message || `生成失败 (${res.status})`)
    err.data = data
    throw err
  }
  return data
}
