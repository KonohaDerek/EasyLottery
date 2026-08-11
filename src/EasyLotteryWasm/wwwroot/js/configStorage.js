(() => {
    if (window.easyLotteryConfig) {
        return;
    }

    const storageKey = "easy-lottery.config.yaml";
    const remoteConfigUrl = "/settings";
    const sessionTokenKey = "easy-lottery.session-token";
    let memoryFallback = "";
    let nextRemoteAttemptAt = 0;
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

    function logSessionError(scope, error) {
        const message = error?.message ?? "";
        if (/Unable to obtain a session token \((401|403|429)\)\./.test(message)
            || message === "Admin session temporarily unavailable; retry later."
            || message === "Admin login required.") {
            console.warn(`[easyLotteryConfig] ${scope}`, error);
            return;
        }

        logError(scope, error);
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
                    throw new Error("OBS token 已過期，請從管理頁重新開啟測試 OBS。");
                }
            }
        } catch (error) {
            logError("ensureSessionToken.sessionStorage.getItem", error);
        }

        throw new Error("Admin login required.");
    }

    async function getSessionHeaders() {
        try {
            const token = await ensureSessionToken();
            return token ? { "X-EasyLottery-Session-Token": token } : {};
        } catch (error) {
            logSessionError("getSessionHeaders", error);
            return {};
        }
    }

    bootstrapSessionTokenFromQuery();

    function base64UrlToBytes(value) {
        const normalized = String(value || "").replace(/-/g, "+").replace(/_/g, "/");
        const binary = atob(normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "="));
        return Uint8Array.from(binary, character => character.charCodeAt(0));
    }

    function bytesToBase64Url(value) {
        const bytes = new Uint8Array(value);
        let binary = "";
        for (const byte of bytes) binary += String.fromCharCode(byte);
        return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    }

    function clonePublicKeyOptions(options) {
        const publicKey = structuredClone(options?.publicKey ?? options);
        publicKey.challenge = base64UrlToBytes(publicKey.challenge);
        if (publicKey.user?.id) publicKey.user.id = base64UrlToBytes(publicKey.user.id);
        for (const item of [...(publicKey.allowCredentials || []), ...(publicKey.excludeCredentials || [])]) {
            item.id = base64UrlToBytes(item.id);
        }
        return publicKey;
    }

    function serializeCredential(credential) {
        const response = credential.response;
        const result = {
            id: credential.id,
            rawId: bytesToBase64Url(credential.rawId),
            type: credential.type,
            authenticatorAttachment: credential.authenticatorAttachment || null,
            response: {
                clientDataJSON: bytesToBase64Url(response.clientDataJSON)
            }
        };
        if (response instanceof AuthenticatorAttestationResponse) {
            result.response.attestationObject = bytesToBase64Url(response.attestationObject);
            result.response.transports = response.getTransports?.() || [];
        } else {
            result.response.authenticatorData = bytesToBase64Url(response.authenticatorData);
            result.response.signature = bytesToBase64Url(response.signature);
            result.response.userHandle = response.userHandle ? bytesToBase64Url(response.userHandle) : null;
        }
        result.clientExtensionResults = credential.getClientExtensionResults?.() || {};
        return result;
    }

    async function createPasskey(options) {
        if (!window.isSecureContext || !window.PublicKeyCredential) {
            throw new Error("目前網址或瀏覽器不支援 Passkey，請使用 HTTPS 與支援 WebAuthn 的瀏覽器。");
        }
        const credential = await navigator.credentials.create({ publicKey: clonePublicKeyOptions(options) });
        if (!credential) throw new Error("Passkey 註冊已取消。");
        return serializeCredential(credential);
    }

    async function getPasskey(options) {
        if (!window.isSecureContext || !window.PublicKeyCredential) {
            throw new Error("目前網址或瀏覽器不支援 Passkey，請使用 HTTPS 與支援 WebAuthn 的瀏覽器。");
        }
        const credential = await navigator.credentials.get({ publicKey: clonePublicKeyOptions(options) });
        if (!credential) throw new Error("Passkey 登入已取消。");
        return serializeCredential(credential);
    }

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
        setSessionToken: token => {
            const value = String(token || "").trim();
            if (!value) throw new Error("Cannot store an empty session token.");
            window.sessionStorage.setItem(sessionTokenKey, value);
        },
        clearSessionToken: () => window.sessionStorage.removeItem(sessionTokenKey),
        createPasskey,
        getPasskey,
        hasSessionToken: () => {
            try {
                const token = window.sessionStorage.getItem(sessionTokenKey);
                const payload = readJwtPayload(token || "");
                if (!token || !payload?.exp || payload.exp * 1000 <= Date.now() + 5_000) {
                    window.sessionStorage.removeItem(sessionTokenKey);
                    return false;
                }
                return true;
            } catch (error) {
                logError("hasSessionToken", error);
                return false;
            }
        },
    };
})();
