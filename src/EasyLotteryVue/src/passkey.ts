export interface PasskeyOptions {
  publicKey?: ApiPublicKeyOptions
  [key: string]: unknown
}

interface ApiCredentialDescriptor {
  id: string
  [key: string]: unknown
}

interface ApiPublicKeyOptions {
  challenge: string
  user?: { id: string; [key: string]: unknown }
  allowCredentials?: ApiCredentialDescriptor[]
  excludeCredentials?: ApiCredentialDescriptor[]
  [key: string]: unknown
}

interface BrowserCredentialDescriptor extends Omit<ApiCredentialDescriptor, 'id'> {
  id: ArrayBuffer
}

interface BrowserPublicKeyOptions extends Omit<ApiPublicKeyOptions, 'challenge' | 'user' | 'allowCredentials' | 'excludeCredentials'> {
  challenge: ArrayBuffer
  user?: { id: ArrayBuffer; [key: string]: unknown }
  allowCredentials?: BrowserCredentialDescriptor[]
  excludeCredentials?: BrowserCredentialDescriptor[]
}

export interface SerializedCredential {
  id: string
  rawId: string
  type: string
  authenticatorAttachment: string | null
  response: Record<string, unknown>
  clientExtensionResults: AuthenticationExtensionsClientOutputs
}

export function decodeBase64Url(value: string): ArrayBuffer {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/')
  const binary = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, '='))
  return Uint8Array.from(binary, (character) => character.charCodeAt(0)).buffer
}

export function encodeBase64Url(value: ArrayBuffer | ArrayBufferView): string {
  const bytes = value instanceof ArrayBuffer
    ? new Uint8Array(value)
    : new Uint8Array(value.buffer, value.byteOffset, value.byteLength)
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

export function preparePublicKeyOptions(options: PasskeyOptions): BrowserPublicKeyOptions {
  const source = structuredClone(options.publicKey ?? options) as ApiPublicKeyOptions
  const prepared = {
    ...source,
    challenge: decodeBase64Url(source.challenge),
  } as unknown as BrowserPublicKeyOptions
  if (source.user) prepared.user = { ...source.user, id: decodeBase64Url(source.user.id) }
  if (source.allowCredentials) {
    prepared.allowCredentials = source.allowCredentials.map((item) => ({ ...item, id: decodeBase64Url(item.id) }))
  }
  if (source.excludeCredentials) {
    prepared.excludeCredentials = source.excludeCredentials.map((item) => ({ ...item, id: decodeBase64Url(item.id) }))
  }
  return prepared
}

export async function createPasskey(options: PasskeyOptions): Promise<SerializedCredential> {
  ensureWebAuthnAvailable()
  const credential = await navigator.credentials.create({
    publicKey: preparePublicKeyOptions(options) as unknown as PublicKeyCredentialCreationOptions,
  })
  if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey 註冊已取消。')
  return serializeCredential(credential)
}

export async function getPasskey(options: PasskeyOptions): Promise<SerializedCredential> {
  ensureWebAuthnAvailable()
  const credential = await navigator.credentials.get({
    publicKey: preparePublicKeyOptions(options) as unknown as PublicKeyCredentialRequestOptions,
  })
  if (!(credential instanceof PublicKeyCredential)) throw new Error('Passkey 登入已取消。')
  return serializeCredential(credential)
}

function ensureWebAuthnAvailable(): void {
  if (typeof window === 'undefined' || !window.isSecureContext || !window.PublicKeyCredential) {
    throw new Error('目前網址或瀏覽器不支援 Passkey，請使用 HTTPS 與支援 WebAuthn 的瀏覽器。')
  }
}

function serializeCredential(credential: PublicKeyCredential): SerializedCredential {
  const response = credential.response
  const serializedResponse: Record<string, unknown> = {
    clientDataJSON: encodeBase64Url(response.clientDataJSON),
  }

  if ('attestationObject' in response) {
    const attestation = response as AuthenticatorAttestationResponse & { getTransports?: () => AuthenticatorTransport[] }
    serializedResponse.attestationObject = encodeBase64Url(attestation.attestationObject)
    serializedResponse.transports = attestation.getTransports?.() ?? []
  } else {
    const assertion = response as AuthenticatorAssertionResponse
    serializedResponse.authenticatorData = encodeBase64Url(assertion.authenticatorData)
    serializedResponse.signature = encodeBase64Url(assertion.signature)
    serializedResponse.userHandle = assertion.userHandle ? encodeBase64Url(assertion.userHandle) : null
  }

  return {
    id: credential.id,
    rawId: encodeBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment,
    response: serializedResponse,
    clientExtensionResults: credential.getClientExtensionResults(),
  }
}
