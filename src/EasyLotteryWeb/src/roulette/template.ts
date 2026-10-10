export const rouletteSegmentCounts = [6, 8, 12, 24] as const;
export type RoulettePublicationStatus = 0 | 1; // Published = 0, Draft = 1 (existing API enum values).

export interface RouletteSegment {
  id: number;
  templateId: number;
  index: number;
  title: string;
  imageUrl: string;
  color: string;
  probability: number;
}

export interface RouletteTemplate {
  id: number;
  publicId: string;
  marketSourcePublicId: string;
  name: string;
  description: string;
  segmentCount: number;
  spinDurationSec: number;
  resultDisplayDurationSeconds: number;
  easingFunction: string;
  initialAngleDeg: number;
  centerImageUrl: string;
  backgroundImageUrl: string;
  pointerImageUrl: string;
  spinSoundUrl: string;
  winSoundUrl: string;
  isBuiltIn: boolean;
  publicationStatus: RoulettePublicationStatus;
  createdAt: string;
  updatedAt: string;
  segments: RouletteSegment[];
}

const emptyGuid = "00000000-0000-0000-0000-000000000000";
const segmentColors = ["#e74c3c", "#e67e22", "#f1c40f", "#2ecc71", "#1abc9c", "#3498db", "#9b59b6", "#e91e63"];

export function createRouletteSegment(index: number): RouletteSegment {
  return {
    id: 0,
    templateId: 0,
    index,
    title: `選項 ${index + 1}`,
    imageUrl: "",
    color: segmentColors[index % segmentColors.length],
    probability: 0
  };
}

export function createRouletteTemplate(): RouletteTemplate {
  return resizeRouletteSegments({
    id: 0,
    publicId: emptyGuid,
    marketSourcePublicId: emptyGuid,
    name: "",
    description: "",
    segmentCount: 8,
    spinDurationSec: 5,
    resultDisplayDurationSeconds: 12,
    easingFunction: "ease-out-cubic",
    initialAngleDeg: 0,
    centerImageUrl: "",
    backgroundImageUrl: "",
    pointerImageUrl: "",
    spinSoundUrl: "",
    winSoundUrl: "",
    isBuiltIn: false,
    publicationStatus: 1,
    createdAt: new Date(0).toISOString(),
    updatedAt: new Date(0).toISOString(),
    segments: []
  });
}

export function cloneRouletteTemplate(template: RouletteTemplate): RouletteTemplate {
  return { ...template, segments: template.segments.map(segment => ({ ...segment })) };
}

export function resizeRouletteSegments(template: RouletteTemplate): RouletteTemplate {
  const segmentCount = rouletteSegmentCounts.includes(template.segmentCount as typeof rouletteSegmentCounts[number])
    ? template.segmentCount
    : 8;
  const segments = [...template.segments]
    .sort((left, right) => left.index - right.index)
    .slice(0, segmentCount)
    .map((segment, index) => ({ ...segment, index }));
  while (segments.length < segmentCount) segments.push(createRouletteSegment(segments.length));
  return { ...template, segmentCount, segments };
}

export function buildRouletteTestUrl(origin: string, publicId: string, token: string): string {
  const url = new URL(`/obs/roulette/${encodeURIComponent(publicId)}?controls=1`, origin);
  url.hash = new URLSearchParams({ sessionToken: token }).toString();
  return url.toString();
}
