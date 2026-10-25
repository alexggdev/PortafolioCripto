"use client";

import { useRouter } from "next/navigation";
import { useId, useState, type FormEvent } from "react";
import { ConfirmDialog } from "@/shared/components/ConfirmDialog";
import { ChevronLeftIcon, CopyIcon, PencilIcon } from "@/shared/components/Icons";
import { Modal, PRIMARY_BUTTON, SECONDARY_BUTTON } from "@/shared/components/Modal";
import { AvatarImage } from "@/features/portfolio/components/PortfolioAvatar";
import { AVATAR_COUNT, avatarIndex } from "@/features/portfolio/lib/avatar";
import { createPortfolio, deletePortfolio, duplicatePortfolio, updatePortfolio, type ApiResult } from "@/features/portfolio/lib/portfolioApi";
import { rememberPortfolio } from "@/features/portfolio/lib/useOpenPortfolio";
import { copyName, MAX_NAME } from "@/features/portfolio/lib/portfolios";
import { showToast } from "@/shared/lib/toast";
import type { PortfolioInfo } from "@/shared/lib/types";

export type PortfolioDialogState =
  | { kind: "create" }
  | { kind: "edit"; portfolio: PortfolioInfo }
  | { kind: "duplicate"; portfolio: PortfolioInfo }
  | { kind: "delete"; portfolio: PortfolioInfo };

const INPUT = "w-full rounded-xl bg-rule px-4 py-3 font-semibold text-ink placeholder:font-normal placeholder:text-soft";

/** Envía una petición y se ocupa de «guardando…», los errores y el cierre/redirección al terminar bien. */
function useSubmit(onClose: () => void) {
  const router = useRouter();
  const [saving, setSaving] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);

  async function submit<T>(request: () => Promise<ApiResult<T>>, goTo?: (data: T | null) => string, successMessage?: string, openId?: (data: T | null) => string | undefined) {
    setErrors([]);
    setSaving(true);
    const result = await request();
    setSaving(false);
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }
    if (openId) await rememberPortfolio(openId(result.data)); // el nuevo portafolio queda abierto
    onClose();
    if (successMessage) showToast(successMessage);
    if (goTo) router.push(goTo(result.data));
    router.refresh(); // el menú lateral y las listas se leen en el servidor
  }

  return { saving, errors, submit };
}

function Errors({ errors }: { errors: readonly string[] }) {
  if (errors.length === 0) return null;
  return (
    <ul role="alert" className="mt-3 space-y-1 text-sm text-down">
      {errors.map((message) => (
        <li key={message}>{message}</li>
      ))}
    </ul>
  );
}

function NameField({ id, label, value, onChange }: { id: string; label: string; value: string; onChange: (name: string) => void }) {
  return (
    <div className="min-w-0 flex-1">
      <label htmlFor={id} className="mb-2 block font-semibold">
        {label}
      </label>
      <input id={id} value={value} onChange={(event) => onChange(event.target.value)} maxLength={MAX_NAME} autoComplete="off" autoFocus className={INPUT} />
      <p className="mt-2 text-sm text-soft">
        {value.length}/{MAX_NAME} caracteres
      </p>
    </div>
  );
}

/** Elegir una de las imágenes. Devuelve la elegida al pulsar «Guardar». */
function AvatarPicker({ current, onBack, onPick }: { current: number; onBack: () => void; onPick: (index: number) => void }) {
  const [selected, setSelected] = useState(current);
  return (
    <>
      <div className="flex items-center gap-3 pr-10">
        <button type="button" onClick={onBack} aria-label="Volver" className="text-soft hover:text-ink">
          <ChevronLeftIcon className="size-6" />
        </button>
        <h2 className="text-2xl font-bold">Cambiar avatar</h2>
      </div>
      <div className="mt-5 flex justify-center">
        <AvatarImage index={selected} size={96} />
      </div>
      <p className="mt-5 text-center text-soft">Elige tu avatar favorito a continuación:</p>
      <div role="radiogroup" aria-label="Avatares" className="mt-4 grid grid-cols-7 gap-2 border-b border-rule pb-5">
        {Array.from({ length: AVATAR_COUNT }, (_, index) => (
          <button
            key={index}
            type="button"
            role="radio"
            aria-checked={index === selected}
            aria-label={`Avatar ${index + 1}`}
            onClick={() => setSelected(index)}
            className={`rounded-xl p-0.5 ${index === selected ? "ring-2 ring-accent" : "hover:ring-2 hover:ring-rule"}`}
          >
            <AvatarImage index={index} size={44} />
          </button>
        ))}
      </div>
      <button type="button" onClick={() => onPick(selected)} className={`mt-5 ${PRIMARY_BUTTON}`}>
        Guardar
      </button>
    </>
  );
}

