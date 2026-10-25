// IP del visitante para el limitador de peticiones de la API.
// Detrás del proxy (Caddy) la cabecera X-Forwarded-For llega con la IP real como primer valor: Caddy descarta lo que
// el visitante intente enviar en esa cabecera. Se reenvía solo ese primer valor, y solo si parece una IP.

const IPV4 = /^\d{1,3}(\.\d{1,3}){3}$/;
const IPV6 = /^[0-9a-fA-F:.]+$/;

export function clientIpFrom(header: string | null | undefined): string | null {
  if (!header) return null;
  const first = header.split(",")[0]?.trim() ?? "";
  if (first.length === 0 || first.length > 45) return null;
  if (IPV4.test(first)) return first;
  if (first.includes(":") && IPV6.test(first)) return first;
  return null;
}
