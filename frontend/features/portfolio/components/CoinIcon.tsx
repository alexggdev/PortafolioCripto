/** Logo redondo de la moneda; si no hay imagen, un círculo vacío del mismo tamaño. */
export function CoinIcon({ url, size = 28 }: { url: string | null; size?: number }) {
  return url ? (
    // Imágenes remotas de CoinGecko: <img> directo para no configurar dominios de next/image.
    // eslint-disable-next-line @next/next/no-img-element
    <img src={url} alt="" width={size} height={size} loading="lazy" className="shrink-0 rounded-full" style={{ width: size, height: size }} />
  ) : (
    <span aria-hidden="true" className="inline-block shrink-0 rounded-full bg-rule" style={{ width: size, height: size }} />
  );
}
