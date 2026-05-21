/**
 * authApi.js — 调用 .NET Core 认证接口
 */

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5050'

async function postAuth(path, body) {
  const res = await fetch(`${API_BASE}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

  const data = await res.json().catch(() => ({}))
  if (!res.ok) {
    const err = new Error(data.message || `请求失败 (${res.status})`)
    err.status = res.status
    err.data = data
    throw err
  }
  return data
}

export function register({ email, password, displayName }) {
  return postAuth('/api/auth/register', { email, password, displayName: displayName || null })
}

export function login({ email, password }) {
  return postAuth('/api/auth/login', { email, password })
}
