export interface DonatePrize {
  id: number;
  name: string;
  imageUrl: string;
  quantity: number;
  remainingQuantity: number;
  probability: number;
  isGrandPrize: boolean;
}

export interface DonateActivity {
  id: number;
  publicId: string;
  name: string;
  type: number;
  minimumDonationAmount: number;
  startsAtUtc: string;
  endsAtUtc: string;
  animation: number;
  polaroidTemplateKey: string;
  useAiCongratulation: boolean;
  showDonateInformation: boolean;
  resultDisplayDurationSeconds: number;
  animationDurationSeconds: number;
  useWebmAnimation: boolean;
  webmAnimationUrl: string;
  webmPosterUrl: string;
  webmAnimationLoop: boolean;
  isEnabled: boolean;
  prizes: DonatePrize[];
}

export function createDonateActivity(now = new Date()): DonateActivity {
  const startsAt = new Date(now);
  const endsAt = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
  return {
    id: 0,
    publicId: crypto.randomUUID(),
    name: "",
    type: 0,
    minimumDonationAmount: 100,
    startsAtUtc: startsAt.toISOString(),
    endsAtUtc: endsAt.toISOString(),
    animation: 0,
    polaroidTemplateKey: "classic",
    useAiCongratulation: false,
    showDonateInformation: true,
    resultDisplayDurationSeconds: 15,
    animationDurationSeconds: 8,
    useWebmAnimation: false,
    webmAnimationUrl: "",
    webmPosterUrl: "",
    webmAnimationLoop: false,
    isEnabled: false,
    prizes: [createDonatePrize({ probability: 100 })]
  };
}

export function createDonatePrize(values: Partial<DonatePrize> = {}): DonatePrize {
  return {
    id: 0,
    name: "",
    imageUrl: "",
    quantity: 1,
    remainingQuantity: 1,
    probability: 0,
    isGrandPrize: false,
    ...values
  };
}

export function cloneDonateActivity(activity: DonateActivity): DonateActivity {
  return {
    ...activity,
    publicId: activity.publicId || crypto.randomUUID(),
    polaroidTemplateKey: activity.polaroidTemplateKey || "classic",
    resultDisplayDurationSeconds: activity.resultDisplayDurationSeconds > 0 ? activity.resultDisplayDurationSeconds : 15,
    animationDurationSeconds: activity.animationDurationSeconds > 0 ? activity.animationDurationSeconds : 8,
    webmAnimationUrl: activity.webmAnimationUrl || "",
    webmPosterUrl: activity.webmPosterUrl || "",
    prizes: activity.prizes.map(prize => ({ ...prize }))
  };
}

export function toLocalDateTimeInput(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (part: number) => String(part).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

export function localDateTimeToUtc(value: string): string {
  const date = new Date(value);
  if (!value || Number.isNaN(date.getTime())) throw new Error("請輸入有效的活動開始與結束時間。");
  return date.toISOString();
}

export function prizeProbabilityTotal(prizes: DonatePrize[]): number {
  return prizes.reduce((total, prize) => total + Number(prize.probability || 0), 0);
}

export function formatLocalDateTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "—" : new Intl.DateTimeFormat(undefined, { dateStyle: "short", timeStyle: "short" }).format(date);
}

export function buildDonateObsTestUrl(origin: string, publicId: string, token: string): string {
  const url = new URL(`/obs/donate/${encodeURIComponent(publicId)}?controls=1`, origin);
  url.hash = new URLSearchParams({ sessionToken: token }).toString();
  return url.toString();
}