/** Crear o editar un portafolio: el mismo formulario, con otro título y otro botón. */
function PortfolioForm({ portfolio, onClose }: { portfolio: PortfolioInfo | null; onClose: () => void }) {
  const uid = useId();
  const { saving, errors, submit } = useSubmit(onClose);
  const creating = portfolio === null;
  const [initialAvatar] = useState(() => (portfolio ? avatarIndex(portfolio) : Math.floor(Math.random() * AVATAR_COUNT)));
  const [name, setName] = useState(portfolio?.name ?? "");
  const [avatar, setAvatar] = useState(initialAvatar);
  const [picking, setPicking] = useState(false);

  function save(event: FormEvent) {
    event.preventDefault();
    if (name.trim() === "") return;
    if (creating) {
      void submit(() => createPortfolio(name.trim(), avatar), () => "/portafolio", "Portafolio creado.", (created) => created?.id);
    } else {
      void submit(() => updatePortfolio(portfolio.id, { name: name.trim(), ...(avatar !== initialAvatar && { avatar }) }));
    }
  }

  return (
    <Modal labelId={`${uid}-title`} onClose={onClose} width="30rem">
      {picking ? (
        <AvatarPicker
          current={avatar}
          onBack={() => setPicking(false)}
          onPick={(index) => {
            setAvatar(index);
            setPicking(false);
          }}
        />
      ) : (
        <form onSubmit={save} noValidate>
          <h2 id={`${uid}-title`} className="pr-10 text-2xl font-bold">
            {creating ? "Crear portafolio" : "Editar portafolio"}
          </h2>
          <div className="mt-5 flex items-start gap-4">
            <NameField id={`${uid}-name`} label="Nombre del portafolio" value={name} onChange={setName} />
            <button type="button" onClick={() => setPicking(true)} aria-label="Cambiar avatar" className="relative mt-9 shrink-0">
              <AvatarImage index={avatar} size={72} />
              <span className="absolute -bottom-1 -right-1 inline-flex size-7 items-center justify-center rounded-full border-2 border-surface bg-paper text-accent">
                <PencilIcon className="size-3.5" />
              </span>
            </button>
          </div>
          <Errors errors={errors} />
          <button type="submit" disabled={saving || name.trim() === ""} className={`mt-5 ${PRIMARY_BUTTON}`}>
            {saving ? "Guardando…" : creating ? "Crear portafolio" : "Guardar"}
          </button>
        </form>
      )}
    </Modal>
  );
}

function DuplicateDialog({ portfolio, onClose }: { portfolio: PortfolioInfo; onClose: () => void }) {
  const uid = useId();
  const { saving, errors, submit } = useSubmit(onClose);
  const [name, setName] = useState(() => copyName(portfolio.name));

  function save(event: FormEvent) {
    event.preventDefault();
    if (name.trim() === "") return;
    void submit(() => duplicatePortfolio(portfolio.id, name.trim()), () => "/portafolio", undefined, (copy) => copy?.id);
  }

  return (
    <Modal labelId={`${uid}-title`} onClose={onClose} width="30rem">
      <form onSubmit={save} noValidate className="flex flex-col items-center text-center">
        <span className="text-accent">
          <CopyIcon className="size-12" />
        </span>
        <h2 id={`${uid}-title`} className="mt-3 text-2xl font-bold">
          Duplicar portafolio
        </h2>
        <p className="mt-3">Se creará un duplicado exacto de este portafolio, con todos sus activos y transacciones.</p>
        <div className="mt-5 w-full text-left">
          <NameField id={`${uid}-name`} label="Nombre del portafolio" value={name} onChange={setName} />
        </div>
        <div className="w-full text-left">
          <Errors errors={errors} />
        </div>
        <div className="mt-6 w-full space-y-3">
          <button type="submit" disabled={saving || name.trim() === ""} className={PRIMARY_BUTTON}>
            {saving ? "Duplicando…" : "Duplicar"}
          </button>
          <button type="button" onClick={onClose} className={SECONDARY_BUTTON}>
            Cancelar
          </button>
        </div>
      </form>
    </Modal>
  );
}

function DeleteDialog({ portfolio, onClose }: { portfolio: PortfolioInfo; onClose: () => void }) {
  const { saving, errors, submit } = useSubmit(onClose);
  return (
    <ConfirmDialog
      title="Eliminar portafolio"
      message={`Se borrarán «${portfolio.name}», sus ${portfolio.transactionCount} transacciones y su historial. No se puede deshacer.`}
      confirmLabel="Eliminar"
      busy={saving}
      errors={errors}
      onConfirm={() => void submit(() => deletePortfolio(portfolio.id), () => "/portafolio")}
      onCancel={onClose}
    />
  );
}

/** Todos los diálogos de un portafolio: crear, editar, duplicar y eliminar. */
export function PortfolioDialog({ state, onClose }: { state: PortfolioDialogState; onClose: () => void }) {
  switch (state.kind) {
    case "create":
      return <PortfolioForm portfolio={null} onClose={onClose} />;
    case "edit":
      return <PortfolioForm portfolio={state.portfolio} onClose={onClose} />;
    case "duplicate":
      return <DuplicateDialog portfolio={state.portfolio} onClose={onClose} />;
    case "delete":
      return <DeleteDialog portfolio={state.portfolio} onClose={onClose} />;
  }
}
