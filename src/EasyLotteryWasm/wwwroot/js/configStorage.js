(() => {
    const storageKey = "easy-lottery.config.yaml";

    async function read() {
        if (window.__TAURI__?.invoke) {
            const content = await window.__TAURI__.invoke("read_config_yaml");
            return content ?? "";
        }

        return window.localStorage.getItem(storageKey) ?? "";
    }

    async function write(content) {
        if (window.__TAURI__?.invoke) {
            await window.__TAURI__.invoke("write_config_yaml", { content: content ?? "" });
            return;
        }

        window.localStorage.setItem(storageKey, content ?? "");
    }

    window.easyLotteryConfig = {
        read,
        write,
    };
})();