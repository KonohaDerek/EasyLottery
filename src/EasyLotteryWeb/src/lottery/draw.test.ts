import { describe, expect, it } from "vitest";
import { drawLottery, participantGroups, type Participant } from "./draw";

const participant = (name: string, values: Partial<Participant> = {}): Participant => ({
  id: name,
  lotteryGroupId: name,
  name,
  level: "",
  group: "",
  tags: [],
  isWinner: false,
  ...values
});

describe("core lottery draw", () => {
  it("draws an unselected participant, consumes the first prize, and marks all weighted tickets for that entry", () => {
    const entries = [
      participant("Ada", { lotteryGroupId: "member-1" }),
      participant("Ada", { id: "ticket-2", lotteryGroupId: "member-1" }),
      participant("Bo")
    ];

    const result = drawLottery(entries, ["Grand prize", "Second prize"], count => count - 1);

    expect(result.winner).toMatchObject({ member: { name: "Bo" }, prize: "Grand prize" });
    expect(result.participants.map(item => item.isWinner)).toEqual([false, false, true]);
    expect(result.prizes).toEqual(["Second prize"]);
  });

  it("prevents a weighted member from winning twice while preserving their separate ticket rows", () => {
    const entries = [
      participant("Ada", { lotteryGroupId: "member-1", isWinner: true }),
      participant("Ada", { id: "ticket-2", lotteryGroupId: "member-1", isWinner: true }),
      participant("Bo")
    ];

    const result = drawLottery(entries, ["Prize"], () => 0);

    expect(result.winner?.member.name).toBe("Bo");
    expect(result.participants.filter(item => item.name === "Ada")).toHaveLength(2);
  });

  it("does not mutate state or draw when prizes, participants, or eligible participants are missing", () => {
    const entries = [participant("Ada", { isWinner: true })];
    expect(drawLottery(entries, ["Prize"], () => 0)).toMatchObject({ winner: null, reason: "no-eligible-participants" });
    expect(drawLottery([participant("Ada")], [], () => 0)).toMatchObject({ winner: null, reason: "no-prizes" });
    expect(entries[0].isWinner).toBe(true);
  });

  it("lists distinct groups and provides the legacy ungrouped choice only once", () => {
    expect(participantGroups([
      participant("Ada", { group: " VIP " }),
      participant("Bo", { group: "vip" }),
      participant("Cy")
    ])).toEqual(["VIP", "未分組"]);
  });
});
