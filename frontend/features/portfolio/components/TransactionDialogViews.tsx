"use client";

import { useState, type ReactNode } from "react";
import { Dropdown } from "@/shared/components/Dropdown";
import { ChevronLeftIcon, ChevronRightIcon } from "@/shared/components/Icons";
import { PRIMARY_BUTTON } from "@/shared/components/Modal";
import { formatLocal, monthGrid, parseLocal, pickDay, timeOf, timeSlots, WEEKDAYS } from "@/features/portfolio/lib/calendar";
import { MAX_NOTE, normalizeDecimal } from "@/features/portfolio/lib/transactions";

const FIELD = "w-full rounded-xl bg-rule px-4 py-3 text-ink placeholder:text-soft transition-colors hover:bg-ink/10 focus:bg-ink/10";
const MONTHS = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];
const FIRST_YEAR = 2009;

/** Cabecera de una subpantalla: flecha para volver y título. */
export function ViewHeader({ titleId, title, onBack }: { titleId: string; title: string; onBack: () => void }) {
  return (
    <div className="flex items-center gap-3 pr-10">
      <button type="button" onClick={onBack} aria-label="Volver" className="inline-flex size-9 items-center justify-center rounded-lg text-ink hover:bg-rule">
        <ChevronLeftIcon className="size-6" />
      </button>
      <h2 id={titleId} className="text-2xl font-bold">
        {title}
      </h2>
    </div>
  );
}

/** Esqueleto común: cabecera, contenido y botón principal. Mide lo que necesita su contenido, sin huecos. */
function ViewFrame({ titleId, title, onBack, action, children }: { titleId: string; title: string; onBack: () => void; action: ReactNode; children: ReactNode }) {
  return (
    <div>
      <ViewHeader titleId={titleId} title={title} onBack={onBack} />
      <div className="mt-4">{children}</div>
      <div className="mt-5">{action}</div>
    </div>
  );
}

/** Calendario y hora. Los días futuros no se pueden elegir. */
export function DateView({ titleId, value, max, onApply, onBack }: { titleId: string; value: string; max: string; onApply: (local: string) => void; onBack: () => void }) {
  const limit = parseLocal(max)!;
  const first = parseLocal(value) ?? limit;
  const [draft, setDraft] = useState(value);
  const [shown, setShown] = useState({ year: first.year, month: first.month });
  const selected = parseLocal(draft) ?? limit;
  const weeks = monthGrid(shown.year, shown.month);

  const atLatest = shown.year === limit.year && shown.month === limit.month;
  const atEarliest = shown.year === FIRST_YEAR && shown.month === 0;
  const step = (delta: number) => {
    const d = new Date(shown.year, shown.month + delta, 1);
    setShown({ year: d.getFullYear(), month: d.getMonth() });
  };
  const years = Array.from({ length: limit.year - FIRST_YEAR + 1 }, (_, i) => FIRST_YEAR + i);
  const times = [...new Set([timeOf(draft), ...timeSlots()])].sort();
  const setTime = (time: string) => {
    const next = `${draft.slice(0, 10)}T${time}`;
    setDraft(next > max ? max : next);
  };
  const selectClass = "rounded-lg bg-transparent px-1 py-1 font-medium hover:bg-rule";

  return (
    <ViewFrame
      titleId={titleId}
      title="Fecha y hora"
      onBack={onBack}
      action={
        <button type="button" onClick={() => onApply(draft)} className={PRIMARY_BUTTON}>
          Cambiar fecha y hora
        </button>
      }
    >
      <div className="flex items-center justify-between">
        <button type="button" onClick={() => step(-1)} disabled={atEarliest} aria-label="Mes anterior" className="inline-flex size-9 cursor-pointer items-center justify-center rounded-lg hover:bg-rule disabled:cursor-default disabled:opacity-30">
          <ChevronLeftIcon />
        </button>
        <div className="flex items-center gap-2">
          <select aria-label="Mes" value={shown.month} onChange={(e) => setShown((s) => ({ ...s, month: Number(e.target.value) }))} className={selectClass}>
            {MONTHS.map((name, i) => (
              <option key={name} value={i} disabled={shown.year === limit.year && i > limit.month}>
                {name}
              </option>
            ))}
          </select>
          <select aria-label="Año" value={shown.year} onChange={(e) => setShown((s) => ({ ...s, year: Number(e.target.value), month: Math.min(s.month, Number(e.target.value) === limit.year ? limit.month : 11) }))} className={selectClass}>
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </div>
        <button type="button" onClick={() => step(1)} disabled={atLatest} aria-label="Mes siguiente" className="inline-flex size-9 cursor-pointer items-center justify-center rounded-lg hover:bg-rule disabled:cursor-default disabled:opacity-30">
          <ChevronRightIcon />
        </button>
      </div>

      <div className="mt-2 grid grid-cols-7 text-center text-sm">
        {WEEKDAYS.map((d) => (
          <span key={d} className="py-1 font-medium text-soft">
            {d}
          </span>
        ))}
        {weeks.flat().map((cell) => {
          const isSelected = cell.year === selected.year && cell.month === selected.month && cell.day === selected.day;
          const future = formatLocal(cell.year, cell.month, cell.day, 0, 0) > max;
          return (
            <button
              key={`${cell.year}-${cell.month}-${cell.day}`}
              type="button"
              disabled={future}
              aria-pressed={isSelected}
              onClick={() => {
                setDraft(pickDay(draft, cell, max));
                if (!cell.inMonth) setShown({ year: cell.year, month: cell.month });
              }}
              className={`mx-auto inline-flex size-7 items-center justify-center rounded-full tabular-nums ${
                isSelected ? "bg-accent font-semibold text-on-accent" : cell.inMonth ? "hover:bg-rule" : "text-soft hover:bg-rule"
              } disabled:cursor-not-allowed disabled:opacity-30 disabled:hover:bg-transparent`}
            >
              {cell.day}
            </button>
          );
        })}
      </div>

      <div className="mt-3 flex items-center justify-between">
        <span>Hora</span>
        <Dropdown
          label="Hora"
          value={timeOf(draft)}
          options={times.map((t) => ({ value: t, label: t }))}
          onChange={setTime}
          buttonClassName="inline-flex w-28 items-center justify-center gap-2 rounded-xl bg-accent px-4 py-3 font-semibold text-on-accent"
        />
      </div>
    </ViewFrame>
  );
}

