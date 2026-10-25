type Listener = (message: string) => void;

const listeners = new Set<Listener>();

/** Muestra un aviso breve arriba (lo dibuja <Toaster /> en el diseño de la página, así sigue visible al cambiar de página). */
export function showToast(message: string): void {
  listeners.forEach((listener) => listener(message));
}

export function subscribeToasts(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
