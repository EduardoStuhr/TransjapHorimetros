"use client";

import { useAuth } from "@/components/auth/auth-provider";
import { formatDateTime } from "@/lib/domain-formatters";

export function ReadingTracePanel({ eventId, actorId, createdAt, updatedAt }: {
  eventId: string; actorId: string | null; createdAt: string; updatedAt: string;
}) {
  const { user } = useAuth();
  if (!user.roles.some(r => ["SUPERVISOR", "ADMINISTRATIVE", "MANAGER", "ADMIN"].includes(r))) return null;
  return <section className="rounded-lg border bg-white p-5">
    <h2 className="font-semibold">Rastreabilidade do registro</h2>
    <dl className="mt-4 grid gap-4 break-all text-sm sm:grid-cols-2">
      <div><dt className="text-slate-500">Evento do cliente</dt><dd>{eventId}</dd></div>
      <div><dt className="text-slate-500">Usuário autenticado</dt><dd>{actorId ?? "Não registrado no histórico"}</dd></div>
      <div><dt className="text-slate-500">Criado em</dt><dd>{formatDateTime(createdAt)}</dd></div>
      <div><dt className="text-slate-500">Última gravação do registro</dt><dd>{formatDateTime(updatedAt)}</dd></div>
    </dl>
  </section>;
}