/** Comisión en dólares. Se comprueba al aplicar, así el error aparece donde se escribe. */
export function FeeView({ titleId, value, onApply, onBack }: { titleId: string; value: string; onApply: (fee: string) => void; onBack: () => void }) {
  const [draft, setDraft] = useState(value);
  const [error, setError] = useState<string | null>(null);

  function apply() {
    if (draft.trim() !== "" && normalizeDecimal(draft) === null) {
      setError("La comisión no es un número válido.");
      return;
    }
    onApply(draft.trim());
  }

  return (
    <ViewFrame
      titleId={titleId}
      title="Añadir tarifa"
      onBack={onBack}
      action={
        <button type="button" onClick={apply} className={PRIMARY_BUTTON}>
          Añadir tarifa
        </button>
      }
    >
      <div className="relative">
        <span aria-hidden="true" className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-soft">
          $
        </span>
        <input
          value={draft}
          onChange={(e) => {
            setDraft(e.target.value);
            setError(null);
          }}
          onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), apply())}
          inputMode="decimal"
          autoComplete="off"
          autoFocus
          aria-label="Comisión en dólares"
          aria-invalid={!!error || undefined}
          className={`${FIELD} pl-9`}
        />
      </div>
      {error && (
        <p role="alert" className="mt-3 text-sm text-down">
          {error}
        </p>
      )}
    </ViewFrame>
  );
}

/** Nota libre con contador de caracteres. */
export function NoteView({ titleId, value, onApply, onBack }: { titleId: string; value: string; onApply: (note: string) => void; onBack: () => void }) {
  const [draft, setDraft] = useState(value);

  return (
    <ViewFrame
      titleId={titleId}
      title="Añadir nota"
      onBack={onBack}
      action={
        <button type="button" onClick={() => onApply(draft.trim())} disabled={draft.length > MAX_NOTE} className={PRIMARY_BUTTON}>
          Añadir nota
        </button>
      }
    >
      <div className="relative">
        <textarea
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          autoFocus
          placeholder="Escribe tu nota aquí…"
          aria-label="Nota"
          maxLength={MAX_NOTE}
          className={`${FIELD} h-36 resize-none pb-8`}
        />
        <span className={`pointer-events-none absolute bottom-3 right-4 text-sm tabular-nums ${draft.length > MAX_NOTE ? "text-down" : "text-ink"}`}>
          {draft.length}/{MAX_NOTE}
        </span>
      </div>
    </ViewFrame>
  );
}
