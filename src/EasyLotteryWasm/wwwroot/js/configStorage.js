(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
    const remoteConfigUrl = "/settings";
    const adminTokenKey = "easy-lottery.admin-token";
    let memoryFallback = "";
    let nextRemoteAttemptAt = 0;

    function getSafeContent(content) {
        return content == null ? "" : content;
    }

    function cacheFallback(content) {
        memoryFallback = getSafeContent(content);

        try {
            window.localStorage.setItem(storageKey, memoryFallback);
        } catch {
        }

        return memoryFallback;
    }

    function getAdminHeaders() {
        try { const token = window.sessionStorage.getItem(adminTokenKey); return token ? { "X-EasyLottery-Admin-Token": token } : {}; } catch { return {}; }
    }

    async function read() {
        const now = Date.now();
        try {
            // The web host owns the YAML file. Do not let a stale per-browser
            // localStorage value override shared settings.
            if (now >= nextRemoteAttemptAt) {
                const response = await fetch(remoteConfigUrl, { cache: "no-store", headers: getAdminHeaders() });
                if (response.ok) {
                    nextRemoteAttemptAt = 0;
                    return cacheFallback(await response.text());
                }

                // The standalone WASM dev server has no YAML API. Avoid generating
                // a 404 every overlay refresh while retaining the local fallback.
                nextRemoteAttemptAt = now + 30_000;
            }
        } catch {
            nextRemoteAttemptAt = now + 30_000;
        }

        try {
            return cacheFallback(window.localStorage.getItem(storageKey));
        } catch {
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
                throw new Error(`Unable to save shared YAML configuration (${response.status}).`);
            }
        } catch {
            // The browser cache above is the offline fallback.
        }
    }

    window.easyLotteryConfig = {
        read,
        write,
        setAdminToken: (token) => window.sessionStorage.setItem(adminTokenKey, token || ""),
    };
})();
