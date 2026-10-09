export const sessionTokenKey = "easy-lottery.session-token";

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly code = "api_request_failed",
    readonly details: unknown = null
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export interface ApiResponse<T> {
  data: T;
  etag: string;
}

function token(): string | null {
  if (typeof sessionStorage === "undefined") return null;
  return sessionStorage.getItem(sessionTokenKey);
}

async function parsePayload(response: Response): Promise<unknown> {
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("json")) return response.json().catch(() => null);
  return response.text().catch(() => "");
}

function errorFromResponse(response: Response, payload: unknown): ApiError {
  const record = payload && typeof payload === "object" ? payload as Record<string, unknown> : null;
  const message = typeof record?.error === "string"
    ? record.error
    : `API 回傳 HTTP ${response.status}。`;
  const code = typeof record?.code === "string" ? record.code : "api_request_failed";
  return new ApiError(message, response.status, code, payload);
}

export async function apiRequest<T>(path: string, options: RequestInit = {}): Promise<ApiResponse<T>> {
  const headers = new Headers(options.headers);
  const value = token();
  if (value) headers.set("X-EasyLottery-Session-Token", value);
  if (options.body && !headers.has("Content-Type") && !(options.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, { ...options, headers });
  const payload = await parsePayload(response);
  if (!response.ok) throw errorFromResponse(response, payload);
  return { data: payload as T, etag: response.headers.get("etag") ?? "" };
}

export async function apiText(path: string, options: RequestInit = {}): Promise<ApiResponse<string>> {
  const headers = new Headers(options.headers);
  const value = token();
  if (value) headers.set("X-EasyLottery-Session-Token", value);
  if (options.body && !headers.has("Content-Type")) headers.set("Content-Type", "text/yaml");
  const response = await fetch(path, { ...options, headers });
  const payload = await response.text().catch(() => "");
  if (!response.ok) throw errorFromResponse(response, payload);
  return { data: payload, etag: response.headers.get("etag") ?? "" };
}
