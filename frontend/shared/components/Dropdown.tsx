"use client";

import { useEffect, useId, useMemo, useRef, useState, type ReactNode } from "react";
import { CheckIcon, ChevronDownIcon, SearchIcon } from "@/shared/components/Icons";

export interface DropdownOption<T extends string> {
  value: T;
  label: string;
  icon?: ReactNode;
  /** Contenido propio de la opción (en vez de icono + texto). */
  content?: ReactNode;
}

/**
 * Lista desplegable que se abre hacia arriba por defecto y flota sobre lo demás: no cambia el tamaño de la ventana
 * ni de la tabla donde está. Con `searchable`, el botón se convierte en un campo para filtrar la lista.
 */
export function Dropdown<T extends string>({
  label,
  value,
  options,
  onChange,
  placement = "up",
  align = "end",
  searchable = false,
  buttonClassName = "inline-flex items-center gap-2 rounded-xl bg-paper px-3 py-2.5 text-sm font-semibold hover:text-accent",
  block = false,
}: {
  /** Nombre accesible de la lista. */
  label: string;
  value: T;
  options: readonly DropdownOption<T>[];
  onChange: (value: T) => void;
  placement?: "up" | "down";
  /** Con `block` el desplegable ocupa todo el ancho; si no, se alinea al borde indicado del botón. */
  align?: "start" | "end";
  searchable?: boolean;
  buttonClassName?: string;
  block?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const listId = useId();

  useEffect(() => {
    if (!open) return;
    const onPointer = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault(); // que Esc cierre solo la lista y no la ventana que la contiene
        event.stopPropagation();
        setOpen(false);
      }
    };
    document.addEventListener("pointerdown", onPointer);
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("pointerdown", onPointer);
      document.removeEventListener("keydown", onKey, true);
    };
  }, [open]);

  useEffect(() => {
    if (open && searchable) inputRef.current?.focus();
    if (!open) setQuery("");
  }, [open, searchable]);

  // Al abrir, la lista se desplaza para que la opción elegida quede a la vista (útil en una lista larga de horas).
  useEffect(() => {
    const list = listRef.current;
    const current = list?.querySelector<HTMLElement>('[aria-selected="true"]');
    if (open && list && current) list.scrollTop = current.offsetTop - list.clientHeight / 2 + current.clientHeight / 2;
  }, [open]);

  const selected = options.find((o) => o.value === value) ?? options[0];
  const shown = useMemo(() => {
    const q = query.trim().toLowerCase();
    return q ? options.filter((o) => o.label.toLowerCase().includes(q)) : options;
  }, [options, query]);

  const render = (o: DropdownOption<T>) =>
    o.content ?? (
      <>
        {o.icon}
        <span className="truncate">{o.label}</span>
      </>
    );

  return (
    <div ref={rootRef} className={`relative ${block ? "block w-full" : "inline-block"}`}>
      {open && searchable ? (
        <div className={`${buttonClassName} ${block ? "w-full" : ""}`}>
          <input
            ref={inputRef}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            aria-label={`Buscar en ${label}`}
            aria-controls={listId}
            placeholder="Buscar"
            className="w-24 min-w-0 bg-transparent outline-none placeholder:text-soft"
          />
          <SearchIcon className="size-4 shrink-0 text-soft" />
        </div>
      ) : (
        <button
          type="button"
          onClick={() => setOpen((o) => !o)}
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-label={`${label}: ${selected?.label ?? ""}`}
          className={`${buttonClassName} ${block ? "w-full justify-between" : ""}`}
        >
          {selected && render(selected)}
          <ChevronDownIcon className={`size-4 shrink-0 text-soft transition-transform ${open ? "rotate-180" : ""}`} />
        </button>
      )}

      {open && (
        <ul
          ref={listRef}
          id={listId}
          role="listbox"
          aria-label={label}
          className={`fade-in absolute z-40 max-h-64 w-max min-w-full max-w-[20rem] overflow-y-auto rounded-xl bg-surface p-1 shadow-xl ring-1 ring-rule ${
            placement === "up" ? "bottom-full mb-2" : "top-full mt-2"
          } ${block ? "left-0 right-0" : align === "end" ? "right-0" : "left-0"}`}
        >
          {shown.length === 0 && <li className="px-3 py-2 text-sm text-soft">Sin resultados</li>}
          {shown.map((o) => (
            <li key={o.value} role="option" aria-selected={o.value === value}>
              <button
                type="button"
                onClick={() => {
                  onChange(o.value);
                  setOpen(false);
                }}
                className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-semibold hover:bg-paper"
              >
                <span className="flex min-w-0 flex-1 items-center gap-3">{render(o)}</span>
                {o.value === value && <CheckIcon className="size-4 shrink-0 text-accent" />}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
