"use client";

import { useState } from "react";
import { CircleAlert, KeyRound, LoaderCircle, RefreshCw, ShieldCheck, WifiOff } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useDeviceIdentity } from "@/features/device-identity/device-identity-provider";

export function DeviceIdentityGate({ children }: { children: React.ReactNode }) {
  const { identity, loading, busy, error, register, refreshStatus } = useDeviceIdentity();
  const [displayName, setDisplayName] = useState("Dispositivo de campo");

  if (loading) {
    return <div className="grid min-h-72 place-items-center text-slate-600" role="status">
      <span className="flex items-center gap-2"><LoaderCircle className="animate-spin" /> Validando dispositivo seguro...</span>
    </div>;
  }

  if (identity?.status === "AUTHORIZED") return children;

  const offline = typeof navigator !== "undefined" && !navigator.onLine;
  const revoked = identity?.status === "REVOKED";
  const pending = identity?.status === "PENDING";

  return (
    <section className="mx-auto max-w-xl rounded-xl border bg-white p-6 shadow-sm" aria-labelledby="device-gate-title">
      <span className={`grid size-14 place-items-center rounded-full ${revoked ? "bg-red-100 text-red-700" : pending ? "bg-amber-100 text-amber-800" : "bg-blue-100 text-blue-900"}`}>
        {offline && !identity ? <WifiOff /> : revoked || pending ? <CircleAlert /> : <KeyRound />}
      </span>
      <h1 id="device-gate-title" className="mt-4 text-2xl font-black text-slate-950">
        {revoked ? "Dispositivo revogado" : pending ? "Autorização pendente" : "Cadastre este dispositivo"}
      </h1>
      <p className="mt-2 text-slate-600">
        {revoked
          ? "Este dispositivo foi revogado e não pode registrar novas leituras. A fila local foi preservada para revisão."
          : pending
            ? "Este dispositivo ainda não foi autorizado. Solicite a liberação a um gerente ou administrador."
            : offline
              ? "O primeiro cadastro exige internet. Conecte-se para solicitar a autorização deste dispositivo."
              : "Antes da primeira coleta, identifique este dispositivo e envie uma solicitação ao responsável."}
      </p>

      {!identity || identity.status === "UNREGISTERED" ? (
        <div className="mt-6 space-y-3">
          <div>
            <Label htmlFor="device-display-name">Nome do dispositivo</Label>
            <Input
              id="device-display-name"
              className="mt-2 min-h-12"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              maxLength={100}
              placeholder="Ex.: Tablet da equipe 2"
            />
          </div>
          <Button
            type="button"
            className="min-h-12 w-full"
            disabled={busy || offline}
            onClick={() => void register(displayName).catch(() => undefined)}
          >
            {busy ? <LoaderCircle className="animate-spin" /> : <ShieldCheck />}
            {busy ? "Enviando solicitação..." : "Solicitar cadastro"}
          </Button>
        </div>
      ) : (
        <Button type="button" variant="outline" className="mt-6 min-h-12 w-full" disabled={busy || offline} onClick={() => void refreshStatus()}>
          <RefreshCw /> Verificar autorização
        </Button>
      )}

      {identity ? (
        <dl className="mt-5 rounded-lg bg-slate-50 p-4 text-xs text-slate-600">
          <div className="flex justify-between gap-3"><dt>Identificação</dt><dd className="font-semibold text-slate-900">{identity.displayName}</dd></div>
          {identity.publicKeyFingerprint ? <div className="mt-2 flex justify-between gap-3"><dt>Chave</dt><dd className="font-mono text-slate-900">{identity.publicKeyFingerprint.slice(0, 12)}...</dd></div> : null}
        </dl>
      ) : null}
      {error ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-900" role="alert">{error}</p> : null}
    </section>
  );
}
