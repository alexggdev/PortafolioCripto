import Link from "next/link";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";

/**
 * Celda «Activo»: toda la celda es un enlace a la ficha pública de la moneda, en otra pestaña.
 * El clic no sube a la fila (que puede tener su propia acción).
 */
export function CoinCell({ slug, name, symbol, imageUrl }: { slug: string; name: string; symbol: string; imageUrl: string | null }) {
  return (
    <td className="relative px-3 py-3">
      <Link
        href={`/monedas/${slug}`}
        target="_blank"
        rel="noopener noreferrer"
        onClick={(event) => event.stopPropagation()}
        className="flex items-center gap-3 after:absolute after:inset-0"
      >
        <CoinIcon url={imageUrl} size={28} />
        <span className="font-semibold">{name}</span>
        <span className="text-soft">{symbol.toUpperCase()}</span>
      </Link>
    </td>
  );
}
