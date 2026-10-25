import type { MetadataRoute } from "next";

// El mercado y las fichas de monedas son públicos. Las zonas privadas y la API no deben aparecer en buscadores.
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: "*", allow: "/", disallow: ["/api/", "/portafolio", "/seguimiento", "/alertas", "/login", "/registro"] }],
  };
}
