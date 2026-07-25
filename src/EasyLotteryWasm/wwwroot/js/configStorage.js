(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
    const remoteConfigUrl = "/settings";
    const adminTokenKey = "easy-lottery.admin-token";
    let memoryFallback = "";
    let nextRemoteAttemptAt = 0;
    let authorizationRequired = false;

    function logError(scope, error) {
        try {
            console.error(`[easyLotteryConfig] ${scope}`, error);
        } catch (logErrorFailure) {
            console.warn("[easyLotteryConfig] failed to emit console error", logErrorFailure);
        }
    }

    function bootstrapAdminTokenFromQuery() {
        try {
            const url = new URL(window.location.href);
            const token = (url.searchParams.get("adminToken") || "").trim();
            if (!token) {
                return;
            }

            window.sessionStorage.setItem(adminTokenKey, token);
            url.searchParams.delete("adminToken");
            window.history.replaceState({}, document.title, `${url.pathname}${url.search}${url.hash}`);
        } catch (error) {
            logError("bootstrapAdminTokenFromQuery", error);
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

    function getAdminHeaders() {
        try {
            const token = window.sessionStorage.getItem(adminTokenKey);
            return token ? { "X-EasyLottery-Admin-Token": token } : {};
        } catch (error) {
            logError("getAdminHeaders", error);
            return {};
        }
    }

    bootstrapAdminTokenFromQuery();

    async function read() {
        const now = Date.now();
        try {
            // The web host owns the YAML file. Do not let a stale per-browser
            // localStorage value override shared settings.
            if (now >= nextRemoteAttemptAt) {
                const response = await fetch(remoteConfigUrl, { cache: "no-store", headers: getAdminHeaders() });
                if (response.ok) {
                    nextRemoteAttemptAt = 0;
                    authorizationRequired = false;
                    return cacheFallback(await response.text());
                }

                // A reachable web host explicitly rejected the request. Preserve this
                // distinction from an offline standalone WASM server so the UI can
                // guide the user instead of displaying an empty fallback as settings.
                authorizationRequired = response.status === 401;

                // The standalone WASM dev server has no YAML API. Avoid generating
                // a 404 every overlay refresh while retaining the local fallback.
                nextRemoteAttemptAt = now + 30_000;
            }
        } catch (error) {
            logError("read.fetch", error);
            authorizationRequired = false;
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

        // Keep an offline copy in case the web host is temporarily unavailable.
        cacheFallback(safeContent);

        try {
            const response = await fetch(remoteConfigUrl, {
                method: "PUT",
                cache: "no-store",
                headers: { "Content-Type": "text/yaml; charset=utf-8", ...getAdminHeaders() },
                body: safeContent
            });

            if (!response.ok) {
                authorizationRequired = response.status === 401;
                throw new Error(`Unable to save shared YAML configuration (${response.status}).`);
            }
            authorizationRequired = false;
        } catch (error) {
            logError("write.fetch", error);
            // The browser cache above is the offline fallback.
        }
    }

    window.easyLotteryConfig = {
        read,
        write,
        setAdminToken: (token) => window.sessionStorage.setItem(adminTokenKey, token || ""),
        getAdminToken: () => window.sessionStorage.getItem(adminTokenKey) || "",
        requiresAdminToken: () => authorizationRequired,
    };
})();
