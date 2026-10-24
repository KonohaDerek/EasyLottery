import { describe, expect, it } from "vitest";
import { adminRoutes } from "../routes";
import { adminNavigationGroups, getActiveAdminNavigationPath, type AdminNavigationLink } from "./admin-navigation";

describe("admin navigation", () => {
  it("separates daily work, results, integrations, and system settings", () => {
    expect(adminNavigationGroups.map(group => group.title)).toEqual([
      "抽獎與活動",
      "直播互動",
      "抽獎模板",
      "結果與紀錄",
      "OBS 與視覺",
      "平台與支付",
      "系統設定與安全"
    ]);
    expect(adminNavigationGroups[3].links.map(([, label]) => label)).toEqual([
      "抽獎紀錄", "活動結果", "Donate 抽獎紀錄", "審計紀錄"
    ]);
  });

  it("maps every admin route to exactly one visible navigation item", () => {
    const links: AdminNavigationLink[] = [];
    for (const group of adminNavigationGroups) links.push(...group.links);
    expect(new Set(links.map(([path]) => path)).size).toBe(links.length);

    for (const route of adminRoutes) {
      const path = route.path
        .replace(":id", "preview-id")
        .replace(":publicId", "public-id")
        .replace(":roundId", "round-id");
      const activePath = getActiveAdminNavigationPath(path);
      expect(activePath, `missing navigation item for ${path}`).toBeDefined();
      expect(links.filter(([linkPath]) => linkPath === activePath)).toHaveLength(1);
    }
  });

  it("matches nested routes to their most specific navigation item and keeps root exact", () => {
    expect(getActiveAdminNavigationPath("/roulette/preview/abc")).toBe("/roulette");
    expect(getActiveAdminNavigationPath("/system/audit/record-1")).toBe("/system/audit");
    expect(getActiveAdminNavigationPath("/")).toBe("/");
    expect(getActiveAdminNavigationPath("/unknown-route")).toBeUndefined();
  });
});
