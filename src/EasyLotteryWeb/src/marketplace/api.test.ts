import { afterEach, describe, expect, it, vi } from "vitest";
import { installMarketplaceTemplate, loadMarketplace, publishMarketplaceCopy, type MarketplaceEntry } from "./api";
import { createPokeTemplate, type PokeTemplate } from "../pokebox/template";
import { createRouletteTemplate, type RouletteTemplate } from "../roulette/template";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

function poke(id: number, publicId: string, overrides: Partial<PokeTemplate> = {}): PokeTemplate {
  return { ...createPokeTemplate(), id, publicId, name: `Poke ${id}`, description: "Demo", updatedAt: "2026-10-01T00:00:00Z", ...overrides };
}

function roulette(id: number, publicId: string, overrides: Partial<RouletteTemplate> = {}): RouletteTemplate {
  return { ...createRouletteTemplate(), id, publicId, name: `Roulette ${id}`, description: "Demo", updatedAt: "2026-10-01T00:00:00Z", ...overrides };
}

describe("marketplace API", () => {
  it("lists only published built-ins and identifies the newest editable copy", async () => {
    const source = poke(1, "poke-source", { isBuiltIn: true, publicationStatus: 0 });
    const copy = poke(2, "poke-copy", { marketSourcePublicId: "POKE-SOURCE", updatedAt: "2026-10-02T00:00:00Z" });
    const unpublished = poke(3, "poke-draft", { isBuiltIn: true, publicationStatus: 1 });
    const rouletteSource = roulette(4, "roulette-source", { isBuiltIn: true, publicationStatus: 0 });
    const calls: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input), "https://example.test").pathname;
      calls.push(path);
      return new Response(JSON.stringify(path.includes("poke-") ? [source, copy, unpublished] : [rouletteSource]), {
        status: 200,
        headers: { "Content-Type": "application/json" }
      });
    }));

    await expect(loadMarketplace()).resolves.toEqual([
      expect.objectContaining({ kind: "pokebox", id: 1, installed: true, installedId: 2 }),
      expect.objectContaining({ kind: "roulette", id: 4, installed: false, installedId: null })
    ]);
    expect(calls).toEqual(["/api/poke-templates", "/api/roulette-templates"]);
  });

  it("duplicates first, then publishes only the new copy before returning its edit route", async () => {
    const calls: Array<{ path: string; method: string; body: unknown }> = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = new URL(String(input), "https://example.test").pathname;
      calls.push({ path, method: init?.method ?? "GET", body: init?.body ? JSON.parse(String(init.body)) : null });
      if (path.endsWith("/duplicate")) return new Response(JSON.stringify({ id: 27 }), { status: 200, headers: { "Content-Type": "application/json" } });
      return new Response(null, { status: 204 });
    }));
    const entry: MarketplaceEntry = {
      kind: "pokebox", id: 3, publicId: "source", name: "Source", description: "", installed: true,
      installedId: 12, updatedAt: "2026-10-01T00:00:00Z"
    };

    await expect(installMarketplaceTemplate(entry)).resolves.toBe("/pokebox?edit=27");
    expect(calls).toEqual([
      { path: "/api/poke-templates/3/duplicate", method: "POST", body: null },
      { path: "/api/poke-templates/27/publication-status", method: "PUT", body: { status: 0 } }
    ]);
  });

  it("does not publish or navigate when duplication fails", async () => {
    const fetch = vi.fn(async () => new Response(JSON.stringify({ error: "copy failed" }), {
      status: 503, headers: { "Content-Type": "application/json" }
    }));
    vi.stubGlobal("fetch", fetch);
    const entry: MarketplaceEntry = {
      kind: "roulette", id: 9, publicId: "source", name: "Source", description: "", installed: false,
      installedId: null, updatedAt: "2026-10-01T00:00:00Z"
    };

    await expect(installMarketplaceTemplate(entry)).rejects.toMatchObject({ status: 503 });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it("retains the new copy id after publication failure so retry does not duplicate again", async () => {
    const calls: string[] = [];
    let publicationAttempts = 0;
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = new URL(String(input), "https://example.test").pathname;
      calls.push(path);
      if (path.endsWith("/duplicate")) return new Response(JSON.stringify({ id: 31 }), { status: 200, headers: { "Content-Type": "application/json" } });
      publicationAttempts++;
      return publicationAttempts === 1
        ? new Response(JSON.stringify({ error: "temporary" }), { status: 503, headers: { "Content-Type": "application/json" } })
        : new Response(null, { status: 204 });
    }));
    const entry: MarketplaceEntry = {
      kind: "roulette", id: 9, publicId: "source", name: "Source", description: "", installed: false,
      installedId: null, updatedAt: "2026-10-01T00:00:00Z"
    };

    await expect(installMarketplaceTemplate(entry)).rejects.toMatchObject({ copyId: 31 });
    await expect(publishMarketplaceCopy(entry, 31)).resolves.toBe("/roulette?edit=31");
    expect(calls).toEqual([
      "/api/roulette-templates/9/duplicate",
      "/api/roulette-templates/31/publication-status",
      "/api/roulette-templates/31/publication-status"
    ]);
  });
});
