import { PortfolioSkeleton } from "@/shared/components/PageSkeleton";

// Mientras el servidor prepara el portafolio: la forma de la página, con bloques que parpadean.
export default function Loading() {
  return <PortfolioSkeleton />;
}
