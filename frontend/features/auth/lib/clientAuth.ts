/** Sesión caducada en el navegador: limpia las cookies y lleva al login, con vuelta a esta misma página. */
export function redirectToLogin() {
  const next = window.location.pathname + window.location.search;
  window.location.assign(`/api/session/expired?next=${encodeURIComponent(next)}`);
}
