"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { XIcon } from "@/shared/components/Icons";

/**
 * Ventana de diálogo común (el elemento nativo <dialog>): se abre al montarse, se cierra con Esc, con la X o
 * pulsando el fondo, y avisa con `onClose` en cualquiera de los casos. Quien la usa pinta su propio título
 * con `id={labelId}` para que el lector de pantalla la nombre.
 */
export function Modal({
  labelId,
  onClose,
  width = "28rem",
  children,
}: {
  labelId: string;
  onClose: () => void;
  width?: string;
  children: ReactNode;
}) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = ref.current;
    if (dialog && !dialog.open) dialog.showModal();
  }, []);

  return (
    <dialog
      ref={ref}
      onClose={onClose}
      onClick={(event) => {
        if (event.target === ref.current) ref.current?.close(); // clic en el fondo
      }}
      aria-labelledby={labelId}
      style={{ width: `min(${width}, calc(100vw - 2rem))` }}
      className="m-auto rounded-2xl bg-surface p-0 text-ink shadow-2xl backdrop:bg-black/60"
    >
      <button
        type="button"
        onClick={() => ref.current?.close()}
        aria-label="Cerrar"
        className="absolute right-4 top-4 inline-flex size-9 items-center justify-center rounded-lg text-soft hover:text-ink"
      >
        <XIcon />
      </button>
      <div className="max-h-[90dvh] overflow-y-auto p-6">{children}</div>
    </dialog>
  );
}

/** Estilos compartidos por los botones grandes de los diálogos. */
export const PRIMARY_BUTTON = "w-full rounded-lg bg-accent px-4 py-3 font-semibold text-on-accent hover:opacity-90 disabled:opacity-60";
export const SECONDARY_BUTTON = "w-full rounded-lg bg-rule px-4 py-3 font-semibold text-ink hover:opacity-90";
