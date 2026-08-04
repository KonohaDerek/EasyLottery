(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
    const remoteConfigUrl = "/settings";
    const sessionTokenKey = "easy-lottery.session-token";
    let memoryFallback = "";
    let nextRemoteAttemptAt = 0;
    let sessionTokenPromise = null;
    let remoteEtag = "";

    function readJwtPayload(token) {
        try {
            const part = token.split(".")[1];
            if (!part) return null;
            const normalized = part.replace(/-/g, "+").replace(/_/g, "/");
            return JSON.parse(atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=")));
        } catch {
            return null;
        }
    }

    function logError(scope, error) {
        try {
            console.error(`[easyLotteryConfig] ${scope}`, error);
        } catch (logErrorFailure) {
            console.warn("[easyLotteryConfig] failed to emit console error", logErrorFailure);
        }
    }

    function bootstrapSessionTokenFromQuery() {
        try {
            const url = new URL(window.location.href);
            const hashParams = new URLSearchParams(url.hash.replace(/^#/, ""));
            const token = (url.searchParams.get("sessionToken") || hashParams.get("sessionToken") || "").trim();
            if (!token) {
                return;
            }

            window.sessionStorage.setItem(sessionTokenKey, token);
            url.searchParams.delete("sessionToken");
            hashParams.delete("sessionToken");
            url.hash = hashParams.toString() ? `#${hashParams.toString()}` : "";
            window.history.replaceState({}, document.title, `${url.pathname}${url.search}${url.hash}`);
        } catch (error) {
            logError("bootstrapSessionTokenFromQuery", error);
        }
    }

    function getSafeContent(content) {
        return content == null ? "" : content;
    }

    function cacheFallback(content) {
        memoryFallback = getSafeContent(content);

        try {
            window.localStorage.setItem(storageKey, memoryFallback);
        } catch (error) {
            logError("cacheFallback.localStorage.setItem", error);
        }

        return memoryFallback;
    }

    async function ensureSessionToken() {
        try {
            const cached = window.sessionStorage.getItem(sessionTokenKey);
            if (cached) {
                const payload = readJwtPayload(cached);
                const expired = !payload?.exp || payload.exp * 1000 <= Date.now() + 5_000;
                if (!expired) return cached;
                window.sessionStorage.removeItem(sessionTokenKey);
                if (payload?.token_use === "obs") {
                    throw new Error("OBS token 已過期，請從管理頁重新開啟 OBS URL。");
                }
            }
        } catch (error) {
            logError("ensureSessionToken.sessionStorage.getItem", error);
        }

        if (!sessionTokenPromise) {
            sessionTokenPromise = (async () => {
                const response = await fetch("/api/session-token", {
                    method: "GET",
                    cache: "no-store",
                });
                if (!response.ok) {
                    throw new Error(`Unable to obtain a session token (${response.status}).`);
                }

                const payload = await response.json();
                const token = (typeof payload === "string"
                    ? payload
                    : payload?.token ?? payload?.Token ?? payload?.sessionToken ?? payload?.SessionToken ?? "").toString().trim();
                if (!token) {
                    throw new Error("The session token endpoint returned an empty token.");
                }

                try {
                    window.sessionStorage.setItem(sessionTokenKey, token);
                } catch (error) {
                    logError("ensureSessionToken.sessionStorage.setItem", error);
                }

                return token;
            })().finally(() => {
                sessionTokenPromise = null;
            });
        }

        return sessionTokenPromise;
    }

    async function getSessionHeaders() {
        try {
            const token = await ensureSessionToken();
            return token ? { "X-EasyLottery-Session-Token": token } : {};
        } catch (error) {
            logError("getSessionHeaders", error);
            return {};
        }
    }

    bootstrapSessionTokenFromQuery();
    void ensureSessionToken().catch(error => logError("ensureSessionToken.bootstrap", error));

    async function read() {
        const now = Date.now();
        try {
            // The web host owns the YAML file. Do not let a stale per-browser
            // localStorage value override shared settings.
            if (now >= nextRemoteAttemptAt) {
                const response = await fetch(remoteConfigUrl, { cache: "no-store", headers: await getSessionHeaders() });
                if (response.ok) {
                    nextRemoteAttemptAt = 0;
                    remoteEtag = response.headers.get("ETag") || "";
                    return cacheFallback(await response.text());
                }

                // The standalone WASM dev server has no YAML API. Avoid generating
                // a 404 every overlay refresh while retaining the local fallback.
                nextRemoteAttemptAt = now + 30_000;
            }
        } catch (error) {
            logError("read.fetch", error);
            nextRemoteAttemptAt = now + 30_000;
        }

        try {
            return cacheFallback(window.localStorage.getItem(storageKey));
        } catch (error) {
            logError("read.localStorage.getItem", error);
        }

        return memoryFallback;
    }

    async function write(content) {
        const safeContent = getSafeContent(content);

        try {
            const headers = { "Content-Type": "text/yaml; charset=utf-8", ...(await getSessionHeaders()) };
            if (remoteEtag) {
                headers["If-Match"] = remoteEtag;
            }
            const response = await fetch(remoteConfigUrl, {
                method: "PUT",
                cache: "no-store",
                headers,
                body: safeContent
            });

            if (!response.ok) {
                throw new Error(`Unable to save shared YAML configuration (${response.status}).`);
            }

            // Only update the browser cache after the shared YAML was written
            // successfully so the browser never becomes the source of truth.
            remoteEtag = response.headers.get("ETag") || remoteEtag;
            cacheFallback(safeContent);
        } catch (error) {
            logError("write.fetch", error);
            throw error;
        }
    }

    window.easyLotteryConfig = {
        read,
        write,
        getSessionToken: async () => await ensureSessionToken(),
        hasSessionToken: () => {
            try {
                return !!window.sessionStorage.getItem(sessionTokenKey);
            } catch (error) {
                logError("hasSessionToken", error);
                return false;
            }
        },
    };
})();
