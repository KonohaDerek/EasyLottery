import { defineStore } from "pinia";

const sessionTokenKey = "easy-lottery.session-token";

export const useSessionStore = defineStore("session", {
  state: () => ({
    authenticated: false
  }),
  actions: {
    setToken(token: string) {
      const value = token.trim();
      if (!value) throw new Error("登入回應沒有有效的 session token。");
      sessionStorage.setItem(sessionTokenKey, value);
      this.authenticated = true;
    }
  }
});
