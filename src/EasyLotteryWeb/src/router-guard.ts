export function adminRouteRedirect(path: string, hasToken: boolean): string | null {
  return hasToken ? null : `/login?redirect=${encodeURIComponent(path)}`;
}

export function shopRouteRedirect(enabled: boolean): string | null {
  return enabled ? null : "/";
}
