import { UtilizationPanel } from "./utilization-panel";
import Link from "next/link";
import { OperationalFilters } from "@/components/shared/operational-filters";
import { parseOperationalFilters } from "@/lib/operational-filters";
import { getOperationalSummary } from "@/services/operations.service";

export async function OperationalSummaryPanel({
  params,
}: {
  params: Record<string, string | string[] | undefined>;
}) {
  let filters: ReturnType<typeof parseOperationalFilters>;
  try {
    filters = parseOperationalFilters(params);
  } catch {
    return <div role="alert" className="rounded border p-4">Revise o período e os identificadores informados nos filtros. <Link className="underline" href="/">Limpar filtros</Link></div>;
  }
  const summary = await getOperationalSummary(filters);
  const metrics = [
    ["Máquinas cadastradas", summary.totalMachines],
    ["Máquinas ativas", summary.activeMachines],
    ["Com leituras no período", summary.machinesWithReadings],
    ["Leituras aprovadas", summary.approvedReadings],
    ["Sem captura nos últimos 7 dias UTC", summary.withoutRecentReading],
    ["Leituras no período", summary.readings],
    ["Recebidas hoje (UTC)", summary.receivedToday],
    ["Aguardando revisão", summary.pendingReview],
    ["Suspeitas", summary.suspectReadings],
    ["Anomalias abertas", summary.openAnomalies],
  ] as const;
  return (
    <section className="space-y-4" aria-label="Supervisão operacional">
      <h2 className="text-lg font-bold">Supervisão operacional</h2>
      <OperationalFilters filters={filters} />
      <UtilizationPanel filters={{ ...filters, page: typeof params.page === "string" && /^[1-9]\d{0,5}$/.test(params.page) ? params.page : "1" }} />
      <div className="grid gap-3 sm:grid-cols-4">
        {metrics.map(([label, value]) => (
          <div className="rounded border bg-white p-4" key={label}>
            <p>{label}</p>
            <strong className="text-2xl">{value}</strong>
          </div>
        ))}
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        {[
          ["Leituras por obra (até 100)", summary.byWorkSite],
          ["Leituras por dia UTC (até 366 dias)", summary.byDay],
        ].map(([title, items]) => (
          <div key={String(title)} className="rounded border p-4">
            <h3 className="font-bold">{String(title)}</h3>
            <ul className="max-h-64 overflow-auto">
              {(items as typeof summary.byDay).map((i) => (
                <li
                  className="flex justify-between border-b py-2"
                  key={i.label}
                >
                  <span>{i.label}</span>
                  <strong>{i.count}</strong>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="rounded border p-4">
        <h3 className="font-bold">
          Máquinas ativas com recebimento mais antigo
        </h3>
        <p className="text-sm">
          Histórico global das máquinas selecionadas. Ausência de atualização
          requer análise operacional.
        </p>
        <ul className="max-h-64 overflow-auto">
          {summary.oldestMachines.map((m) => (
            <li key={m.fleetNumber} className="border-b py-2">
              Frota {m.fleetNumber} · {m.model} ·{" "}
              {m.lastReceivedAt
                ? new Date(m.lastReceivedAt).toLocaleString("pt-BR", {
                    timeZone: "UTC",
                  }) + " UTC"
                : "Sem leitura"}
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
