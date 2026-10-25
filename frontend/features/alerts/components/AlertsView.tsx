"use client";

import Link from "next/link";
import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { CoinPicker, type PickedCoin } from "@/features/portfolio/components/CoinPicker";
import { PctText } from "@/shared/components/PctText";
import { UpdatedAgo } from "@/features/market/components/UpdatedAgo";
import {
  ALERTS_CHANGED_EVENT,
  DIRECTION_OPTIONS,
  directionLabel,
  distanceToTargetPct,
  splitAlerts,
  validateAlertForm,
  type AlertFieldErrors,
} from "@/features/alerts/lib/alerts";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import { formatUsd } from "@/shared/lib/format";
import type { AlertDirection, AssetSummary, PriceAlert } from "@/shared/lib/types";
import { ROW_HOVER } from "@/shared/lib/ui";
import { usePolledJson } from "@/shared/lib/usePolledJson";

const POLL_MS = 15_000;
const inputClass = "w-full border border-rule bg-surface px-3 py-2 text-ink placeholder:text-soft";

const dateFormat = new Intl.DateTimeFormat("es-ES", { dateStyle: "medium", timeStyle: "short" });

function notifyChanged() {
  window.dispatchEvent(new Event(ALERTS_CHANGED_EVENT));
}

export function AlertsView({ initial, initialCoin }: { initial: PriceAlert[]; initialCoin: PickedCoin | null }) {
  const [version, setVersion] = useState(0);
  const { data, updatedAt, failing } = usePolledJson<PriceAlert[]>(`/api/alerts?v=${version}`, initial, POLL_MS, {
    onUnauthorized: redirectToLogin,
  });
  const { active, triggered } = splitAlerts(data);

  // Lo que llegó sin ver sigue marcado como «Nueva» mientras estás en la página, aunque ya se marque como visto.
  const [fresh, setFresh] = useState<Set<number>>(() => new Set());
  const markingRef = useRef(false);
  useEffect(() => {
    const unread = data.filter((a) => a.unread).map((a) => a.id);
    if (unread.length === 0) return;
    setFresh((prev) => new Set([...prev, ...unread]));
    if (markingRef.current) return;
    markingRef.current = true;
    void fetch("/api/alerts/read", { method: "POST" })
      .then(() => notifyChanged())
      .catch(() => undefined)
      .finally(() => {
        markingRef.current = false;
      });
  }, [data]);

  const [actionError, setActionError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  async function remove(alert: PriceAlert) {
    setBusyId(alert.id);
    setActionError(null);
    try {
      const response = await fetch(`/api/alerts/${alert.id}`, { method: "DELETE" });
      if (response.status === 401) {
        redirectToLogin();
        return;
      }
      if (!response.ok && response.status !== 404) {
        setActionError("No se pudo borrar la alerta.");
        return;
      }
      setVersion((v) => v + 1);
      notifyChanged();
    } catch {
      setActionError("No se pudo conectar con el servidor. Inténtalo de nuevo.");
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="mt-8 space-y-10">
      <CreateAlertForm initialCoin={initialCoin} onCreated={() => setVersion((v) => v + 1)} />

      {actionError && (
        <p role="alert" className="border-l-2 border-down pl-3 text-sm text-down">
          {actionError}
        </p>
      )}

      <section aria-labelledby="activas">
        <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="activas" className="condensed text-2xl font-bold tracking-tight">
            Activas
          </h2>
          <UpdatedAgo updatedAt={updatedAt} failing={failing} />
        </div>
        {active.length === 0 ? (
          <p className="text-soft">No tienes alertas activas. Crea una arriba y te avisaremos cuando el precio llegue.</p>
        ) : (
          <div className="overflow-x-auto bg-surface">
            <table className="w-full min-w-[34rem] border-collapse text-left text-sm sm:text-base">
              <caption className="sr-only">Alertas activas</caption>
              <thead>
                <tr className="border-b border-rule text-sm text-soft">
                  <th scope="col" className="px-3 py-3 font-medium">Moneda</th>
                  <th scope="col" className="px-3 py-3 font-medium">Aviso</th>
                  <th scope="col" className="px-3 py-3 text-right font-medium">Precio actual</th>
                  <th scope="col" className="px-3 py-3 text-right font-medium">Falta</th>
                  <th scope="col" className="px-3 py-3 text-right font-medium">
                    <span className="sr-only">Acciones</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {active.map((alert) => (
                  <tr key={alert.id} className={`border-b border-rule last:border-b-0 ${ROW_HOVER}`}>
                    <td className="px-3 py-3"><CoinCell alert={alert} /></td>
                    <td className="px-3 py-3">
                      <span className="text-soft">{directionLabel(alert.direction)}</span>{" "}
                      <span className="font-medium tabular-nums">{formatUsd(alert.targetPriceUsd)}</span>
                    </td>
                    <td className="px-3 py-3 text-right tabular-nums">{formatUsd(alert.currentPriceUsd)}</td>
                    <td className="px-3 py-3 text-right">
                      <PctText value={distanceToTargetPct(alert.currentPriceUsd, alert.targetPriceUsd)} />
                    </td>
                    <td className="px-3 py-3 text-right">
                      <DeleteButton alert={alert} busy={busyId === alert.id} onDelete={remove} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section aria-labelledby="disparadas">
        <h2 id="disparadas" className="condensed mb-3 text-2xl font-bold tracking-tight">
          Disparadas
        </h2>
        {triggered.length === 0 ? (
          <p className="text-soft">Cuando una alerta se cumpla, aparecerá aquí y en la campana del menú.</p>
        ) : (
          <ul className="divide-y divide-rule bg-surface">
            {triggered.map((alert) => (
              <li key={alert.id} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 px-3 py-3">
                <div className="min-w-0">
                  <CoinCell alert={alert} />
                  <p className="mt-1 text-sm text-soft">
                    {directionLabel(alert.direction)} {formatUsd(alert.targetPriceUsd)} · llegó a{" "}
                    <span className="font-medium text-ink tabular-nums">{formatUsd(alert.triggeredPriceUsd)}</span> ·{" "}
                    {alert.triggeredAtUtc ? dateFormat.format(new Date(alert.triggeredAtUtc)) : ""}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {fresh.has(alert.id) && <span className="bg-accent px-2 py-0.5 text-xs font-bold text-on-accent">Nueva</span>}
                  <DeleteButton alert={alert} busy={busyId === alert.id} onDelete={remove} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <p className="max-w-prose text-sm text-soft">
        Los precios de las alertas son en dólares (US$). Se revisan cada pocos segundos con el precio en vivo; si una
        moneda no tiene un precio reciente, no se dispara. Cada alerta salta una sola vez.
      </p>
    </div>
  );
}

function CoinCell({ alert }: { alert: PriceAlert }) {
  return (
    <Link href={`/monedas/${alert.assetSlug}`} className="inline-flex items-center gap-2">
      {alert.imageUrl ? (
        // eslint-disable-next-line @next/next/no-img-element
        <img src={alert.imageUrl} alt="" width={20} height={20} loading="lazy" referrerPolicy="no-referrer" className="size-5 rounded-full" />
      ) : (
        <span aria-hidden="true" className="inline-block size-5 rounded-full bg-rule" />
      )}
      <span className="font-medium">{alert.name}</span>
      <span className="text-sm text-soft">{alert.symbol.toUpperCase()}</span>
    </Link>
  );
}

function DeleteButton({ alert, busy, onDelete }: { alert: PriceAlert; busy: boolean; onDelete: (a: PriceAlert) => void }) {
  return (
    <button
      type="button"
      onClick={() => onDelete(alert)}
      disabled={busy}
      aria-label={`Borrar la alerta de ${alert.name}`}
      className="text-sm text-soft underline underline-offset-4 hover:text-down disabled:opacity-60"
    >
      {busy ? "Borrando…" : "Borrar"}
    </button>
  );
}

function CreateAlertForm({ initialCoin, onCreated }: { initialCoin: PickedCoin | null; onCreated: () => void }) {
  const uid = useId();
  const [coin, setCoin] = useState<PickedCoin | null>(initialCoin);
  const [direction, setDirection] = useState<AlertDirection>("Above");
  const [price, setPrice] = useState("");
  const [errors, setErrors] = useState<AlertFieldErrors>({});
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [current, setCurrent] = useState<number | null>(null);

  // Precio actual de la moneda elegida, para saber qué objetivo poner.
  useEffect(() => {
    setCurrent(null);
    if (!coin) return;
    const controller = new AbortController();
    fetch(`/api/assets/${encodeURIComponent(coin.slug)}`, { cache: "no-store", signal: controller.signal })
      .then((r) => (r.ok ? (r.json() as Promise<AssetSummary>) : null))
      .then((asset) => setCurrent(asset?.priceUsd ?? null))
      .catch(() => undefined);
    return () => controller.abort();
  }, [coin]);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setServerErrors([]);
    const result = validateAlertForm({ assetSlug: coin?.slug ?? "", direction, price });
    setErrors(result.errors);
    if (result.body === null) return;

    setSaving(true);
    try {
      const response = await fetch("/api/alerts", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: result.body,
      });
      if (response.status === 401) {
        redirectToLogin();
        return;
      }
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { errors?: string[]; error?: string } | null;
        setServerErrors(body?.errors?.length ? body.errors : [body?.error ?? "No se pudo crear la alerta."]);
        return;
      }
      setPrice("");
      setErrors({});
      onCreated();
      notifyChanged();
    } catch {
      setServerErrors(["No se pudo conectar con el servidor. Inténtalo de nuevo."]);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate aria-labelledby={`${uid}-title`} className="space-y-4 bg-surface p-4 sm:p-5">
      <h2 id={`${uid}-title`} className="condensed text-2xl font-bold tracking-tight">
        Nueva alerta
      </h2>

      <div className="space-y-1">
        <span className="block text-sm font-medium">Moneda</span>
        <CoinPicker value={coin} onChange={setCoin} invalid={!!errors.asset} describedBy={`${uid}-asset-error`} />
        {errors.asset && (
          <p id={`${uid}-asset-error`} role="alert" className="text-sm text-down">
            {errors.asset}
          </p>
        )}
        {coin && current !== null && (
          <p className="text-sm text-soft">
            Precio actual: <span className="tabular-nums">{formatUsd(current)}</span>
          </p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1">
          <label htmlFor={`${uid}-direction`} className="block text-sm font-medium">
            Avisarme
          </label>
          <select
            id={`${uid}-direction`}
            value={direction}
            onChange={(event) => setDirection(event.target.value as AlertDirection)}
            className={inputClass}
          >
            {DIRECTION_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="space-y-1">
          <label htmlFor={`${uid}-price`} className="block text-sm font-medium">
            Precio objetivo (US$)
          </label>
          <input
            id={`${uid}-price`}
            value={price}
            onChange={(event) => setPrice(event.target.value)}
            inputMode="decimal"
            autoComplete="off"
            placeholder="70000"
            aria-invalid={!!errors.price || undefined}
            aria-describedby={errors.price ? `${uid}-price-error` : undefined}
            className={inputClass}
          />
          {errors.price && (
            <p id={`${uid}-price-error`} role="alert" className="text-sm text-down">
              {errors.price}
            </p>
          )}
        </div>
      </div>

      {serverErrors.length > 0 && (
        <ul role="alert" className="space-y-1 border-l-2 border-down pl-3 text-sm text-down">
          {serverErrors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}

      <button type="submit" disabled={saving} className="bg-accent px-4 py-2 font-medium text-on-accent disabled:opacity-60">
        {saving ? "Creando…" : "Crear alerta"}
      </button>
    </form>
  );
}
