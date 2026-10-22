"use client";

import { useCallback, useEffect, useState } from "react";
import { CheckCircle2, KeyRound, LoaderCircle, RefreshCw, Search, ShieldX, TabletSmartphone } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { PagedDevicesResponse, RegisteredDeviceAuditResponse, RegisteredDeviceResponse } from "@/features/device-identity/types";

const statusLabels = { PENDING: "Pendente", AUTHORIZED: "Autorizado", REVOKED: "Revogado" } as const;

function formatDate(value: string | null) {
  return value ? new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value)) : "—";
}

async function readResponse<T>(response: Response): Promise<T> {
  const body = await response.json().catch(() => null) as Record<string, unknown> | null;
  if (!response.ok) throw new Error(typeof body?.detail === "string" ? body.detail : "Operação não concluída.");
  return body as T;
}

export function DeviceManagement() {
  const [devices, setDevices] = useState<RegisteredDeviceResponse[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [status, setStatus] = useState("");
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [history, setHistory] = useState<RegisteredDeviceAuditResponse[] | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({ page: "1", pageSize: "100" });
      if (status) params.set("status", status);
      if (search.trim()) params.set("search", search.trim());
      const response = await readResponse<PagedDevicesResponse>(await fetch(`/api/devices?${params}`, { cache: "no-store" }));
      setDevices(response.items);
      const nextSelected = response.items.find((device) => device.id === selectedId) ?? response.items[0] ?? null;
      setSelectedId(nextSelected?.id ?? null);
      setRenameValue(nextSelected?.displayName ?? "");
      setHistory(null);
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar os dispositivos.");
    } finally {
      setLoading(false);
    }
  }, [search, selectedId, status]);

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeout);
  }, [load]);
  const selected = devices.find((device) => device.id === selectedId) ?? null;

  async function mutate(path: string, method = "POST", body?: object) {
    setBusy(true);
    setError(null);
    try {
      await readResponse<RegisteredDeviceResponse>(await fetch(path, {
        method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined,
      }));
      await load();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Operação não concluída.");
    } finally {
      setBusy(false);
    }
  }

  async function loadHistory(deviceId: string) {
    try {
      setHistory(await readResponse<RegisteredDeviceAuditResponse[]>(
        await fetch(`/api/devices/${deviceId}/history`, { cache: "no-store" }),
      ));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Não foi possível carregar o histórico.");
    }
  }

  return (
    <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_380px]">
      <section className="rounded-xl border bg-white shadow-sm">
        <div className="grid gap-3 border-b p-4 sm:grid-cols-[1fr_190px_auto]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-3.5 size-4 text-slate-400" />
            <Input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nome, e-mail, ID ou chave" className="min-h-11 pl-9" />
          </div>
          <select value={status} onChange={(event) => setStatus(event.target.value)} className="min-h-11 rounded-md border border-slate-300 bg-white px-3 text-sm">
            <option value="">Todos os status</option><option value="PENDING">Pendentes</option><option value="AUTHORIZED">Autorizados</option><option value="REVOKED">Revogados</option>
          </select>
          <Button type="button" variant="outline" className="min-h-11" onClick={() => void load()}><RefreshCw /> Atualizar</Button>
        </div>
        {loading ? <div className="grid min-h-52 place-items-center text-slate-500"><LoaderCircle className="animate-spin" /></div> : devices.length === 0 ? (
          <div className="p-10 text-center text-slate-500">Nenhum dispositivo encontrado.</div>
        ) : <div className="divide-y">{devices.map((device) => (
          <button key={device.id} type="button" onClick={() => { setSelectedId(device.id); setRenameValue(device.displayName); setHistory(null); }} className={`grid w-full gap-2 p-4 text-left transition sm:grid-cols-[1fr_150px_150px] ${selectedId === device.id ? "bg-blue-50" : "hover:bg-slate-50"}`}>
            <span className="min-w-0"><span className="block truncate font-bold text-slate-950">{device.displayName}</span><span className="block truncate text-xs text-slate-500">{device.requestedByEmail}</span></span>
            <span><Badge variant="outline">{statusLabels[device.status]}</Badge></span>
            <span className="text-xs text-slate-500">Solicitado {formatDate(device.requestedAt)}</span>
          </button>
        ))}</div>}
      </section>

      <aside className="rounded-xl border bg-white p-5 shadow-sm xl:sticky xl:top-24">
        {selected ? <div>
          <div className="flex items-start gap-3"><span className="grid size-11 shrink-0 place-items-center rounded-lg bg-blue-950 text-white"><TabletSmartphone /></span><div className="min-w-0"><h2 className="truncate text-lg font-black text-slate-950">{selected.displayName}</h2><p className="text-xs text-slate-500">{selected.platform} · {statusLabels[selected.status]}</p></div></div>
          <dl className="mt-5 space-y-3 text-sm">
            <div><dt className="text-xs font-semibold uppercase text-slate-500">ID local legado</dt><dd className="mt-1 break-all font-mono text-xs">{selected.localDeviceId}</dd></div>
            <div><dt className="text-xs font-semibold uppercase text-slate-500">Fingerprint SHA-256</dt><dd className="mt-1 break-all rounded bg-slate-50 p-2 font-mono text-xs">{selected.publicKeyFingerprint}</dd></div>
            <div><dt className="text-xs font-semibold uppercase text-slate-500">Algoritmo</dt><dd>{selected.keyAlgorithm}</dd></div>
            <div><dt className="text-xs font-semibold uppercase text-slate-500">Solicitação</dt><dd>{selected.requestedByEmail}<br />{formatDate(selected.requestedAt)}</dd></div>
            <div><dt className="text-xs font-semibold uppercase text-slate-500">Autorização</dt><dd>{selected.authorizedByEmail ?? "—"}<br />{formatDate(selected.authorizedAt)}</dd></div>
            <div><dt className="text-xs font-semibold uppercase text-slate-500">Revogação</dt><dd>{selected.revokedByEmail ?? "—"}<br />{formatDate(selected.revokedAt)}</dd></div>
            <div><dt className="text-xs font-semibold uppercase text-slate-500">Última comprovação</dt><dd>{formatDate(selected.lastSeenAt)}</dd></div>
          </dl>
          <div className="mt-5 border-t pt-5"><Label htmlFor="rename-device">Nome administrativo</Label><div className="mt-2 flex gap-2"><Input id="rename-device" value={renameValue} maxLength={100} onChange={(event) => setRenameValue(event.target.value)} /><Button type="button" variant="outline" disabled={busy || !renameValue.trim()} onClick={() => void mutate(`/api/devices/${selected.id}`, "PATCH", { displayName: renameValue })}>Salvar</Button></div></div>
          <div className="mt-4 border-t pt-4">
            <Button type="button" variant="ghost" className="w-full" onClick={() => void loadHistory(selected.id)}>Carregar histórico auditável</Button>
            {history ? <ol className="mt-2 space-y-2 text-xs">{history.map((event, index) => <li key={`${event.action}-${event.createdAt}-${index}`} className="rounded bg-slate-50 p-2"><strong>{event.action}</strong><br /><span className="text-slate-500">{formatDate(event.createdAt)} · {event.userEmail ?? "sistema"}</span></li>)}</ol> : null}
          </div>
          {selected.status === "PENDING" ? <Button type="button" className="mt-4 min-h-12 w-full" disabled={busy} onClick={() => void mutate(`/api/devices/${selected.id}/authorize`)}><CheckCircle2 /> Autorizar dispositivo</Button> : null}
          {selected.status !== "REVOKED" ? <Button type="button" variant="destructive" className="mt-2 min-h-12 w-full" disabled={busy} onClick={() => { if (window.confirm("Revogar este dispositivo? A ação preserva o histórico e não reautoriza automaticamente.")) void mutate(`/api/devices/${selected.id}/revoke`); }}><ShieldX /> Revogar dispositivo</Button> : null}
        </div> : <div className="py-12 text-center text-slate-500"><KeyRound className="mx-auto mb-3" />Selecione um dispositivo.</div>}
        {error ? <p className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-900" role="alert">{error}</p> : null}
      </aside>
    </div>
  );
}
