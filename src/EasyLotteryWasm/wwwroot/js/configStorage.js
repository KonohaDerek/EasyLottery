(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
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
            const content = await tauriInvoke("read_config_yaml");
            memoryFallback = getSafeContent(content);
            return memoryFallback;
        }

        try {
            const content = window.localStorage.getItem(storageKey);
            memoryFallback = getSafeContent(content);
            return memoryFallback;
        } catch {
            return memoryFallback;
        }
    }

    async function write(content) {
        const safeContent = getSafeContent(content);
        memoryFallback = safeContent;

        const tauriInvoke = getTauriInvoker();
        if (tauriInvoke) {
            await tauriInvoke("write_config_yaml", { content: safeContent });
            return;
        }

        try {
            window.localStorage.setItem(storageKey, safeContent);
        } catch {
        }
    }

    window.easyLotteryConfig = {
        read,
        write,
    };
})();