"use client";

import { useEffect, useId, useState, type FormEvent, type ReactNode } from "react";
import { CalendarIcon, DollarIcon, NoteIcon } from "@/shared/components/Icons";
import { Dropdown } from "@/shared/components/Dropdown";
import { Modal, PRIMARY_BUTTON } from "@/shared/components/Modal";
import { CoinCombobox } from "@/features/portfolio/components/CoinCombobox";
import type { PickedCoin } from "@/features/portfolio/components/CoinPicker";
import { CoinSelectDialog } from "@/features/portfolio/components/CoinSelectDialog";
import { DateView, FeeView, NoteView } from "@/features/portfolio/components/TransactionDialogViews";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { formatUsd, toPlainNumber } from "@/shared/lib/format";
import { CHIP } from "@/shared/lib/ui";
import { usePriceOnDate } from "@/features/portfolio/lib/usePriceOnDate";
import {
  normalizeDecimal,
  priceMode,
  TYPE_OPTIONS,
  utcIsoToLocal,
  validateTransactionForm,
  type FieldErrors,
} from "@/features/portfolio/lib/transactions";
import type { TransactionDto, TransactionType } from "@/shared/lib/types";

/**
 * Sin `coin`, lo primero que se ve es «Seleccionar moneda». Con `coin` (desde una fila de la tabla) se salta ese paso;
 * con `locked` (dentro de la página de una moneda) tampoco se muestra ni se puede cambiar la moneda.
 */
export type DialogState = { mode: "new"; coin?: PickedCoin; locked?: boolean } | { mode: "edit"; tx: TransactionDto };

const inputClass = "w-full rounded-lg bg-rule px-3 py-3 text-ink placeholder:text-soft transition-colors hover:bg-ink/10 focus:bg-ink/10";
const dateChipFormat = new Intl.DateTimeFormat("es-ES", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });

function Field({
  id,
  label,
  error,
  children,
}: {
  id: string;
  label: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <div className="group/field min-w-0 space-y-1.5">
      <label htmlFor={id} className="block cursor-pointer font-semibold text-ink/70 transition-colors group-focus-within/field:text-ink group-hover/field:text-ink">
        {label}
      </label>
      {children}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-sm text-down">
          {error}
        </p>
      )}
    </div>
  );
}

function nowLocal(): string {
  return utcIsoToLocal(new Date().toISOString());
}

/** «24 oct 2025, 03:45» a partir del valor de un <input type="datetime-local">. */
function dateLabel(local: string): string {
  const date = new Date(local);
  return Number.isNaN(date.getTime()) ? "Fecha" : dateChipFormat.format(date);
}

