import { useEffect, useRef, useState } from "react";

/** Ancho actual de un elemento (se actualiza al redimensionar). Sirve para dibujar un SVG a tamaño real. */
export function useElementWidth(initial = 640) {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(initial);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const update = () => setWidth(Math.max(240, Math.floor(el.clientWidth)));
    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return [ref, width] as const;
}
