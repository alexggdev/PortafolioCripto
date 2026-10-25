// Los slugs son los identificadores de CoinGecko: minúsculas, números, guiones y poco más.
const SLUG = /^[a-z0-9][a-z0-9._-]{0,199}$/;

export function isValidSlug(value: string): boolean {
  return SLUG.test(value);
}
