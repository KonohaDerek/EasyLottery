import { defineStore } from "pinia";
import { sessionTokenKey } from "../api/client";

export function getStoredToken(): string | null {
  if (typeof sessionStorage === "undefined") return null;
  return sessionStorage.getItem(sessionTokenKey);
}

export const useSessionStore = defineStore("session", {
  state: () => ({
    authenticated: Boolean(getStoredToken())
  }),
  actions: {
    setToken(token: string) {
      const value = token.trim();
      if (!value) throw new Error("登入回應沒有有效的 session token。");
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
