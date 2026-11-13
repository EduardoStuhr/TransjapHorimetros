import { utilizationReason } from "@/services/utilization-presentation";
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
  const bySite = new Map<string, number>();
  for (const i of report.items) {
    if (i.hours != null) bySite.set(i.workSite ?? "Sem atribuição", (bySite.get(i.workSite ?? "Sem atribuição") ?? 0) + i.hours);
  }
  const machineHours = [...machines].map(([fleet, items]) => ({
    label: "Frota " + fleet, value: items.reduce((sum, i) => sum + (i.hours ?? 0), 0),
  }));
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
    <div className="grid gap-4 md:grid-cols-2">
      <Bars title="HOURS calculadas por frota" items={machineHours} />
      <Bars title="HOURS por obra atribuível" items={[...bySite].map(([label, value]) => ({ label, value }))} />
    </div>
    <div className="max-h-96 space-y-4 overflow-auto">
      {[...machines].map(([fleet, items]) => <details key={fleet}>
        <summary>Frota {fleet} · {items[0].model} · {items.length} registros</summary>
        <table className="w-full text-left text-sm">
          <caption>Evolução cronológica · HOURS calculáveis</caption>
          <thead><tr><th>Captura UTC</th><th>Horímetro</th><th>Horas</th><th>Obra</th><th>Apuração</th></tr></thead>
          <tbody>{items.map(i => <tr className="border-b" key={i.readingId}>
            <td><Link className="underline" href={"/horimetros/" + i.readingId}>{new Date(i.at).toLocaleString("pt-BR", { timeZone: "UTC" })}</Link></td>
            <td>{number(i.final)}</td><td>{i.hours == null ? "Indisponível" : number(i.hours)}</td>
            <td>{i.workSite ?? "Não atribuída"}</td><td>{utilizationReason(i.reason)}</td>
          </tr>)}</tbody>
        </table>
      </details>)}
    </div>
  </section>;
}


function Bars({ title, items }: { title: string; items: { label: string; value: number }[] }) {
  const maximum = Math.max(1, ...items.map(i => i.value));
  return <figure>
    <figcaption className="font-bold">{title}</figcaption>
    {items.length === 0 && <p>Sem intervalos calculáveis.</p>}
    <ul className="max-h-64 overflow-auto">{items.map(i => <li key={i.label} className="my-2" title={i.label + ": " + number(i.value) + " HOURS"}>
      <div className="flex justify-between"><span>{i.label}</span><span>{number(i.value)}</span></div>
      <div className="h-2 rounded bg-slate-100" aria-hidden="true"><div className="h-2 rounded bg-blue-700" style={{ width: (100 * i.value / maximum) + "%" }} /></div>
    </li>)}</ul>
  </figure>;
}
