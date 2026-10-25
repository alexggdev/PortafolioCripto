import Link from "next/link";

export default function NotFound() {
  return (
    <div className="py-20">
      <h1 className="condensed text-3xl font-bold tracking-tight">No encontramos esa página</h1>
      <p className="mt-3 max-w-prose text-soft">La dirección no existe o la moneda ya no está en el catálogo.</p>
      <Link href="/" className="mt-6 inline-block font-medium text-accent underline underline-offset-4">
        Volver al mercado
      </Link>
    </div>
  );
}
