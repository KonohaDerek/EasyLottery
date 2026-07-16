(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
    const remoteConfigUrl = "http://localhost:18930/easy-lottery-config.yaml";
    let memoryFallback = "";

    function getSafeContent(content) {
        return content == null ? "" : content;
    }

    function getTauriInvoker() {
        if (window.__TAURI__ && typeof window.__TAURI__.invoke === "function") {
            return window.__TAURI__.invoke.bind(window.__TAURI__);
        }

        return null;
    }

    async function read() {
        const tauriInvoke = getTauriInvoker();
        if (tauriInvoke) {
            try {
                const content = await tauriInvoke("read_config_yaml");
                memoryFallback = getSafeContent(content);
                return memoryFallback;
            } catch {
            }
        }

        try {
            const content = window.localStorage.getItem(storageKey);
            memoryFallback = getSafeContent(content);
            if (memoryFallback) {
                return memoryFallback;
            }
        } catch {
        }

        try {
            const response = await fetch(remoteConfigUrl, { cache: "no-store", mode: "cors" });
            if (response.ok) {
                const content = await response.text();
                memoryFallback = getSafeContent(content);
                return memoryFallback;
            }
        } catch {
        }

        return memoryFallback;
    }

    async function write(content) {
        const safeContent = getSafeContent(content);
        memoryFallback = safeContent;

        try {
            window.localStorage.setItem(storageKey, safeContent);
        } catch {
        }

        const tauriInvoke = getTauriInvoker();
        if (tauriInvoke) {
            await tauriInvoke("write_config_yaml", { content: safeContent });
            return;
        }

        try {
            await fetch(remoteConfigUrl, {
                method: "PUT",
                cache: "no-store",
                mode: "cors",
                headers: {
                    "Content-Type": "text/yaml; charset=utf-8"
                },
                body: safeContent
            });
        } catch {
        }
    }

    window.easyLotteryConfig = {
        read,
        write,
    };
})();