export function TransactionDialog({
  state,
  portfolioId,
  onClose,
  onSaved,
}: {
  state: DialogState;
  /** Portafolio donde se anota una transacción nueva. */
  portfolioId: string | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const uid = useId();
  const editing = state.mode === "edit" ? state.tx : null;

  const [coin, setCoin] = useState<PickedCoin | null>(
    editing
      ? { slug: editing.assetSlug, symbol: editing.symbol, name: editing.name, imageUrl: editing.imageUrl }
      : state.mode === "new"
        ? (state.coin ?? null)
        : null,
  );
  const [type, setType] = useState<TransactionType>(editing?.type ?? "Buy");
  const [quantity, setQuantity] = useState(editing ? toPlainNumber(editing.quantity) : "");
  const [price, setPrice] = useState(editing && editing.unitPriceUsd > 0 ? toPlainNumber(editing.unitPriceUsd) : "");
  const [fee, setFee] = useState(editing && editing.feeUsd > 0 ? toPlainNumber(editing.feeUsd) : "");
  const [date, setDate] = useState(editing ? utcIsoToLocal(editing.dateUtc) : nowLocal());
  const [note, setNote] = useState(editing?.note ?? "");

  const [errors, setErrors] = useState<FieldErrors>({});
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [view, setView] = useState<"main" | "date" | "fee" | "note">("main");
  // Mientras no se escriba el precio a mano, se rellena con el de la moneda en la fecha elegida.
  const [priceTouched, setPriceTouched] = useState(editing !== null);
  const [max] = useState(nowLocal);

  const mode = priceMode(type);
  const isCommon = type === "Buy" || type === "Sell";
  const OTHER_TYPES = TYPE_OPTIONS.filter((o) => o.group === "other");
  const q = normalizeDecimal(quantity);
  const p = normalizeDecimal(price);
  const feeNum = normalizeDecimal(fee);
  const gross = q !== null && p !== null && mode !== "none" ? Number(q) * Number(p) : null;
  const feeValue = feeNum !== null ? Number(feeNum) : 0;
  // Compra: lo gastado incluye la comisión. Venta: lo recibido la descuenta.
  const total = gross === null ? null : type === "Buy" ? gross + feeValue : type === "Sell" ? gross - feeValue : gross;

  const lookup = usePriceOnDate(coin?.slug ?? null, date);
  useEffect(() => {
    if (!priceTouched && mode !== "none" && lookup.status === "found") setPrice(toPlainNumber(lookup.priceUsd));
  }, [lookup, priceTouched, mode]);

  const beforeLaunch =
    coin && lookup.status === "before"
      ? `${coin.name} todavía no tenía precio en esa fecha: sus datos empiezan el ${lookup.firstDate}.`
      : undefined;

  // Solo al abrir desde el botón general: primero se elige la moneda. Después se cambia con el campo desplegable.
  if (!editing && coin === null) return <CoinSelectDialog onPick={setCoin} onClose={onClose} />;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setServerErrors([]);

    const result = validateTransactionForm(
      { assetSlug: coin?.slug ?? "", type, quantity, price, fee, date, note },
      Date.now(),
      editing ? null : portfolioId,
    );
    const found = beforeLaunch ? { ...result.errors, date: beforeLaunch } : result.errors;
    setErrors(found);
    if (result.body === null || beforeLaunch) {
      if (found.date && !result.errors.date) setView("main");
      return;
    }

    setSaving(true);
    try {
      const response = await fetch(editing ? `/api/portfolio/transactions/${editing.id}` : "/api/portfolio/transactions", {
        method: editing ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: result.body,
      });

      if (response.status === 401) {
        redirectToLogin();
        return;
      }
      if (!response.ok) {
        const data = (await response.json().catch(() => null)) as { errors?: string[]; error?: string } | null;
        setServerErrors(data?.errors?.length ? data.errors : [data?.error ?? "No se pudo guardar la transacción."]);
        return;
      }
      onSaved();
    } catch {
      setServerErrors(["No se pudo conectar con el servidor. Inténtalo de nuevo."]);
    } finally {
      setSaving(false);
    }
  }

  const id = (name: string) => `${uid}-${name}`;
  const describe = (name: keyof FieldErrors) => (errors[name] ? `${id(name)}-error` : undefined);
  const dateError = errors.date ?? beforeLaunch;

  const showCoin = !editing && state.mode === "new" && !state.locked && coin !== null;
  const totalLabel = type === "Buy" ? "Total gastado" : type === "Sell" ? "Total recibido" : "Total";
  const title = editing ? "Editar transacción" : "Añadir transacción";
  const back = () => setView("main");

  // Cada pantalla (principal, fecha, tarifa, nota) mide lo que necesita su contenido: no sobra espacio.
  return (
    <Modal labelId={id("title")} onClose={onClose} width="30rem">
      <div>
        {view === "date" && (
          <DateView
            titleId={id("title")}
            value={date}
            max={max}
            onBack={back}
            onApply={(next) => {
              setDate(next);
              setErrors((e) => ({ ...e, date: undefined }));
              back();
            }}
          />
        )}
        {view === "fee" && (
          <FeeView
            titleId={id("title")}
            value={fee}
            onBack={back}
            onApply={(next) => {
              setFee(next);
              setErrors((e) => ({ ...e, fee: undefined }));
              back();
            }}
          />
        )}
        {view === "note" && (
          <NoteView
            titleId={id("title")}
            value={note}
            onBack={back}
            onApply={(next) => {
              setNote(next);
              back();
            }}
          />
        )}

        {view === "main" && (
          <form onSubmit={submit} noValidate className="flex flex-col gap-4">
            <div>
              <h2 id={id("title")} className="pr-10 text-2xl font-bold">
                {title}
              </h2>
            </div>

            {!editing && (
              <>
                <div role="group" aria-label="Tipo de transacción" className="grid grid-cols-3 gap-1 rounded-lg bg-rule p-1">
                  {(
                    [
                      { key: "Buy", label: "Comprar" },
                      { key: "Sell", label: "Vender" },
                      { key: "other", label: "Transferir" },
                    ] as const
                  ).map((tab) => {
                    const active = tab.key === "other" ? !isCommon : type === tab.key;
                    return (
                      <button
                        key={tab.key}
                        type="button"
                        aria-pressed={active}
                        onClick={() => setType(tab.key === "other" ? (OTHER_TYPES[0].value as TransactionType) : tab.key)}
                        className={`cursor-pointer rounded-md px-3 py-2 text-sm font-semibold transition-colors ${active ? "bg-surface text-ink shadow-sm" : "text-soft hover:bg-ink/10 hover:text-ink"}`}
                      >
                        {tab.label}
                      </button>
                    );
                  })}
                </div>

              </>
            )}

            {showCoin && coin && (
              <div>
                <CoinCombobox
                  coin={coin}
                  className={inputClass}
                  onPick={(picked) => {
                    if (picked.slug !== coin.slug) {
                      setPrice("");
                      setPriceTouched(false);
                      setCoin(picked);
                    }
                  }}
                />
              </div>
            )}

            {!isCommon && (
              <div className="group/field space-y-1.5">
                <p className="cursor-default font-semibold text-ink/70 transition-colors group-hover/field:text-ink">Transferir</p>
                <Dropdown<TransactionType>
                  label="Tipo de transferencia"
                  value={type}
                  options={OTHER_TYPES.map((o) => ({ value: o.value, label: o.label }))}
                  onChange={setType}
                  placement="down"
                  align="start"
                  block
                  buttonClassName={`${inputClass} flex cursor-pointer items-center font-semibold`}
                />
              </div>
            )}

            <div className={`grid gap-3 ${mode === "none" ? "" : "sm:grid-cols-2"}`}>
              <Field id={id("quantity")} label="Cantidad" error={errors.quantity}>
                <input
                  id={id("quantity")}
                  value={quantity}
                  onChange={(e) => setQuantity(e.target.value)}
                  inputMode="decimal"
                  autoComplete="off"
                  placeholder="0.00"
                  aria-invalid={!!errors.quantity || undefined}
                  aria-describedby={describe("quantity")}
                  className={inputClass}
                />
              </Field>

              {mode !== "none" && (
                <Field id={id("price")} label="Precio por moneda" error={errors.price}>
                  <div className="relative">
                    <span aria-hidden="true" className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-soft">
                      $
                    </span>
                    <input
                      id={id("price")}
                      value={price}
                      onChange={(e) => {
                        setPrice(e.target.value);
                        setPriceTouched(true);
                      }}
                      inputMode="decimal"
                      autoComplete="off"
                      placeholder="60000"
                      aria-invalid={!!errors.price || undefined}
                      aria-describedby={describe("price")}
                      className={`${inputClass} pl-8`}
                    />
                  </div>
                </Field>
              )}
            </div>

            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={() => setView("date")} className={`${CHIP} ${dateError ? "ring-1 ring-down" : ""}`}>
                <CalendarIcon className="size-4" /> {dateLabel(date)}
              </button>
              <button type="button" onClick={() => setView("fee")} className={`${CHIP} ${errors.fee ? "ring-1 ring-down" : ""}`}>
                <DollarIcon className="size-4 shrink-0" /> <span className="max-w-[9rem] truncate">{fee.trim() || "Comisión"}</span>
              </button>
              <button type="button" onClick={() => setView("note")} className={CHIP}>
                <NoteIcon className="size-4" /> Notas
              </button>
            </div>
            {[dateError, errors.fee, errors.note].filter(Boolean).map((message) => (
              <p key={message} role="alert" className="-mt-3 text-sm text-down">
                {message}
              </p>
            ))}

            {mode !== "none" && (
              <div aria-live="polite" className={editing ? "px-1" : "rounded-lg bg-rule px-4 py-3"}>
                <p className="text-soft">{totalLabel}</p>
                <p className="mt-1 text-3xl font-bold tabular-nums">{total !== null && Number.isFinite(total) && total >= 0 ? formatUsd(total) : "$ 0"}</p>
              </div>
            )}

            {serverErrors.length > 0 && (
              <div role="alert" className="rounded-lg border border-down px-4 py-3 text-sm text-down">
                {serverErrors.map((message) => (
                  <p key={message}>{message}</p>
                ))}
              </div>
            )}

            <button type="submit" disabled={saving} className={PRIMARY_BUTTON}>
              {saving ? "Guardando…" : title}
            </button>
          </form>
        )}
      </div>
    </Modal>
  );
}
