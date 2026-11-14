import { afterEach, describe, expect, it, vi } from "vitest";
import { isShopEnabled, isShopFeaturePath, loadHubLinkSettings, saveHubLinkSettings, testHubLink } from "./api";
import { shopRouteNotFoundRedirect } from "./route-access";

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
  vi.restoreAllMocks();
});

describe("shop feature gate", () => {
  it("defaults to disabled when the server returns 404 or cannot be reached", async () => {
    await expect(isShopEnabled(async () => new Response(null, { status: 404 }))).resolves.toBe(false);
    await expect(isShopEnabled(async () => { throw new Error("offline"); })).resolves.toBe(false);
  });

  it("enables the shop only when the status endpoint explicitly says true", async () => {
    await expect(isShopEnabled(async () => Response.json({ enabled: true }))).resolves.toBe(true);
    await expect(isShopEnabled(async () => Response.json({ enabled: false }))).resolves.toBe(false);
  });

  it("recognizes public and admin shop paths", () => {
    expect(isShopFeaturePath("/shop")).toBe(true);
    expect(isShopFeaturePath("/shop/packs/1")).toBe(true);
    expect(isShopFeaturePath("/system/card-hub")).toBe(true);
    expect(isShopFeaturePath("/system/payment")).toBe(false);
  });

  it("routes disabled shop pages to the existing not-found route before auth", async () => {
    await expect(shopRouteNotFoundRedirect("/system/card-hub", "card-hub", {}, "", async () => false)).resolves.toEqual({
      name: "not-found",
      params: { pathMatch: ["system", "card-hub"] },
      query: {},
      hash: "",
      replace: true
    });
    await expect(shopRouteNotFoundRedirect("/shop", "not-found", {}, "", async () => false)).resolves.toBeNull();
    await expect(shopRouteNotFoundRedirect("/system/card-hub", "card-hub", {}, "", async () => true)).resolves.toBeNull();
  });
});

describe("Card Hub settings API", () => {
  it("loads only the write-only settings contract", async () => {
    const response = { hubBaseUrl: "https://hub.example.test", siteClientId: "site-1", hasClientSecret: true, status: "linked", tenantId: null, lastPublishAt: null };
    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(Response.json(response));

    await expect(loadHubLinkSettings()).resolves.toEqual(response);
    expect(globalThis.fetch).toHaveBeenCalledWith("/api/shop/hub-link", expect.objectContaining({ cache: "no-store" }));
  });

  it("sends credentials only in the save or test request body", async () => {
    const request = { hubBaseUrl: "https://hub.example.test", siteClientId: "site-1", clientSecret: "one-time-secret" };
    const response = { hubBaseUrl: request.hubBaseUrl, siteClientId: request.siteClientId, hasClientSecret: true, status: "linked", tenantId: null, lastPublishAt: null };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json(response));

    await saveHubLinkSettings(request);
    await testHubLink(request);

    expect(fetchMock).toHaveBeenNthCalledWith(1, "/api/shop/hub-link", expect.objectContaining({ method: "PUT", body: JSON.stringify(request) }));
    expect(fetchMock).toHaveBeenNthCalledWith(2, "/api/shop/hub-link/test", expect.objectContaining({ method: "POST", body: JSON.stringify(request) }));
  });

  it("preserves a disabled-tenant result for test-connection feedback", async () => {
    const request = { hubBaseUrl: "https://hub.example.test", siteClientId: "site-1", clientSecret: "one-time-secret" };
    const response = { hubBaseUrl: request.hubBaseUrl, siteClientId: request.siteClientId, hasClientSecret: true, status: "tenant_disabled", tenantId: "streamer-1", lastPublishAt: null };
    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(Response.json(response));

    await expect(testHubLink(request)).resolves.toEqual(response);
  });
});
