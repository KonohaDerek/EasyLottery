import { isShopEnabled, isShopFeaturePath } from "./api";

import type { LocationQuery, LocationQueryRaw } from "vue-router";

export interface ShopNotFoundRedirect {
  name: "not-found";
  params: { pathMatch: string[] };
  query: LocationQueryRaw;
  hash: string;
  replace: true;
}

export async function shopRouteNotFoundRedirect(
  path: string,
  routeName: unknown,
  query: LocationQuery,
  hash: string,
  isEnabled: () => Promise<boolean> = () => isShopEnabled()
): Promise<ShopNotFoundRedirect | null> {
  if (!isShopFeaturePath(path) || await isEnabled()) return null;
  if (routeName === "not-found") return null;

  return {
    name: "not-found",
    params: { pathMatch: path.slice(1).split("/").filter(Boolean) },
    query,
    hash,
    replace: true
  };
}
