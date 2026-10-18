import { expect, test } from "./fixtures.mjs";
import { createHash, createPrivateKey, createPublicKey, sign, verify } from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

async function isolatePasskeyState() {
  if (!process.env.E2E_STORAGE_DIRECTORY) throw new Error("E2E_STORAGE_DIRECTORY must point to the dedicated Passkey test storage directory.");
  const tempRoot = path.resolve(process.env.RUNNER_TEMP ?? os.tmpdir());
  const storageDirectory = path.resolve(process.env.E2E_STORAGE_DIRECTORY);
  if (path.dirname(storageDirectory) !== tempRoot || !path.basename(storageDirectory).startsWith("easy-lottery-e2e-")) {
    throw new Error("Passkey E2E cleanup is limited to an easy-lottery-e2e-* directory directly under RUNNER_TEMP.");
  }

  const rootInfo = await fs.lstat(tempRoot);
  const directoryInfo = await fs.lstat(storageDirectory);
  if (!rootInfo.isDirectory() || rootInfo.isSymbolicLink() || !directoryInfo.isDirectory() || directoryInfo.isSymbolicLink()) {
    throw new Error("Passkey E2E storage and temp root must be real directories, not symbolic links.");
  }

  const statePath = path.join(storageDirectory, "admin-passkey.json");
  try {
    const stateInfo = await fs.lstat(statePath);
    if (!stateInfo.isFile() || stateInfo.isSymbolicLink()) throw new Error("Passkey state must be a regular file.");
    await fs.unlink(statePath);
  } catch (error) {
    if (error?.code !== "ENOENT") throw error;
  }
}

async function rejectMutatedAssertion(page, privateKey, mutate) {
  const verifyPath = "**/api/auth/passkey/verify";
  await page.route(verifyPath, async route => {
    const body = route.request().postDataJSON();
    expect(body.flow).toBe("login");
    const clientDataJSON = Buffer.from(body.credential.response.clientDataJSON, "base64url");
    const clientData = JSON.parse(clientDataJSON.toString("utf8"));
    const authenticatorData = Buffer.from(body.credential.response.authenticatorData, "base64url");
    mutate(clientData, authenticatorData);

    const updatedClientData = Buffer.from(JSON.stringify(clientData));
    const signedData = Buffer.concat([
      authenticatorData,
      createHash("sha256").update(updatedClientData).digest()
    ]);
    const isEd25519 = privateKey.asymmetricKeyType === "ed25519";
    const signature = isEd25519
      ? sign(null, signedData, privateKey)
      : sign("sha256", signedData, {
          key: privateKey,
          dsaEncoding: "der"
        });
    const publicKey = createPublicKey(privateKey);
    expect(verify(isEd25519 ? null : "sha256", signedData, publicKey, signature)).toBe(true);

    body.credential.response.clientDataJSON = updatedClientData.toString("base64url");
    body.credential.response.authenticatorData = authenticatorData.toString("base64url");
    body.credential.response.signature = signature.toString("base64url");
    await route.continue({ postData: JSON.stringify(body) });
  });

  const responsePromise = page.waitForResponse(response =>
    new URL(response.url()).pathname === "/api/auth/passkey/verify" && response.request().method() === "POST");
  await page.locator("#admin-email").fill("admin@example.com");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  const response = await responsePromise;
  expect(response.status()).toBe(401);
  expect((await response.json()).code).toBe("passkey_verification_failed");
  await expect(page.getByRole("alert")).toBeVisible();
  expect(await page.evaluate(() => sessionStorage.getItem("easy-lottery.session-token"))).toBeNull();
  await page.unroute(verifyPath);
}

test("blank login email stays client-side", async ({ page }) => {
  let optionsCalls = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/auth/passkey/options") {
      optionsCalls++;
    }
  });

  await page.goto("/login", { waitUntil: "networkidle" });
  await expect(page.locator("#admin-email")).toHaveValue("");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(page.getByRole("alert")).toContainText("請輸入有效的管理員 email");
  expect(optionsCalls).toBe(0);
});

test("malformed login email stays client-side", async ({ page }) => {
  let optionsCalls = 0;
  page.on("request", request => {
    if (new URL(request.url()).pathname === "/api/auth/passkey/options") {
      optionsCalls++;
    }
  });

  await page.goto("/login", { waitUntil: "networkidle" });
  await page.locator("#admin-email").fill("not-an-email");
  await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
  await expect(page.getByRole("alert")).toContainText("請輸入有效的管理員 email");
  expect(optionsCalls).toBe(0);
});

