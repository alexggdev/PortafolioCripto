"use client";

import { useCallback, useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { createPortal } from "react-dom";

export interface PopMenuItem<T extends string> {
  key: T;
  text: string;
  danger?: boolean;
  /** Icono a la izquierda del texto (como en CoinMarketCap). */
  icon?: ReactNode;
  /** Línea pequeña bajo el texto. */
  description?: string;
  /** Con valor, la opción es un interruptor (encendido o apagado) y el menú no se cierra al pulsarla. */
  checked?: boolean;
  /** Línea separadora encima de esta opción. */
  separated?: boolean;
}

function Switch({ on }: { on: boolean }) {
  return (
    <span aria-hidden="true" className={`relative ml-auto inline-flex h-6 w-11 shrink-0 rounded-full transition-colors ${on ? "bg-accent" : "bg-rule"}`}>
      <span className={`absolute top-0.5 size-5 rounded-full bg-white shadow transition-all ${on ? "left-[1.375rem]" : "left-0.5"}`} />
    </span>
  );
}

/** Botón «···» redondeado como el de CoinMarketCap. */
export function DotsIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" className="size-5" fill="currentColor">
      <circle cx="5" cy="12" r="1.7" />
      <circle cx="12" cy="12" r="1.7" />
      <circle cx="19" cy="12" r="1.7" />
    </svg>
  );
}

/**
 * Panel flotante junto a un botón. Se dibuja en el <body> con posición fija: ni las tablas con scroll lo recortan ni
 * las filas con animación (que crean su propio plano) lo tapan o lo desplazan. `align="start"` lo abre desde el
 * botón hacia la derecha; `"end"`, hacia la izquierda. Si no cabe, se desplaza lo justo para quedar dentro de la pantalla.
 */
export function PopPanel({
  anchor,
  align = "end",
  label,
  role,
  onClose,
  onKeyDown,
  className = "",
  matchWidth = false,
  autoFocus = true,
  onPointerEnter,
  onPointerLeave,
  children,
}: {
  anchor: HTMLElement;
  align?: "start" | "end";
  label: string;
  role?: string;
  onClose: () => void;
  onKeyDown?: (event: KeyboardEvent<HTMLDivElement>) => void;
  className?: string;
  /** El panel mide lo mismo que el botón (para listas desplegables de un campo). */
  matchWidth?: boolean;
  /** Pasa el foco al primer botón o campo al abrir (desactivado en paneles que se abren al pasar el ratón). */
  autoFocus?: boolean;
  onPointerEnter?: () => void;
  onPointerLeave?: () => void;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const panel = ref.current;
    if (!panel) return;
    const rect = anchor.getBoundingClientRect();
    const margin = 8;
    if (matchWidth) panel.style.width = `${rect.width}px`;
    const left = align === "start" ? rect.left : rect.right - panel.offsetWidth;
    panel.style.left = `${Math.min(Math.max(margin, left), window.innerWidth - panel.offsetWidth - margin)}px`;
    const below = rect.bottom + 6;
    const fits = below + panel.offsetHeight <= window.innerHeight - margin;
    panel.style.top = `${fits ? below : Math.max(margin, rect.top - panel.offsetHeight - 6)}px`;
  }, [anchor, align, matchWidth]);

  useEffect(() => {
    const onPointer = (event: PointerEvent) => {
      const target = event.target as Node;
      if (!ref.current?.contains(target) && !anchor.contains(target)) onClose();
    };
    const onKey = (event: globalThis.KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault(); // dentro de una ventana, Esc cierra solo el panel
        onClose();
        anchor.focus();
      }
    };
    // Al desplazar la página se cierra; al desplazar la propia lista (si la tiene), no.
    const onScroll = (event: Event) => {
      if (!ref.current?.contains(event.target as Node)) onClose();
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey, true);
    window.addEventListener("scroll", onScroll, true);
    window.addEventListener("resize", onClose);
    if (autoFocus) ref.current?.querySelector<HTMLElement>("button, input")?.focus();
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey, true);
      window.removeEventListener("scroll", onScroll, true);
      window.removeEventListener("resize", onClose);
    };
  }, [anchor, onClose, autoFocus]);

  return createPortal(
    <div
      ref={ref}
      role={role}
      aria-label={label}
      onKeyDown={onKeyDown}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
      style={{ position: "fixed", top: 0, left: 0 }}
      className={`z-50 rounded-xl border border-rule bg-surface shadow-xl ${className}`}
    >
      {children}
    </div>,
    anchor.closest("dialog") ?? document.body, // dentro de una ventana modal, el panel va dentro de ella o quedaría detrás
  );
}

/**
 * Menú desplegable de acciones. Se dibuja con posición fija junto al botón: las tablas tienen scroll
 * horizontal y un menú normal quedaría recortado.
 */
export function PopMenu<T extends string>({
  label,
  items,
  onChoose,
  buttonClassName = "inline-flex size-8 items-center justify-center rounded-lg text-soft hover:bg-paper hover:text-ink",
  icon,
  widthClass = "w-60",
  align = "end",
}: {
  /** Nombre accesible: «Más acciones de Bitcoin». */
  label: string;
  items: readonly PopMenuItem<T>[];
  onChoose: (key: T) => void;
  buttonClassName?: string;
  /** Icono del botón; por defecto los tres puntos. */
  icon?: ReactNode;
  /** Ancho del menú (clase de Tailwind). */
  widthClass?: string;
  /** «start»: el menú se abre desde el botón hacia la derecha; «end» (por defecto), hacia la izquierda. */
  align?: "start" | "end";
}) {
  const [anchor, setAnchor] = useState<HTMLButtonElement | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const open = anchor !== null;

  const close = useCallback(() => setAnchor(null), []);

  function onMenuKey(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    event.preventDefault();
    const buttons = Array.from(menuRef.current?.querySelectorAll<HTMLButtonElement>("button") ?? []);
    const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
    const next = event.key === "ArrowDown" ? (index + 1) % buttons.length : (index - 1 + buttons.length) % buttons.length;
    buttons[next]?.focus();
  }

  return (
    <>
      <button
        type="button"
        onClick={(event) => {
          const button = event.currentTarget;
          setAnchor((current) => (current ? null : button));
        }}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={label}
        title={label}
        className={buttonClassName}
      >
        {icon ?? <DotsIcon />}
      </button>
      {anchor && (
        <PopPanel anchor={anchor} align={align} label={label} role="menu" onClose={close} onKeyDown={onMenuKey} className={`${widthClass} p-1`}>
          <div ref={menuRef}>
            {items.map((item) => (
              <button
                key={item.key}
                type="button"
                role={item.checked === undefined ? "menuitem" : "menuitemcheckbox"}
                aria-checked={item.checked}
                onClick={() => {
                  if (item.checked === undefined) close();
                  onChoose(item.key);
                }}
                className={`flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-medium hover:bg-paper ${
                  item.separated ? "mt-1 border-t border-rule pt-3" : ""
                } ${item.danger ? "text-down" : "text-ink"}`}
              >
                {item.icon && <span className="shrink-0 text-soft">{item.icon}</span>}
                <span className="min-w-0">
                  {item.text}
                  {item.description && <span className="mt-0.5 block text-xs font-normal text-soft">{item.description}</span>}
                </span>
                {item.checked !== undefined && <Switch on={item.checked} />}
              </button>
            ))}
          </div>
        </PopPanel>
      )}
    </>
  );
}
