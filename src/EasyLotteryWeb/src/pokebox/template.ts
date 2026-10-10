export type PokeMode = 0 | 1; // Random = 0, Manual = 1.
export type PokeAnimation = 0 | 1 | 2 | 3 | 4; // Burst, Smoke, Flash, Bounce, Flip.
export type PokePublicationStatus = 0 | 1; // Published = 0, Draft = 1.

export interface PokeCell {
  id: number;
  templateId: number;
  index: number;
  title: string;
  subTitle: string;
  imageUrl: string;
  revealedImageUrl: string;
  revealedColor: string;
  isRevealed: boolean;
  revealedAt: string | null;
}

export interface PokeTemplate {
  id: number;
  publicId: string;
  marketSourcePublicId: string;
  name: string;
  description: string;
  gridRows: number;
  gridColumns: number;
  mode: PokeMode;
  allowRePoking: boolean;
  maxPokeCount: number;
  backgroundImageUrl: string;
  fontFamily: string;
  congratulationMessage: string;
  overlayWidth: number;
  overlayHeight: number;
  animation: PokeAnimation;
  animationDurationMs: number;
  resultDisplayDurationSeconds: number;
  pokeSoundUrl: string;
  openSoundUrl: string;
  isBuiltIn: boolean;
  publicationStatus: PokePublicationStatus;
  createdAt: string;
  updatedAt: string;
  cells: PokeCell[];
}

const emptyGuid = "00000000-0000-0000-0000-000000000000";

export function createPokeCell(index: number): PokeCell {
  return {
    id: 0,
    templateId: 0,
    index,
    title: `格子 ${index + 1}`,
    subTitle: "",
    imageUrl: "",
    revealedImageUrl: "",
    revealedColor: "#cccccc",
    isRevealed: false,
    revealedAt: null
  };
}

export function createPokeTemplate(): PokeTemplate {
  return resizePokeGrid({
    id: 0,
    publicId: emptyGuid,
    marketSourcePublicId: emptyGuid,
    name: "",
    description: "",
    gridRows: 3,
    gridColumns: 3,
    mode: 0,
    allowRePoking: false,
    maxPokeCount: 0,
    backgroundImageUrl: "",
    fontFamily: "",
    congratulationMessage: "",
    overlayWidth: 1920,
    overlayHeight: 1080,
    animation: 0,
    animationDurationMs: 650,
    resultDisplayDurationSeconds: 8,
    pokeSoundUrl: "",
    openSoundUrl: "",
    isBuiltIn: false,
    publicationStatus: 1,
    createdAt: new Date(0).toISOString(),
    updatedAt: new Date(0).toISOString(),
    cells: []
  });
}

export function clonePokeTemplate(template: PokeTemplate): PokeTemplate {
  return { ...template, cells: template.cells.map(cell => ({ ...cell })) };
}

export function resizePokeGrid(template: PokeTemplate): PokeTemplate {
  const gridRows = clampDimension(template.gridRows, 3);
  const gridColumns = clampDimension(template.gridColumns, 3);
  const count = gridRows * gridColumns;
  const cells = [...template.cells]
    .sort((left, right) => left.index - right.index)
    .slice(0, count)
    .map((cell, index) => ({ ...cell, index }));
  while (cells.length < count) cells.push(createPokeCell(cells.length));
  return { ...template, gridRows, gridColumns, cells };
}

export function buildPokeTestUrl(origin: string, publicId: string, token: string): string {
  const url = new URL(`/obs/pokebox/${encodeURIComponent(publicId)}?controls=1`, origin);
  url.hash = new URLSearchParams({ sessionToken: token }).toString();
  return url.toString();
}

function clampDimension(value: number, fallback: number): number {
  return Number.isFinite(value) ? Math.min(10, Math.max(1, Math.trunc(value))) : fallback;
}
