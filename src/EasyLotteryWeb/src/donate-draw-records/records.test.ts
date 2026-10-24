import { describe, expect, it } from "vitest";
import { buildDonateDrawHistoryQuery } from "./records";

describe("buildDonateDrawHistoryQuery", () => {
  it("adds trimmed search, outcome, activity, and UTC date boundaries", () => {
    const query = new URLSearchParams(buildDonateDrawHistoryQuery({
      activityId: "7",
      beforeDate: "2026-10-24",
      fromDate: "2026-10-02",
      outcome: "winning",
      page: 2,
      pageSize: 20,
      search: "  特獎  "
    }));

    expect(query.get("activityId")).toBe("7");
    expect(query.get("outcome")).toBe("winning");
    expect(query.get("search")).toBe("特獎");
    expect(query.get("page")).toBe("2");
    expect(query.get("pageSize")).toBe("20");
    expect(new Date(query.get("fromUtc")!).getHours()).toBe(0);
    expect(new Date(query.get("beforeUtc")!).getDate()).toBe(25);
    expect(new Date(query.get("beforeUtc")!).getHours()).toBe(0);
  });

  it("omits empty filters and bounds the page parameters", () => {
    const query = new URLSearchParams(buildDonateDrawHistoryQuery({
      activityId: "",
      beforeDate: "invalid",
      fromDate: "",
      outcome: "all",
      page: 0,
      pageSize: 1000,
      search: "   "
    }));

    expect([...query.entries()]).toEqual([["page", "1"], ["pageSize", "100"]]);
  });
});
