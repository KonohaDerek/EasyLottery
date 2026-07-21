(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
    const remoteConfigUrl = "/easy-lottery-config.yaml";
    let memoryFallback = "";

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

    async function read() {
        try {
            // The web host owns the YAML file. Do not let a stale per-browser
            // localStorage value override shared settings.
            const response = await fetch(remoteConfigUrl, { cache: "no-store" });
            if (response.ok) {
                return cacheFallback(await response.text());
            }
        } catch {
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
                headers: {
                    "Content-Type": "text/yaml; charset=utf-8"
                },
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
    };
})();
