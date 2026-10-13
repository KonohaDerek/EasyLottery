import { apiRequest } from "../api/client";
import type { PokeTemplate } from "../pokebox/template";
import type { RouletteTemplate } from "../roulette/template";

export type MarketplaceKind = "pokebox" | "roulette";

export interface MarketplaceEntry {
  kind: MarketplaceKind;
  id: number;
  publicId: string;
  name: string;
  description: string;
  installed: boolean;
  installedId: number | null;
  updatedAt: string;
}

export class MarketplacePublicationError extends Error {
  constructor(readonly copyId: number, readonly publicationError: unknown) {
    super(publicationError instanceof Error ? publicationError.message : "新副本發布失敗。");
    this.name = "MarketplacePublicationError";
  }
}

function templateCollection(entry: MarketplaceEntry): string {
  return entry.kind === "pokebox" ? "poke-templates" : "roulette-templates";
}

export function marketplaceEditorPath(entry: MarketplaceEntry, copyId: number): string {
  return `${entry.kind === "pokebox" ? "/pokebox" : "/roulette"}?edit=${copyId}`;
}

function latestCopy<T extends { isBuiltIn: boolean; marketSourcePublicId: string; id: number; updatedAt: string }>(
  templates: T[], sourcePublicId: string
): T | undefined {
  return templates
    .filter(template => !template.isBuiltIn && template.marketSourcePublicId.toLowerCase() === sourcePublicId.toLowerCase())
    .sort((left, right) => right.updatedAt.localeCompare(left.updatedAt))[0];
}

export async function loadMarketplace(): Promise<MarketplaceEntry[]> {
  const [pokes, roulettes] = await Promise.all([
    apiRequest<PokeTemplate[]>("/api/poke-templates"),
    apiRequest<RouletteTemplate[]>("/api/roulette-templates")
  ]);
  const entries: MarketplaceEntry[] = [
    ...pokes.data.filter(template => template.isBuiltIn && template.publicationStatus === 0).map(template => {
      const installed = latestCopy(pokes.data, template.publicId);
      return {
        kind: "pokebox" as const,
        id: template.id,
        publicId: template.publicId,
        name: template.name,
        description: template.description,
        installed: installed !== undefined,
        installedId: installed?.id ?? null,
        updatedAt: template.updatedAt
      };
    }),
    ...roulettes.data.filter(template => template.isBuiltIn && template.publicationStatus === 0).map(template => {
      const installed = latestCopy(roulettes.data, template.publicId);
      return {
        kind: "roulette" as const,
        id: template.id,
        publicId: template.publicId,
        name: template.name,
        description: template.description,
        installed: installed !== undefined,
        installedId: installed?.id ?? null,
        updatedAt: template.updatedAt
      };
    })
  ];
  return entries;
}

export async function installMarketplaceTemplate(entry: MarketplaceEntry): Promise<string> {
  if (!Number.isSafeInteger(entry.id) || entry.id <= 0) throw new Error("市集模板識別碼無效。");
  const collection = templateCollection(entry);
  const { data: copy } = await apiRequest<{ id: number }>(`/api/${collection}/${entry.id}/duplicate`, { method: "POST" });
  if (!copy || !Number.isSafeInteger(copy.id) || copy.id <= 0) throw new Error("市集沒有回傳有效的新活動副本。");

  try {
    return await publishMarketplaceCopy(entry, copy.id);
  } catch (cause) {
    throw new MarketplacePublicationError(copy.id, cause);
  }
}

export async function publishMarketplaceCopy(entry: MarketplaceEntry, copyId: number): Promise<string> {
  if (!Number.isSafeInteger(copyId) || copyId <= 0) throw new Error("市集副本識別碼無效。");
  const collection = templateCollection(entry);
  // Match the legacy market flow: publish only the newly created editable copy.
  await apiRequest<void>(`/api/${collection}/${copyId}/publication-status`, {
    method: "PUT",
    body: JSON.stringify({ status: 0 })
  });
  return marketplaceEditorPath(entry, copyId);
}
