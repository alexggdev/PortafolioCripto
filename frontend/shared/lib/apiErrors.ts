// Saca los mensajes legibles de una respuesta de error de la API de .NET.
// Cubre los dos formatos: nuestro ProblemDetails (errors = lista de textos) y el de validación
// automática de ASP.NET (errors = { campo: [textos] }).

export function extractErrors(body: unknown, fallback: string): string[] {
  if (typeof body !== "object" || body === null) return [fallback];
  const { errors, detail, title } = body as { errors?: unknown; detail?: unknown; title?: unknown };

  const messages: string[] = [];
  if (Array.isArray(errors)) {
    for (const item of errors) if (typeof item === "string" && item.trim()) messages.push(item.trim());
  } else if (typeof errors === "object" && errors !== null) {
    for (const list of Object.values(errors)) {
      if (Array.isArray(list)) for (const item of list) if (typeof item === "string" && item.trim()) messages.push(item.trim());
    }
  }
  if (messages.length > 0) return messages;

  if (typeof detail === "string" && detail.trim()) return [detail.trim()];
  if (typeof title === "string" && title.trim()) return [title.trim()];
  return [fallback];
}
