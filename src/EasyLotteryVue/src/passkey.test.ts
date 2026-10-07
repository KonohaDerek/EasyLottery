import { describe, expect, it } from 'vitest'

import { decodeBase64Url, encodeBase64Url, preparePublicKeyOptions } from './passkey'

describe('WebAuthn option conversion', () => {
  it('round-trips base64url bytes without padding', () => {
    const original = Uint8Array.from([0, 1, 2, 253, 254, 255])
    const encoded = encodeBase64Url(original)

    expect(encoded).not.toContain('=')
    expect([...new Uint8Array(decodeBase64Url(encoded))]).toEqual([...original])
  })

  it('converts nested credential identifiers without mutating the API response', () => {
    const response = {
      publicKey: {
        challenge: 'AAEC',
        user: { id: 'AQI=', name: 'admin@example.com' },
        allowCredentials: [{ id: '_w', type: 'public-key' }],
        excludeCredentials: [{ id: 'AwQ', type: 'public-key' }],
      },
    }

    const prepared = preparePublicKeyOptions(response)

    expect([...new Uint8Array(prepared.challenge)]).toEqual([0, 1, 2])
    expect([...new Uint8Array(prepared.user?.id ?? new ArrayBuffer(0))]).toEqual([1, 2])
    expect([...new Uint8Array(prepared.allowCredentials?.[0]?.id ?? new ArrayBuffer(0))]).toEqual([255])
    expect([...new Uint8Array(prepared.excludeCredentials?.[0]?.id ?? new ArrayBuffer(0))]).toEqual([3, 4])
    expect(response.publicKey.challenge).toBe('AAEC')
    expect(response.publicKey.user.id).toBe('AQI=')
  })
})
