import Link from "next/link";
import { getUtilization } from "@/services/operations.service";
import { filterSearch } from "@/lib/operational-filters";
import type { ReadingFilters } from "@/services/readings.service";

const number = (n: number) => n.toLocaleString("pt-BR", { maximumFractionDigits: 2 });
export async function UtilizationPanel({ filters }: { filters: ReadingFilters }) {
  const report = await getUtilization(filters);
  const search = filterSearch(filters as Record<string, string | undefined>);
  const machines = new Map<number, typeof report.items>();
  for (const item of report.items) {
    const group = machines.get(item.fleetNumber) ?? [];
    group.push(item);
    machines.set(item.fleetNumber, group);
  }
  return <section className="space-y-4 rounded border bg-white p-4" aria-label="Utilização calculável">
    <h3 className="text-lg font-bold">Utilização calculável</h3>
    <p>Somente intervalos completos entre leituras aceitas, em HOURS. Período padrão de 30 dias em UTC.</p>
    <p className="text-2xl font-bold">{report.hours == null ? "Horas indisponíveis" : number(report.hours) + " HOURS"}</p>
    <p>{report.calculatedIntervals} intervalos calculados · {report.unavailableIntervals} sem dados suficientes</p>
    <div className="flex gap-4">
      <a className="underline" href={"/api/reports/utilization/xlsx?" + search}>Excel</a>
      <a className="underline" href={"/api/reports/utilization/pdf?" + search}>PDF</a>
    </div>
    {report.items.length === 0 && <p>Sem leituras para os filtros informados.</p>}
    <div className="max-h-96 space-y-4 overflow-auto">
      {[...machines].map(([fleet, items]) => <details key={fleet}>
        <summary>Frota {fleet} · {items[0].model} · {items.length} registros</summary>
        <table className="w-full text-left text-sm">
          <caption>Evolução cronológica · HOURS calculáveis</caption>
          <thead><tr><th>Captura UTC</th><th>Horímetro</th><th>Horas</th><th>Obra</th><th>Apuração</th></tr></thead>
          <tbody>{items.map(i => <tr className="border-b" key={i.readingId}>
            <td><Link className="underline" href={"/horimetros/" + i.readingId}>{new Date(i.at).toLocaleString("pt-BR", { timeZone: "UTC" })}</Link></td>
            <td>{number(i.final)}</td><td>{i.hours == null ? "Indisponível" : number(i.hours)}</td>
            <td>{i.workSite ?? "Não atribuída"}</td><td>{i.reason}</td>
          </tr>)}</tbody>
        </table>
      </details>)}
    </div>
  </section>;
}
