export type AdminNavigationLink = readonly [path: string, label: string];

export interface AdminNavigationGroup {
  title: string;
  links: readonly AdminNavigationLink[];
}

export const adminNavigationGroups = [
  { title: "抽獎與活動", links: [["/", "抽獎工作區"], ["/donate-activities", "Donate 活動"], ["/market", "活動市集"]] },
  { title: "直播互動", links: [["/interactions/rounds", "直播互動回合"], ["/interactions/identity-link", "觀眾身份綁定"], ["/activity/overtime", "加班台設定"]] },
  { title: "抽獎模板", links: [["/roulette", "轉盤模板"], ["/pokebox", "戳戳樂模板"]] },
  { title: "結果與紀錄", links: [["/lottery-draw-records", "抽獎紀錄"], ["/activity-results", "活動結果"], ["/donate-lottery-records", "Donate 抽獎紀錄"], ["/system/audit", "審計紀錄"]] },
  { title: "OBS 與視覺", links: [["/system/obs-layouts", "OBS 版面"], ["/system/obs-assets", "OBS 資產"], ["/system/sound-cues", "音效預設"], ["/system/visual-styles", "視覺樣式"]] },
  { title: "平台與支付", links: [["/system/platform-connections", "直播平台串接"], ["/system/youtube-login", "YouTube 直播設定"], ["/system/payment", "支付配置"]] },
  { title: "系統設定與安全", links: [["/system/access", "管理員 Passkey"], ["/system/backups", "設定備份"]] }
] as const satisfies readonly AdminNavigationGroup[];

export function getAdminNavigationGroups(shopEnabled: boolean): readonly AdminNavigationGroup[] {
  return shopEnabled
    ? [...adminNavigationGroups, { title: "卡牌商店", links: [["/system/card-hub", "Card Hub 連結"]] }]
    : adminNavigationGroups;
}

export function getActiveAdminNavigationPath(currentPath: string, shopEnabled = false): string | undefined {
  const normalizedPath = normalizePath(currentPath);
  const links: AdminNavigationLink[] = [];
  for (const group of getAdminNavigationGroups(shopEnabled)) links.push(...group.links);
  return links
    .filter(([path]) => path === "/"
      ? normalizedPath === "/"
      : normalizedPath === path || normalizedPath.startsWith(`${path}/`))
    .sort((left, right) => right[0].length - left[0].length)[0]?.[0];
}

export type PublicNavigationLink = readonly [path: string, label: string];

const publicNavigationLinks = [
  ["/about", "關於"],
  ["/privacy-policy", "隱私權政策"],
  ["/login", "管理員登入"]
] as const satisfies readonly PublicNavigationLink[];

export function getPublicNavigationLinks(shopEnabled: boolean): readonly PublicNavigationLink[] {
  return shopEnabled
    ? [...publicNavigationLinks.slice(0, 1), ["/shop", "卡牌商店"], ...publicNavigationLinks.slice(1)]
    : publicNavigationLinks;
}

function normalizePath(path: string): string {
  return path.length > 1 ? path.replace(/\/+$/, "") : path;
}
