"use client";

import Link from "next/link";
import { AlertTriangle, Cloud, CloudOff, LoaderCircle } from "lucide-react";

import { useSync } from "@/components/offline/sync-provider";
import { Badge } from "@/components/ui/badge";

export function SyncStatusIndicator() {
  const { authenticationRequired, connectivity, isSyncing, snapshot } = useSync();
  const failures = snapshot.retryableFailures + snapshot.permanentFailures;
  const pending = snapshot.pending + snapshot.retryableFailures;
  const label = authenticationRequired
    ? "Sessão expirada"
    : isSyncing
      ? "Sincronizando"
      : failures
        ? "Erro de sincronização"
        : connectivity === "OFFLINE"
          ? "Offline"
          : pending
            ? `${pending} pendência${pending === 1 ? "" : "s"}`
            : connectivity === "ONLINE"
              ? "Online"
              : "Verificando";
  const Icon = authenticationRequired || failures
    ? AlertTriangle
    : isSyncing
      ? LoaderCircle
      : connectivity === "OFFLINE"
        ? CloudOff
        : Cloud;

  return (
    <Link href="/sincronizacao" aria-label={`Abrir sincronização. Estado: ${label}`}>
      <Badge
        variant="outline"
        className={
          authenticationRequired || failures
            ? "gap-1.5 border-red-300 bg-red-50 text-red-800"
            : connectivity === "OFFLINE"
              ? "gap-1.5 border-amber-300 bg-amber-50 text-amber-900"
              : "gap-1.5 border-emerald-300 bg-emerald-50 text-emerald-800"
        }
      >
        <Icon className={isSyncing ? "size-3.5 animate-spin" : "size-3.5"} />
        <span className="hidden sm:inline">{label}</span>
        <span className="sm:hidden">{pending || failures || label}</span>
      </Badge>
    </Link>
  );
}