test("admin can register and use a Passkey", async ({ browser, baseURL }) => {
  await isolatePasskeyState();
  const origin = new URL(baseURL ?? "http://127.0.0.1:18930").origin.replace("127.0.0.1", "localhost");
  const context = await browser.newContext();
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  const ceremonies = [];
  page.on("response", response => {
    const pathname = new URL(response.url()).pathname;
    if (["/api/auth/passkey/options", "/api/auth/passkey/verify"].includes(pathname)) {
      ceremonies.push({ pathname, status: response.status() });
    }
  });
  await cdp.send("WebAuthn.enable");
  const { authenticatorId } = await cdp.send("WebAuthn.addVirtualAuthenticator", {
    options: {
      protocol: "ctap2",
      transport: "internal",
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true
    }
  });

  try {
    await page.goto(`${origin}/login`, { waitUntil: "networkidle" });
    await page.locator("#admin-email").fill("admin@example.com");
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(page.getByText("管理員 Passkey 管理", { exact: true })).toBeVisible();
    await expect(page).toHaveURL(`${origin}/system/access`);

    const { credentials } = await cdp.send("WebAuthn.getCredentials", { authenticatorId });
    expect(credentials).toHaveLength(1);
    const privateKey = createPrivateKey({
      key: Buffer.from(credentials[0].privateKey, "base64"),
      format: "der",
      type: "pkcs8"
    });
    expect(["ec", "ed25519"]).toContain(privateKey.asymmetricKeyType);

    await page.getByRole("button", { name: "登出並清除管理員工作階段" }).click();
    await expect(page).toHaveURL(`${origin}/login`);
    expect(await page.evaluate(() => sessionStorage.getItem("easy-lottery.session-token"))).toBeNull();
    await page.locator("#admin-email").fill("admin@example.com");
    await page.getByRole("button", { name: "使用 Passkey 登入" }).click();
    await expect(page.getByText("管理員 Passkey 管理", { exact: true })).toBeVisible();
    await expect(page).toHaveURL(`${origin}/system/access`);
    expect(ceremonies.slice(0, 5)).toEqual([
      { pathname: "/api/auth/passkey/options", status: 409 },
      { pathname: "/api/auth/passkey/options", status: 200 },
      { pathname: "/api/auth/passkey/verify", status: 200 },
      { pathname: "/api/auth/passkey/options", status: 200 },
      { pathname: "/api/auth/passkey/verify", status: 200 }
    ]);

    await page.goto(`${origin}/system/access`, { waitUntil: "networkidle" });
    await expect(page.getByText("管理員 Passkey 管理", { exact: true })).toBeVisible();
    const rows = page.locator("tbody tr");
    await expect(rows).toHaveCount(1);

    for (const [path, heading] of [
      ["/system/payment", "支付配置"],
      ["/system/youtube-login", "YouTube 直播設定"],
      ["/system/audit", "審計紀錄"]
    ]) {
      await page.goto(`${origin}${path}`, { waitUntil: "networkidle" });
      await expect(page.getByRole("heading", { name: heading, exact: true })).toHaveCount(1);
    }

    await page.goto(`${origin}/system/access`, { waitUntil: "networkidle" });
    await expect(rows).toHaveCount(1);

    const { authenticatorId: backupAuthenticatorId } = await cdp.send("WebAuthn.addVirtualAuthenticator", {
      options: {
        protocol: "ctap2",
        transport: "usb",
        hasResidentKey: true,
        hasUserVerification: true,
        isUserVerified: true
      }
    });
    await page.locator("#passkey-name").fill("Backup Authenticator");
    await page.getByRole("button", { name: "新增 Passkey" }).click();
    await expect(rows).toHaveCount(2);
    await expect(page.getByText("Backup Authenticator")).toBeVisible();

    page.once("dialog", dialog => dialog.accept());
    await rows.last().getByRole("button", { name: "移除" }).click();
    await expect(rows).toHaveCount(1);
    await cdp.send("WebAuthn.removeVirtualAuthenticator", { authenticatorId: backupAuthenticatorId });

    await page.getByRole("button", { name: "登出並清除管理員工作階段" }).click();
    await expect(page).toHaveURL(`${origin}/login`);
    const invalidAssertions = [
      (clientData) => {
        clientData.origin = "https://not-allowed.example";
      },
      (_clientData, authenticatorData) => {
        createHash("sha256").update("not-allowed.example").digest().copy(authenticatorData, 0);
      },
      (_clientData, authenticatorData) => {
        authenticatorData[32] &= ~0x04;
      }
    ];
    for (const mutate of invalidAssertions) await rejectMutatedAssertion(page, privateKey, mutate);
    expect(ceremonies.slice(-6)).toEqual([
      { pathname: "/api/auth/passkey/options", status: 200 },
      { pathname: "/api/auth/passkey/verify", status: 401 },
      { pathname: "/api/auth/passkey/options", status: 200 },
      { pathname: "/api/auth/passkey/verify", status: 401 },
      { pathname: "/api/auth/passkey/options", status: 200 },
      { pathname: "/api/auth/passkey/verify", status: 401 }
    ]);
  } finally {
    await context.close();
  }
});
