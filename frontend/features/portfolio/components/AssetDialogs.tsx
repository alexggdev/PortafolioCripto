"use client";

import { useId, useState } from "react";
import { ConfirmDialog } from "@/shared/components/ConfirmDialog";
import { useMoney } from "@/features/currency/components/CurrencyProvider";
import { Dropdown } from "@/shared/components/Dropdown";
import { Modal, PRIMARY_BUTTON } from "@/shared/components/Modal";
import { CoinIcon } from "@/features/portfolio/components/CoinIcon";
import { PortfolioAvatar } from "@/features/portfolio/components/PortfolioAvatar";
import { redirectToLogin } from "@/features/auth/lib/clientAuth";
import type { PortfolioInfo, PortfolioValue, PositionDto } from "@/shared/lib/types";

export type AssetAction = { kind: "move" | "remove"; position: PositionDto };

/** Un portafolio con su imagen, nombre y valor (como en el diálogo «Mover activo» de CoinMarketCap). */
function PortfolioLine({ portfolio, valueUsd }: { portfolio: PortfolioInfo; valueUsd: number | null }) {
  const money = useMoney();
  return (
    <span className="flex min-w-0 items-center gap-3">
      <PortfolioAvatar portfolio={portfolio} size={40} round />
      <span className="min-w-0 text-left">
        <span className="block truncate font-semibold">{portfolio.name}</span>
        {valueUsd !== null && <span className="block text-sm text-soft">{money.price(valueUsd)}</span>}
      </span>
    </span>
  );
}

const BLOCK = "flex w-full items-center justify-between gap-3 rounded-xl bg-rule px-4 py-3";

/** Mover una moneda a otro portafolio, o quitarla del actual (con aviso de confirmación). */
export function AssetActionDialog({
  action,
  portfolios,
  values,
  portfolioId,
  onClose,
  onDone,
}: {
  action: AssetAction;
  portfolios: PortfolioInfo[];
  /** Valor de cada portafolio, para mostrarlo en «Desde» y «A». Opcional. */
  values?: PortfolioValue[] | null;
  portfolioId: string | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const money = useMoney();
  const titleId = useId();
  const { position, kind } = action;
  const others = portfolios.filter((p) => p.id !== portfolioId);
  const current = portfolios.find((p) => p.id === portfolioId) ?? null;
  const valueOf = (id: string) => values?.find((v) => v.id === id)?.valueUsd ?? null;

  const [target, setTarget] = useState(others[0]?.id ?? "");
  const [errors, setErrors] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  const slug = encodeURIComponent(position.slug);
  const targetPortfolio = others.find((p) => p.id === target) ?? null;

  async function run() {
    if (portfolioId === null) return;
    setErrors([]);
    setSaving(true);
    try {
      const response =
        kind === "move"
          ? await fetch(`/api/portfolio/assets/${slug}/move`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({ fromPortfolioId: portfolioId, toPortfolioId: target }),
            })
          : await fetch(`/api/portfolio/assets/${slug}?portfolioId=${portfolioId}`, { method: "DELETE" });

      if (response.status === 401) {
        redirectToLogin();
        return;
      }
      if (!response.ok) {
        const body = (await response.json().catch(() => null)) as { errors?: string[]; error?: string } | null;
        setErrors(body?.errors?.length ? body.errors : [body?.error ?? "No se pudo completar la operación."]);
        return;
      }
      onDone();
    } catch {
      setErrors(["No se pudo conectar con el servidor. Inténtalo de nuevo."]);
    } finally {
      setSaving(false);
    }
  }

  if (kind === "remove") {
    return (
      <ConfirmDialog
        title={`Quitar ${position.name}`}
        message={`Se borrarán todas las transacciones de ${position.name} en ${current ? `«${current.name}»` : "este portafolio"}, y también su ganancia realizada. No se puede deshacer.`}
        confirmLabel="Quitar"
        busy={saving}
        errors={errors}
        onConfirm={run}
        onCancel={onClose}
      />
    );
  }

  return (
    <Modal labelId={titleId} onClose={onClose} width="30rem">
      <h2 id={titleId} className="pr-10 text-2xl font-bold">
        Mover activo
      </h2>

      {others.length === 0 || !current ? (
        <p className="mt-6 text-soft">
          Solo tienes un portafolio. Crea otro desde el menú lateral («+ Crear portafolio») y podrás mover monedas entre ellos.
        </p>
      ) : (
        <div className="mt-6 space-y-5">
          <div>
            <p className="mb-2 text-soft">Activo</p>
            <div className={BLOCK}>
              <span className="flex items-center gap-3">
                <CoinIcon url={position.imageUrl} size={40} />
                <span>
                  <span className="block font-semibold">{position.symbol.toUpperCase()}</span>
                  <span className="block text-sm text-soft">{money.price(position.valueUsd)}</span>
                </span>
              </span>
            </div>
          </div>

          <div>
            <p className="mb-2 text-soft">Desde</p>
            <div className={BLOCK}>
              <PortfolioLine portfolio={current} valueUsd={valueOf(current.id)} />
            </div>
          </div>

          <div>
            <p className="mb-2 text-soft">A</p>
            <Dropdown
              label="Portafolio de destino"
              block
              value={target}
              onChange={setTarget}
              options={others.map((p) => ({ value: p.id, label: p.name, content: <PortfolioLine portfolio={p} valueUsd={valueOf(p.id)} /> }))}
              buttonClassName={BLOCK}
            />
            <p className="mt-3 text-sm text-soft">
              El activo y sus transacciones pasan al otro portafolio. Si ya lo tiene, las posiciones se suman.
            </p>
          </div>

          {errors.length > 0 && (
            <ul role="alert" className="space-y-1 text-sm text-down">
              {errors.map((message) => (
                <li key={message}>{message}</li>
              ))}
            </ul>
          )}

          <button type="button" onClick={run} disabled={saving || !targetPortfolio} className={PRIMARY_BUTTON}>
            {saving ? "Moviendo…" : "Mover"}
          </button>
        </div>
      )}
    </Modal>
  );
}
