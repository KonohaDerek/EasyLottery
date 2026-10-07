const sessionTokenKey = 'easy-lottery.session-token'
const sessionTokenHeader = 'X-EasyLottery-Session-Token'

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

function decodeJwtPayload(token: string): { exp?: number; token_use?: string } | null {
  try {
    const payload = token.split('.')[1]
    if (!payload) return null
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const decoded = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '='))
    return JSON.parse(decoded) as { exp?: number; token_use?: string }
  } catch {
    return null
  }
}

export function getSessionToken(): string | null {
  try {
    const token = window.sessionStorage.getItem(sessionTokenKey)
    if (!token) return null

    const payload = decodeJwtPayload(token)
    if (!payload?.exp || payload.exp * 1000 <= Date.now() + 5_000) {
      window.sessionStorage.removeItem(sessionTokenKey)
      return null
    }

    return token
  } catch {
    return null
  }
}

export function hasSessionToken(): boolean {
  return getSessionToken() !== null
}

export function setSessionToken(token: string): void {
  const value = token.trim()
  if (!value) throw new Error('登入服務沒有提供有效的工作階段。')
  window.sessionStorage.setItem(sessionTokenKey, value)
}

export function clearSessionToken(): void {
  try {
    window.sessionStorage.removeItem(sessionTokenKey)
  } catch {
    // Browsing remains available even if storage is disabled.
  }
}

export async function requestJson<T>(
  path: string,
  method: 'GET' | 'POST' | 'DELETE' = 'GET',
  body?: unknown,
  authorized = true,
): Promise<T> {
  const headers = new Headers({ Accept: 'application/json' })
  if (body !== undefined) headers.set('Content-Type', 'application/json')

  if (authorized) {
    const token = getSessionToken()
    if (token) headers.set(sessionTokenHeader, token)
  }

  const response = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    credentials: 'same-origin',
    cache: 'no-store',
  })

  if (response.status === 204) return undefined as T

  const text = await response.text()
  const payload = parseResponse(text)
  if (!response.ok) {
    const detail = payload && typeof payload === 'object'
      ? payload as { error?: unknown; title?: unknown; code?: unknown }
      : null
    const message = typeof detail?.error === 'string'
      ? detail.error
      : typeof detail?.title === 'string'
        ? detail.title
        : `伺服器回傳 HTTP ${response.status}。`
    const code = typeof detail?.code === 'string' ? detail.code : 'request_failed'
    throw new ApiError(message, response.status, code)
  }

  return payload as T
}

function parseResponse(text: string): unknown {
  if (!text) return undefined
  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}
