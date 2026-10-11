import { getSessionToken, sessionTokenKey, setSessionTokenFallback } from "../api/client";

export function extractObsSessionToken(href: string): { token: string | null; cleanedUrl: string; foundToken: boolean } {
  const url = new URL(href);
  if (!url.pathname.startsWith("/obs/")) {
    return { token: null, cleanedUrl: `${url.pathname}${url.search}${url.hash}`, foundToken: false };
  }

  const query = new URLSearchParams(url.search);
  const fragment = new URLSearchParams(url.hash.slice(1));
  const token = (query.get("sessionToken") ?? fragment.get("sessionToken") ?? "").trim() || null;
  const foundToken = query.has("sessionToken") || fragment.has("sessionToken");
  query.delete("sessionToken");
  fragment.delete("sessionToken");
  url.search = query.toString();
  url.hash = fragment.toString();

  return { token, cleanedUrl: `${url.pathname}${url.search}${url.hash}`, foundToken };
}

export function readObsSessionToken(): string | null {
  return getSessionToken();
}

export function bootstrapObsSessionToken(): void {
  const result = extractObsSessionToken(window.location.href);
  if (!result.foundToken) return;
  if (result.token) {
    try {
      window.sessionStorage.setItem(sessionTokenKey, result.token);
      setSessionTokenFallback(null);
    } catch {
      setSessionTokenFallback(result.token);
      // The in-memory token still supports this page load; never leave it in the URL.
    }
  }
  try {
    window.history.replaceState(window.history.state, document.title, result.cleanedUrl);
  } catch {
    // Keep application startup alive if the host disallows history updates.
  }
}
