import { defineStore } from "pinia";
import { sessionTokenKey } from "../api/client";

export function getStoredToken(): string | null {
  if (typeof sessionStorage === "undefined") return null;
  return sessionStorage.getItem(sessionTokenKey);
}

export function isAdminSessionToken(token: string | null, now = Date.now()): boolean {
  if (!token) return false;

  try {
    const encodedPayload = token.split(".")[1];
    if (!encodedPayload) return false;
    const normalized = encodedPayload.replace(/-/g, "+").replace(/_/g, "/");
    const binary = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "="));
    const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes)) as { token_use?: unknown; exp?: unknown };
    return payload.token_use === "admin" && typeof payload.exp === "number" && payload.exp * 1000 > now;
  } catch {
    return false;
  }
}

export const useSessionStore = defineStore("session", {
  state: () => ({
    authenticated: isAdminSessionToken(getStoredToken())
  }),
  actions: {
    setToken(token: string) {
      const value = token.trim();
      if (!isAdminSessionToken(value)) throw new Error("登入回應沒有有效的管理員 session token。");
      sessionStorage.setItem(sessionTokenKey, value);
      this.authenticated = true;
    },
    clearToken() {
      sessionStorage.removeItem(sessionTokenKey);
      this.authenticated = false;
    },
    token() {
      return getStoredToken();
    }
  }
});
