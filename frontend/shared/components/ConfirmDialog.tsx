"use client";

import { useId } from "react";
import { WarningIcon } from "@/shared/components/Icons";
import { Modal, PRIMARY_BUTTON, SECONDARY_BUTTON } from "@/shared/components/Modal";

/** Aviso de confirmación centrado, con triángulo rojo: «¿Seguro que quieres eliminar esta transacción?». */
export function ConfirmDialog({
  title,
  message,
  confirmLabel,
  busy = false,
  errors = [],
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel: string;
  busy?: boolean;
  errors?: readonly string[];
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const id = useId();
  return (
    <Modal labelId={id} onClose={onCancel} width="30rem">
      <div className="flex flex-col items-center text-center">
        <span className="text-down">
          <WarningIcon className="size-12" />
        </span>
        <h2 id={id} className="mt-4 text-2xl font-bold">
          {title}
        </h2>
        <p className="mt-3">{message}</p>
        {errors.length > 0 && (
          <ul role="alert" className="mt-3 space-y-1 text-sm text-down">
            {errors.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        )}
        <div className="mt-6 w-full space-y-3">
          <button type="button" onClick={onConfirm} disabled={busy} className={PRIMARY_BUTTON}>
            {busy ? "Un momento…" : confirmLabel}
          </button>
          <button type="button" onClick={onCancel} className={SECONDARY_BUTTON}>
            Cancelar
          </button>
        </div>
      </div>
    </Modal>
  );
}
