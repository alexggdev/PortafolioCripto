"use client";

import { useEffect, useState } from "react";
import { subscribeToasts } from "@/shared/lib/toast";

const SHOWN_MS = 3500;

/** Aviso breve arriba y centrado (por ejemplo, «Portafolio creado.»). Se quita solo. */
export function Toaster() {
  const [toast, setToast] = useState<{ id: number; message: string } | null>(null);

  useEffect(() => subscribeToasts((message) => setToast({ id: Date.now(), message })), []);

  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), SHOWN_MS);
    return () => clearTimeout(timer);
  }, [toast]);

  if (!toast) return null;
  return (
    <div role="status" key={toast.id} className="toast-in pointer-events-none fixed inset-x-0 top-3 z-[100] flex justify-center px-4">
      <p className="flex items-center gap-3 rounded-xl bg-white px-5 py-3 text-sm font-medium text-neutral-900 shadow-xl">
        <svg aria-hidden="true" viewBox="0 0 24 24" className="size-6 shrink-0 text-up" fill="none">
          <circle cx="12" cy="12" r="10" fill="currentColor" />
          <path d="M7.5 12.5l3 3 6-6.5" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
        {toast.message}
      </p>
    </div>
  );
}
