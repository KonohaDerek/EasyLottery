import { parse } from "yaml";
import { apiRequest, apiText } from "../api/client";
import type { DonateDrawRecord } from "./lifecycle";

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
  prizes: DonatePrize[];
}

export interface DonateSnapshot {
  activity: DonateActivity | null;
  records: DonateDrawRecord[];
}

type RecordValue = Record<string, unknown>;

export async function readDonateSnapshot(routeKey: string): Promise<DonateSnapshot> {
  const { data } = await apiText("/settings");
  const parsed = parse(data);
  if (!isRecord(parsed)) throw new Error("Donate OBS 設定格式無效。");

  const activities = arrayField(parsed, "DonateLotteryActivities", "donateLotteryActivities");
  const numericRouteId = /^\d+$/.test(routeKey) ? Number(routeKey) : null;
  const activity = activities
    .filter(isRecord)
    .map(normalizeActivity)
    .find(item => item.publicId.toLocaleLowerCase() === routeKey.toLocaleLowerCase()
      || (numericRouteId !== null && item.id === numericRouteId)) ?? null;
  if (!activity) return { activity: null, records: [] };

  const records = arrayField(parsed, "DonateLotteryDrawRecords", "donateLotteryDrawRecords")
    .filter(isRecord)
    .map(normalizeDrawRecord)
    .filter((record): record is DonateDrawRecord & { activityId: number } => record !== null && record.activityId === activity.id);
  return { activity, records };
}

export async function sendTestDonation(activity: DonateActivity, request: {
  donorName: string;
  amount: number;
  message: string;
  paymentMethod: string;
}): Promise<{ processed: boolean; reason: string; records: DonateDrawRecord[] }> {
  const { data } = await apiRequest<{ processed: boolean; reason: string; records: DonateDrawRecord[] }>(
    `/api/obs/donate/${encodeURIComponent(activity.publicId)}/test`,
    { method: "POST", body: JSON.stringify(request) }
  );
  return data;
}

export async function generateDonateCongratulation(activity: DonateActivity, record: DonateDrawRecord): Promise<string> {
  const { data } = await apiRequest<{ message?: string }>("/api/polaroid-congratulation", {
    method: "POST",
    body: JSON.stringify({ activityPublicId: activity.publicId, donorName: record.donorName, prizeName: record.prizeName })
  });
  return data.message?.trim() ?? "";
}

function normalizeActivity(value: RecordValue): DonateActivity {
  const prizes = arrayField(value, "Prizes", "prizes").filter(isRecord).map(prize => ({
    id: numberField(prize, "id", "Id"),
    name: stringField(prize, "name", "Name"),
    imageUrl: stringField(prize, "imageUrl", "ImageUrl"),
    quantity: numberField(prize, "quantity", "Quantity"),
    remainingQuantity: numberField(prize, "remainingQuantity", "RemainingQuantity"),
    probability: numberField(prize, "probability", "Probability"),
    isGrandPrize: booleanField(prize, "isGrandPrize", "IsGrandPrize")
  }));
  return {
    id: numberField(value, "id", "Id"),
    publicId: stringField(value, "publicId", "PublicId"),
    name: stringField(value, "name", "Name"),
    type: numberField(value, "type", "Type"),
    minimumDonationAmount: numberField(value, "minimumDonationAmount", "MinimumDonationAmount"),
    animation: animationValue(field(value, "animation", "Animation")),
    polaroidTemplateKey: stringField(value, "polaroidTemplateKey", "PolaroidTemplateKey") || "classic",
    useAiCongratulation: booleanField(value, "useAiCongratulation", "UseAiCongratulation"),
    showDonateInformation: booleanField(value, "showDonateInformation", "ShowDonateInformation", true),
    resultDisplayDurationSeconds: numberField(value, "resultDisplayDurationSeconds", "ResultDisplayDurationSeconds", 15),
    animationDurationSeconds: numberField(value, "animationDurationSeconds", "AnimationDurationSeconds", 8),
    useWebmAnimation: booleanField(value, "useWebmAnimation", "UseWebmAnimation"),
    webmAnimationUrl: stringField(value, "webmAnimationUrl", "WebmAnimationUrl"),
    webmPosterUrl: stringField(value, "webmPosterUrl", "WebmPosterUrl"),
    webmAnimationLoop: booleanField(value, "webmAnimationLoop", "WebmAnimationLoop"),
    prizes
  };
}

function normalizeDrawRecord(value: RecordValue): (DonateDrawRecord & { activityId: number }) | null {
  const id = numberField(value, "id", "Id");
  const activityId = numberField(value, "activityId", "ActivityId");
  if (!Number.isInteger(id) || id <= 0 || !Number.isInteger(activityId)) return null;
  return {
    id,
    activityId,
    donorName: stringField(value, "donorName", "DonorName", "匿名贊助者"),
    amount: numberField(value, "amount", "Amount"),
    prizeName: stringField(value, "prizeName", "PrizeName"),
    prizeImageUrl: stringField(value, "prizeImageUrl", "PrizeImageUrl"),
    isWinning: booleanField(value, "isWinning", "IsWinning"),
    donationMessage: stringField(value, "donationMessage", "DonationMessage"),
    paymentMethod: stringField(value, "paymentMethod", "PaymentMethod")
  };
}

function animationValue(value: unknown): number {
  if (typeof value === "number" && Number.isInteger(value)) return Math.min(4, Math.max(0, value));
  switch (String(value ?? "").toLocaleLowerCase()) {
    case "gacha": return 1;
    case "garagara": return 2;
    case "moneybox": return 3;
    case "scratchcard":
    case "scratch-card": return 4;
    default: return 0;
  }
}

function isRecord(value: unknown): value is RecordValue {
  return !!value && typeof value === "object" && !Array.isArray(value);
}

function field(value: RecordValue, lower: string, pascal: string): unknown {
  return Object.hasOwn(value, lower) ? value[lower] : value[pascal];
}

function arrayField(value: RecordValue, pascal: string, camel: string): unknown[] {
  const result = value[pascal] ?? value[camel];
  return Array.isArray(result) ? result : [];
}

function stringField(value: RecordValue, lower: string, pascal: string, fallback = ""): string {
  const result = field(value, lower, pascal);
  return typeof result === "string" ? result : fallback;
}

function numberField(value: RecordValue, lower: string, pascal: string, fallback = 0): number {
  const result = field(value, lower, pascal);
  return typeof result === "number" && Number.isFinite(result) ? result : fallback;
}

function booleanField(value: RecordValue, lower: string, pascal: string, fallback = false): boolean {
  const result = field(value, lower, pascal);
  return typeof result === "boolean" ? result : fallback;
}
