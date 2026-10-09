export type PasskeyFlow = "login" | "register";

export interface PasskeyOptionsResponse {
  flow: PasskeyFlow;
  options: Record<string, unknown>;
}

export interface PasskeyTokenResponse {
  token: string;
  expiresAtUtc: string;
}

export class PasskeyRequestError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code: string
  ) {
    super(message);
    this.name = "PasskeyRequestError";
  }
}

function base64UrlToBytes(value: unknown): Uint8Array {
  const normalized = String(value ?? "").replace(/-/g, "+").replace(/_/g, "/");
  const binary = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "="));
  return Uint8Array.from(binary, character => character.charCodeAt(0));
}

function bytesToBase64Url(value: ArrayBuffer | ArrayBufferView): string {
  const bytes = value instanceof ArrayBuffer
    ? new Uint8Array(value)
    : new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export function clonePublicKeyOptions(options: Record<string, unknown>): Record<string, unknown> {
  const publicKey = structuredClone((options.publicKey ?? options) as Record<string, unknown>);
  publicKey.challenge = base64UrlToBytes(publicKey.challenge);
  const user = publicKey.user as { id?: unknown } | undefined;
  if (user?.id) user.id = base64UrlToBytes(user.id);
  for (const item of [
    ...((publicKey.allowCredentials as Array<{ id: unknown }> | undefined) ?? []),
    ...((publicKey.excludeCredentials as Array<{ id: unknown }> | undefined) ?? [])
  ]) {
    item.id = base64UrlToBytes(item.id);
  }
  return publicKey;
}

export function serializeCredential(credential: PublicKeyCredential): Record<string, unknown> {
  const response = credential.response as unknown as {
    clientDataJSON: ArrayBuffer;
    attestationObject?: ArrayBuffer;
    authenticatorData?: ArrayBuffer;
    signature?: ArrayBuffer;
    userHandle?: ArrayBuffer | null;
    getTransports?: () => string[];
  };
  const result: Record<string, unknown> = {
    id: credential.id,
    rawId: bytesToBase64Url(credential.rawId),
    type: credential.type,
    authenticatorAttachment: credential.authenticatorAttachment ?? null,
    response: {
      clientDataJSON: bytesToBase64Url(response.clientDataJSON)
    },
    clientExtensionResults: credential.getClientExtensionResults?.() ?? {}
  };

  const serializedResponse = result.response as Record<string, unknown>;
  if (response.attestationObject) {
    serializedResponse.attestationObject = bytesToBase64Url(response.attestationObject);
    serializedResponse.transports = response.getTransports?.() ?? [];
  } else {
    serializedResponse.authenticatorData = bytesToBase64Url(response.authenticatorData!);
    serializedResponse.signature = bytesToBase64Url(response.signature!);
    serializedResponse.userHandle = response.userHandle ? bytesToBase64Url(response.userHandle) : null;
  }

  return result;
}

async function request<T>(path: string, body: Record<string, unknown>): Promise<T> {
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });
  const payload = await response.json().catch(() => null) as { error?: string; code?: string } | T | null;
  if (!response.ok) {
    const errorPayload = payload as { error?: string; code?: string } | null;
    throw new PasskeyRequestError(
      errorPayload?.error ?? `Passkey API 回傳 HTTP ${response.status}。`,
      response.status,
      errorPayload?.code ?? "passkey_request_failed"
    );
  }
  return payload as T;
}

async function authorizedRequest<T>(path: string, method: string, body?: Record<string, unknown>): Promise<T> {
  const token = sessionStorage.getItem("easy-lottery.session-token");
  const response = await fetch(path, {
    method,
    headers: {
      ...(body ? { "Content-Type": "application/json" } : {}),
      ...(token ? { "X-EasyLottery-Session-Token": token } : {})
    },
    body: body ? JSON.stringify(body) : undefined
  });
  const payload = await response.json().catch(() => null) as { error?: string; code?: string } | T | null;
  if (!response.ok) {
    const errorPayload = payload as { error?: string; code?: string } | null;
    throw new PasskeyRequestError(
      errorPayload?.error ?? `Passkey API 回傳 HTTP ${response.status}。`,
      response.status,
      errorPayload?.code ?? "passkey_request_failed"
    );
  }
  return payload as T;
}

async function completeFlow(email: string, flow: PasskeyFlow): Promise<PasskeyTokenResponse> {
  if (!window.isSecureContext || !window.PublicKeyCredential || !navigator.credentials) {
    throw new Error("目前網址或瀏覽器不支援 Passkey，請使用 HTTPS 與支援 WebAuthn 的瀏覽器。");
  }

  const begin = await request<PasskeyOptionsResponse>("/api/auth/passkey/options", { email, flow });
  const publicKey = clonePublicKeyOptions(begin.options) as unknown as PublicKeyCredentialCreationOptions & PublicKeyCredentialRequestOptions;
  const credential = flow === "register"
    ? await navigator.credentials.create({ publicKey: publicKey as PublicKeyCredentialCreationOptions })
    : await navigator.credentials.get({ publicKey: publicKey as PublicKeyCredentialRequestOptions });
  if (!credential || !(credential instanceof PublicKeyCredential)) {
    throw new Error(flow === "register" ? "Passkey 註冊已取消。" : "Passkey 登入已取消。");
  }

  return request<PasskeyTokenResponse>("/api/auth/passkey/verify", {
    email,
    flow,
    credential: serializeCredential(credential)
  });
}

export async function loginWithPasskey(email: string): Promise<PasskeyTokenResponse> {
  try {
    return await completeFlow(email, "login");
  } catch (cause) {
    if (cause instanceof PasskeyRequestError && cause.code === "registration_required") {
      return completeFlow(email, "register");
    }
    throw cause;
  }
}

export interface PasskeyDevice {
  id: string;
  name: string;
  createdAtUtc: string;
}

export async function listPasskeys(): Promise<PasskeyDevice[]> {
  return authorizedRequest<PasskeyDevice[]>("/api/admin/passkeys", "GET");
}

export async function addPasskey(name: string): Promise<PasskeyDevice> {
  const begin = await authorizedRequest<PasskeyOptionsResponse>("/api/admin/passkeys/options", "POST", { name });
  if (!window.isSecureContext || !window.PublicKeyCredential || !navigator.credentials) {
    throw new Error("目前網址或瀏覽器不支援 Passkey，請使用 HTTPS 與支援 WebAuthn 的瀏覽器。");
  }
  const publicKey = clonePublicKeyOptions(begin.options) as unknown as PublicKeyCredentialCreationOptions;
  const credential = await navigator.credentials.create({ publicKey });
  if (!credential || !(credential instanceof PublicKeyCredential)) throw new Error("Passkey 註冊已取消。");
  return authorizedRequest<PasskeyDevice>("/api/admin/passkeys/verify", "POST", { credential: serializeCredential(credential) });
}

export async function removePasskey(id: string): Promise<void> {
  await authorizedRequest<null>(`/api/admin/passkeys/${encodeURIComponent(id)}`, "DELETE");
}
