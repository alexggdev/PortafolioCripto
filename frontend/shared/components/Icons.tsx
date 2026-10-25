import type { ReactNode } from "react";

// Iconos de trazo simple (24×24, heredan el color del texto). Decorativos: el botón que los usa lleva su aria-label.

type IconProps = { className?: string };

function Svg({ className = "size-5", children }: IconProps & { children: ReactNode }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      {children}
    </svg>
  );
}

export const PencilIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16v4z" />
    <path d="M13.5 6.5l4 4" />
  </Svg>
);

export const TrashIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M4 7h16M10 11v6M14 11v6" />
    <path d="M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3" />
  </Svg>
);

export const PlusIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M12 5v14M5 12h14" />
  </Svg>
);

export const ChevronLeftIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M15 5l-7 7 7 7" />
  </Svg>
);

export const ChevronRightIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M9 5l7 7-7 7" />
  </Svg>
);

export const DownloadIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M12 4v11M7 11l5 5 5-5M5 20h14" />
  </Svg>
);

export const GridIcon = (p: IconProps) => (
  <svg aria-hidden="true" viewBox="0 0 24 24" className={p.className ?? "size-5"} fill="currentColor">
    {[6, 12, 18].flatMap((x) => [6, 12, 18].map((y) => <circle key={`${x}-${y}`} cx={x} cy={y} r="1.6" />))}
  </svg>
);

export const ListIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01" />
  </Svg>
);

export const MoveIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M4 8h14M14 4l4 4-4 4M20 16H6M10 12l-4 4 4 4" />
  </Svg>
);

export const CheckIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M5 12.5l4.5 4.5L19 7.5" />
  </Svg>
);

/** Flechas arriba/abajo de las cabeceras ordenables; la activa se marca con el color. */
export function SortIcon({ state }: { state: "none" | "asc" | "desc" }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 10 14" className="ml-1 inline-block h-3 w-2 align-middle" fill="currentColor">
      <path d="M5 1l3.5 4.5h-7z" className={state === "asc" ? "text-ink" : "text-soft/50"} />
      <path d="M5 13L1.5 8.5h7z" className={state === "desc" ? "text-ink" : "text-soft/50"} />
    </svg>
  );
}

export const XIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M6 6l12 12M18 6L6 18" />
  </Svg>
);

export const ChevronDownIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M5 9l7 7 7-7" />
  </Svg>
);

export const CalendarIcon = (p: IconProps) => (
  <Svg {...p}>
    <rect x="4" y="5" width="16" height="15" rx="2" />
    <path d="M4 10h16M8 3v4M16 3v4" />
  </Svg>
);

export const DollarIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M12 3v18M16.5 7.5C15.6 6.5 14 6 12 6c-2.5 0-4 1.2-4 3s1.5 2.5 4 3 4 1.2 4 3-1.5 3-4 3c-2 0-3.6-.5-4.5-1.5" />
  </Svg>
);

export const NoteIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M4 20h4L19 9a2.8 2.8 0 0 0-4-4L4 16v4z" />
    <path d="M12 20h8" />
  </Svg>
);

export const WarningIcon = ({ className = "size-12" }: IconProps) => (
  <svg aria-hidden="true" viewBox="0 0 24 24" className={className} fill="currentColor">
    <path d="M12 2.8c.6 0 1.1.3 1.4.8l8.4 14.4c.6 1-.1 2.3-1.4 2.3H3.6c-1.3 0-2-1.3-1.4-2.3L10.6 3.6c.3-.5.8-.8 1.4-.8z" />
    <path d="M12 9v4.5M12 16.5h.01" className="stroke-surface" strokeWidth="2.2" strokeLinecap="round" fill="none" />
  </svg>
);

export const SearchIcon = (p: IconProps) => (
  <Svg {...p}>
    <circle cx="11" cy="11" r="6.5" />
    <path d="M16 16l4.5 4.5" />
  </Svg>
);

export const CopyIcon = (p: IconProps) => (
  <Svg {...p}>
    <rect x="9" y="9" width="11" height="11" rx="2" />
    <path d="M5 15V6a2 2 0 0 1 2-2h8" />
  </Svg>
);

/** Seis puntos: el asa para arrastrar. */
export const GripIcon = ({ className = "size-5" }: IconProps) => (
  <svg aria-hidden="true" viewBox="0 0 24 24" className={className} fill="currentColor">
    {[8, 16].flatMap((x) => [5, 12, 19].map((y) => <circle key={`${x}${y}`} cx={x} cy={y} r="1.6" />))}
  </svg>
);

export const PieIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M10.5 4.1A8 8 0 1 0 19.9 13.5h-9.4z" />
    <path d="M13.5 3.1A8 8 0 0 1 20.9 10.5h-7.4z" />
  </Svg>
);

export const StarIcon = (p: IconProps) => (
  <Svg {...p}>
    <path d="M12 3.5l2.6 5.3 5.9.9-4.2 4.1 1 5.8L12 16.9l-5.3 2.7 1-5.8-4.2-4.1 5.9-.9z" />
  </Svg>
);
