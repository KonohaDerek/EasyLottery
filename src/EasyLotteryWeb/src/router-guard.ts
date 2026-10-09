export function adminRouteRedirect(path: string, hasToken: boolean): string | null {
  return path.startsWith("/system/") && !hasToken ? `/login?redirect=${encodeURIComponent(path)}` : null;
}
