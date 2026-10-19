"use client";

import { useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, Clock3, LoaderCircle, RefreshCw } from "lucide-react";

import { useSync } from "@/components/offline/sync-provider";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { getOfflineStore } from "@/offline/indexed-db-store";
import type { OfflineEvent, OfflineEventStatus } from "@/offline/types";

const statusLabels: Record<OfflineEventStatus, string> = {
  PENDING: "Pendente",
  SYNCING: "Sincronizando",
  SYNCED: "Sincronizado",
  FAILED_RETRYABLE: "Tentará novamente",
  FAILED_PERMANENT: "Precisa de revisão",
};

function dateTime(value: string | null) {
  return value ? new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "medium" }).format(new Date(value)) : "—";
}

function eventLabel(event: OfflineEvent) {
  return event.eventType === "READING_CREATED" ? "Leitura" : "Evidência fotográfica";
}

export function SyncQueuePanel() {
  const { authenticationRequired, connectivity, isSyncing, lastSyncAt, snapshot, refresh, syncNow } = useSync();
  const store = useMemo(() => getOfflineStore(), []);
  const [message, setMessage] = useState<string | null>(null);
  const eventByClientId = useMemo(
    () => new Map(snapshot.events.map((event) => [event.clientEventId, event])),
    [snapshot.events],
  );

  async function synchronize() {
    setMessage(null);
    const result = await syncNow(true);
    if (result.authenticationRequired) setMessage("Sessão expirada. Entre novamente para sincronizar.");
    else if (result.synced) setMessage("Sincronização concluída.");
    else if (result.skippedBecauseLocked) setMessage("Outra aba já está sincronizando este dispositivo.");
    else setMessage("A fila foi verificada. Itens com espera ou revisão permanecem salvos.");
  }

  async function retry(event: OfflineEvent) {
    await store.retryEvent(event.localEventId);
    await refresh();
    await synchronize();
  }

  return (
    <div className="space-y-6">
      {(snapshot.otherUserPending > 0) && (
        <div className="rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm text-amber-950" role="alert">
          Existem {snapshot.otherUserPending} eventos pendentes de outro usuário neste dispositivo. Eles não serão enviados nesta sessão.
        </div>
      )}
      {authenticationRequired && (
        <div className="rounded-lg border border-red-300 bg-red-50 p-4 text-sm text-red-900" role="alert">
          Sessão expirada. A fila foi preservada; entre novamente para continuar.
        </div>
      )}

      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Summary label="Pendentes" value={snapshot.pending} icon={Clock3} />
        <Summary label="Sincronizando" value={snapshot.syncing} icon={LoaderCircle} spinning={isSyncing} />
        <Summary label="Sincronizados recentes" value={snapshot.synced} icon={CheckCircle2} />
        <Summary label="Falhas" value={snapshot.retryableFailures + snapshot.permanentFailures} icon={AlertTriangle} />
      </section>

      <section className="rounded-lg border bg-white shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b p-5">
          <div>
            <h2 className="font-semibold text-slate-900">Fila deste usuário</h2>
            <p className="mt-1 text-sm text-slate-500">Conexão: {connectivity === "OFFLINE" ? "offline" : connectivity === "ONLINE" ? "online" : "em verificação"} · última verificação {dateTime(lastSyncAt)}</p>
          </div>
          <Button type="button" onClick={() => void synchronize()} disabled={isSyncing || authenticationRequired}>
            <RefreshCw className={isSyncing ? "animate-spin" : ""} />
            Sincronizar agora
          </Button>
        </div>
        {message && <p className="border-b bg-slate-50 px-5 py-3 text-sm text-slate-700" aria-live="polite">{message}</p>}
        {snapshot.events.length === 0 ? (
          <p className="p-8 text-center text-sm text-slate-500">Nenhum evento offline foi registrado neste dispositivo.</p>
        ) : (
          <ul className="divide-y">
            {[...snapshot.events].reverse().map((event) => (
              <li key={event.localEventId} className="grid gap-3 p-5 md:grid-cols-[minmax(0,1fr)_180px_180px_auto] md:items-center">
                <div>
                  <p className="font-semibold text-slate-900">{eventLabel(event)}</p>
                  <p className="mt-1 text-xs text-slate-500">Salvo em {dateTime(event.createdAtLocal)}</p>
                  {event.lastError && <p className="mt-2 text-sm text-red-700">{event.lastError}</p>}
                </div>
                <div className="text-sm"><span className="text-slate-500">Última tentativa</span><p className="font-medium">{dateTime(event.lastAttemptAt)}</p></div>
                <div><Badge variant="outline">{statusLabels[event.status]}</Badge>{event.eventType === "PHOTO_UPLOADED" && event.status !== "SYNCED" && eventByClientId.get(event.parentReadingClientEventId ?? "")?.status !== "SYNCED" ? <p className="mt-1 text-xs text-slate-500">Aguarda a leitura</p> : null}</div>
                <div>
                  {event.status === "FAILED_RETRYABLE" && (
                    <Button type="button" size="sm" variant="outline" onClick={() => void retry(event)}>Tentar novamente</Button>
                  )}
                  {event.status === "FAILED_PERMANENT" && <span className="text-xs font-medium text-red-700">Revisão necessária</span>}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function Summary({ label, value, icon: Icon, spinning = false }: { label: string; value: number; icon: typeof Clock3; spinning?: boolean }) {
  return <div className="rounded-lg border bg-white p-5 shadow-sm"><div className="flex items-center justify-between"><p className="text-sm font-medium text-slate-500">{label}</p><Icon className={spinning ? "size-5 animate-spin text-slate-500" : "size-5 text-slate-500"} /></div><p className="mt-3 text-3xl font-bold text-slate-900">{value}</p></div>;
}
