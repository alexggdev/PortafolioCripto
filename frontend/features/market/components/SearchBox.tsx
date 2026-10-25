"use client";

import { useEffect, useRef, useState, useTransition, type ChangeEvent } from "react";
import { useRouter } from "next/navigation";
import { toPageHref, type ListQuery } from "@/features/market/lib/query";

const DEBOUNCE_MS = 300;

export function SearchBox({ query }: { query: ListQuery }) {
  const router = useRouter();
  const [value, setValue] = useState(query.search);
  const [pending, startTransition] = useTransition();
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => () => clearTimeout(timer.current), []);

  function onChange(event: ChangeEvent<HTMLInputElement>) {
    const next = event.target.value;
    setValue(next);

    clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      startTransition(() => {
        router.replace(toPageHref(query, { search: next.trim(), page: 1 }), { scroll: false });
      });
    }, DEBOUNCE_MS);
  }

  return (
    <form role="search" onSubmit={(event) => event.preventDefault()} className="w-full sm:max-w-sm">
      <label htmlFor="market-search" className="sr-only">
        Buscar moneda por nombre o símbolo
      </label>
      <input
        id="market-search"
        type="search"
        value={value}
        onChange={onChange}
        maxLength={50}
        autoComplete="off"
        placeholder="Buscar por nombre o símbolo"
        aria-busy={pending}
        className="w-full border border-rule bg-surface px-3 py-2 text-ink placeholder:text-soft"
      />
    </form>
  );
}
