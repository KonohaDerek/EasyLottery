import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiRequest, apiText } from "../api/client";
import { readDonateSnapshot, sendTestDonation } from "./api";

vi.mock("../api/client", () => ({ apiRequest: vi.fn(), apiText: vi.fn() }));

const activity = {
  id: 4,
  publicId: "f23f3cf7-9c65-4d19-99b4-837bfb5cb3e1",
  name: "OBS Donate",
  animation: "Garagara",
  showDonateInformation: true,
  animationDurationSeconds: 8,
  resultDisplayDurationSeconds: 15,
  prizes: [{ id: 2, name: "大獎", imageUrl: "", isGrandPrize: true }]
};

describe("Donate OBS API adapter", () => {
  beforeEach(() => vi.clearAllMocks());

  it("reads scoped PascalCase YAML and filters records to the routed activity", async () => {
    vi.mocked(apiText).mockResolvedValue({
      data: `DonateLotteryActivities:\n  - Id: 4\n    PublicId: ${activity.publicId}\n    Name: OBS Donate\n    Animation: Garagara\n    ShowDonateInformation: true\n    AnimationDurationSeconds: 8\n    ResultDisplayDurationSeconds: 15\n    Prizes:\n      - Id: 2\n        Name: 大獎\n        ImageUrl: ''\n        IsGrandPrize: true\nDonateLotteryDrawRecords:\n  - Id: 13\n    ActivityId: 4\n    DonorName: 小明\n    Amount: 500\n    PrizeName: 大獎\n    IsWinning: true\n  - Id: 14\n    ActivityId: 8\n    DonorName: 其他活動\n`,
      etag: ""
    });

    const snapshot = await readDonateSnapshot(activity.publicId.toUpperCase());

    expect(apiText).toHaveBeenCalledWith("/settings");
    expect(snapshot.activity?.animation).toBe(2);
    expect(snapshot.activity?.prizes[0]?.isGrandPrize).toBe(true);
    expect(snapshot.records.map(record => [record.id, record.donorName])).toEqual([[13, "小明"]]);
  });

  it("supports the legacy numeric activity key", async () => {
    vi.mocked(apiText).mockResolvedValue({ data: `DonateLotteryActivities:\n  - Id: 4\n    PublicId: ${activity.publicId}\n    Name: OBS Donate\n`, etag: "" });

    await expect(readDonateSnapshot("4")).resolves.toMatchObject({ activity: { id: 4 }, records: [] });
  });

  it("sends test entries only to the existing OBS test endpoint", async () => {
    vi.mocked(apiRequest).mockResolvedValue({ data: { processed: true, reason: "", records: [] }, etag: "" });
    const normalized = {
      id: activity.id,
      publicId: activity.publicId,
      name: activity.name,
      type: 1,
      minimumDonationAmount: 1,
      animation: 2,
      polaroidTemplateKey: "classic",
      useAiCongratulation: false,
      showDonateInformation: true,
      resultDisplayDurationSeconds: 15,
      animationDurationSeconds: 8,
      useWebmAnimation: false,
      webmAnimationUrl: "",
      webmPosterUrl: "",
      webmAnimationLoop: false,
      prizes: []
    };

    await sendTestDonation(normalized, { donorName: "測試者", amount: 100, message: "測試", paymentMethod: "測試付款" });

    expect(apiRequest).toHaveBeenCalledWith(`/api/obs/donate/${activity.publicId}/test`, {
      method: "POST",
      body: JSON.stringify({ donorName: "測試者", amount: 100, message: "測試", paymentMethod: "測試付款" })
    });
  });
});
