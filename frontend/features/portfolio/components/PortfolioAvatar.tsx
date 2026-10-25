import { AVATAR_GRID, avatarCells, avatarIndex } from "@/features/portfolio/lib/avatar";
import type { PortfolioInfo } from "@/shared/lib/types";

/** Una de las imágenes pixeladas, por su número. Redonda o con esquinas suaves. */
export function AvatarImage({ index, size = 36, round = false }: { index: number; size?: number; round?: boolean }) {
  return (
    <svg
      aria-hidden="true"
      viewBox={`0 0 ${AVATAR_GRID} ${AVATAR_GRID}`}
      width={size}
      height={size}
      shapeRendering="crispEdges"
      className={`shrink-0 ${round ? "rounded-full" : "rounded-xl"}`}
      style={{ width: size, height: size }}
    >
      {avatarCells(index).map((color, i) => (
        <rect key={i} x={i % AVATAR_GRID} y={Math.floor(i / AVATAR_GRID)} width="1" height="1" fill={color} />
      ))}
    </svg>
  );
}

/** La imagen de un portafolio: la que eligió o, si no, una fija según su id. */
export function PortfolioAvatar({ portfolio, size, round }: { portfolio: Pick<PortfolioInfo, "id" | "avatar">; size?: number; round?: boolean }) {
  return <AvatarImage index={avatarIndex(portfolio)} size={size} round={round} />;
}
