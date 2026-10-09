const fallbackPath = "/system/access";

export function safePostLoginRedirect(requestedRedirect: string | undefined, origin: string): string {
  try {
    const target = new URL(requestedRedirect || fallbackPath, origin);
    if (target.origin !== origin || target.pathname.startsWith("//")) return fallbackPath;
    return `${target.pathname}${target.search}${target.hash}`;
  } catch {
    return fallbackPath;
  }
}
