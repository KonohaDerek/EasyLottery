import { describe, expect, it } from "vitest";
import {
  cloneDonateActivity,
  buildDonateObsTestUrl,
  createDonateActivity,
  createDonatePrize,
  localDateTimeToUtc,
  prizeProbabilityTotal,
  toLocalDateTimeInput
} from "./activity";

describe("Donate activity editor model", () => {
  it("creates defaults compatible with the existing Blazor editor", () => {
    const activity = createDonateActivity(new Date("2026-10-10T00:00:00.000Z"));
    expect(activity).toMatchObject({
      id: 0,
      type: 0,
      minimumDonationAmount: 100,
      startsAtUtc: "2026-10-10T00:00:00.000Z",
      endsAtUtc: "2026-10-17T00:00:00.000Z",
      animation: 0,
      polaroidTemplateKey: "classic",
      resultDisplayDurationSeconds: 15,
      animationDurationSeconds: 8,
      showDonateInformation: true,
      isEnabled: false,
      prizes: [{ id: 0, probability: 100, remainingQuantity: 1, quantity: 1 }]
    });
    expect(activity.publicId).toMatch(/^[0-9a-f-]{36}$/i);
  });

  it("clones all persisted values without sharing prize objects", () => {
    const source = createDonateActivity();
    source.id = 17;
    source.type = 1;
    source.prizes[0] = createDonatePrize({ id: 9, quantity: 8, remainingQuantity: 3, probability: 12.5 });
    const copy = cloneDonateActivity(source);

    expect(copy).toEqual(source);
    expect(copy.prizes[0]).not.toBe(source.prizes[0]);
    copy.prizes[0].remainingQuantity = 0;
    expect(source.prizes[0].remainingQuantity).toBe(3);
  });

  it("formats local input and converts it back to the same UTC instant", () => {
    const utc = "2026-10-10T12:34:00.000Z";
    expect(localDateTimeToUtc(toLocalDateTimeInput(utc))).toBe(utc);
    expect(() => localDateTimeToUtc("not-a-date")).toThrow("請輸入有效的活動開始與結束時間。");
  });

  it("sums probabilities and keeps the 100 percent boundary valid", () => {
    const prizes = [createDonatePrize({ probability: 25 }), createDonatePrize({ probability: 75 })];
    expect(prizeProbabilityTotal(prizes)).toBe(100);
    prizes[1].probability = 75.01;
    expect(prizeProbabilityTotal(prizes)).toBeGreaterThan(100);
  });

  it("keeps OBS session tokens in the URL fragment, never the query", () => {
    const url = new URL(buildDonateObsTestUrl("https://example.test", "public-id", "secret token"));
    expect(url.pathname).toBe("/obs/donate/public-id");
    expect(url.search).toBe("?controls=1");
    expect(url.hash).toBe("#sessionToken=secret+token");
  });
});
