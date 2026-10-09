export function isValidEmail(value: string): boolean {
  const normalized = value.trim();
  return normalized.length > 0
    && normalized.length <= 254
    && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(normalized);
}
