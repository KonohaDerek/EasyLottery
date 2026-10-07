import { defineStore } from 'pinia'
import { ref } from 'vue'

import { ApiError, clearSessionToken, hasSessionToken, requestJson, setSessionToken } from '@/api'
import { createPasskey, getPasskey, type PasskeyOptions } from '@/passkey'

export interface PasskeyDevice {
  id: string
  name: string
  createdAtUtc: string
}

interface PasskeyBeginResponse {
  flow: string
  options: PasskeyOptions
}

interface PasskeyTokenResponse {
  token: string
  expiresAtUtc: string
}

export const useAuthStore = defineStore('auth', () => {
  const isAuthenticated = ref(hasSessionToken())
  const isBusy = ref(false)
  const error = ref('')

  function refreshSession(): boolean {
    isAuthenticated.value = hasSessionToken()
    return isAuthenticated.value
  }

  async function login(email: string): Promise<void> {
    isBusy.value = true
    error.value = ''
    try {
      try {
        await loginWithFlow(email, 'login')
      } catch (cause) {
        if (!(cause instanceof ApiError) || cause.code !== 'registration_required') throw cause
        await loginWithFlow(email, 'register')
      }
      isAuthenticated.value = true
    } catch (cause) {
      error.value = getErrorMessage(cause)
      throw cause
    } finally {
      isBusy.value = false
    }
  }

  async function loadPasskeys(): Promise<PasskeyDevice[]> {
    return requestJson<PasskeyDevice[]>('/api/admin/passkeys')
  }

  async function addPasskey(name: string): Promise<void> {
    const begin = await requestJson<PasskeyBeginResponse>(
      '/api/admin/passkeys/options',
      'POST',
      { name },
    )
    const credential = await createPasskey(begin.options)
    await requestJson<PasskeyDevice>('/api/admin/passkeys/verify', 'POST', { credential })
  }

  async function removePasskey(id: string): Promise<void> {
    await requestJson<void>(`/api/admin/passkeys/${encodeURIComponent(id)}`, 'DELETE')
  }

  async function logout(): Promise<void> {
    try {
      await requestJson<void>('/api/session', 'DELETE')
    } catch {
      // Clearing the browser token is still safe if server-side revocation fails.
    } finally {
      clearSessionToken()
      isAuthenticated.value = false
    }
  }

  async function loginWithFlow(email: string, flow: 'login' | 'register'): Promise<void> {
    const begin = await requestJson<PasskeyBeginResponse>(
      '/api/auth/passkey/options',
      'POST',
      { email, flow },
      false,
    )
    const credential = flow === 'register'
      ? await createPasskey(begin.options)
      : await getPasskey(begin.options)
    const result = await requestJson<PasskeyTokenResponse>(
      '/api/auth/passkey/verify',
      'POST',
      { email, flow, credential },
      false,
    )
    setSessionToken(result.token)
  }

  return {
    isAuthenticated,
    isBusy,
    error,
    refreshSession,
    login,
    loadPasskeys,
    addPasskey,
    removePasskey,
    logout,
  }
})

function getErrorMessage(cause: unknown): string {
  return cause instanceof Error ? cause.message : '無法完成 Passkey 登入。'
}
