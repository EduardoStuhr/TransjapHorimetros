import { utilizationReason } from "@/services/utilization-presentation";
import Link from "next/link";
import { getUtilization } from "@/services/operations.service";
import { filterSearch } from "@/lib/operational-filters";
import type { ReadingFilters } from "@/services/readings.service";

const number = (n: number) => n.toLocaleString("pt-BR", { maximumFractionDigits: 2 });
export async function UtilizationPanel({ filters }: { filters: ReadingFilters }) {
  const report = await getUtilization(filters);
  const search = filterSearch({ ...filters, page: undefined } as Record<string, string | undefined>);
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
  const byDay = new Map<string, number>();
  const warnings = new Map<string, number>();
  for (const item of report.items) {
    const day = item.at.slice(0, 10);
    if (item.hours != null) byDay.set(day, (byDay.get(day) ?? 0) + item.hours);
    else warnings.set(utilizationReason(item.reason), (warnings.get(utilizationReason(item.reason)) ?? 0) + 1);
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
    <p>{report.totalItems} registros · Página {report.page} · Gráficos da página atual</p>
    <nav aria-label="Paginação da utilização" className="flex gap-4">
      {report.page > 1 && <Link className="underline" href={"?" + search + "&page=" + (report.page - 1)}>Anterior</Link>}
      {report.page * report.pageSize < report.totalItems && <Link className="underline" href={"?" + search + "&page=" + (report.page + 1)}>Próxima</Link>}
    </nav>
    <div className="grid gap-4 md:grid-cols-2">
      <Bars title="HOURS calculadas por frota" items={machineHours} />
      <Bars title="HOURS por obra atribuível" items={[...bySite].map(([label, value]) => ({ label, value }))} />
    </div>
    <div className="grid gap-4 md:grid-cols-2">
      <Bars title="HOURS por dia UTC, pela captura final" items={[...byDay].map(([label, value]) => ({ label, value }))} />
      <ul aria-label="Intervalos que exigem atenção">{[...warnings].map(([reason, count]) => <li className="border-b py-2" key={reason}>{reason} · {count}</li>)}</ul>
    </div>
    <div className="max-h-96 space-y-4 overflow-auto">
      {[...machines].map(([fleet, items]) => <details key={fleet}>
        <summary>Frota {fleet} · {items[0].model} · {items.length} registros</summary>
        <Evolution items={items} />
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

function Evolution({ items }: { items: import("@/services/operations.service").UtilizationRow[] }) {
  const values = items.map(i => i.final);
  const min = Math.min(...values);
  const span = Math.max(1, Math.max(...values) - min);
  const start = new Date(items[0].at).getTime();
  const duration = Math.max(1, new Date(items[items.length - 1].at).getTime() - start);
  return <figure className="my-4">
    <figcaption>Horímetro informado · {number(min)} a {number(Math.max(...values))}</figcaption>
    <div className="relative mx-3 h-32 border-b border-l" role="img" aria-label="Capturas ordenadas no tempo. Valores exatos na tabela abaixo.">
      {items.map(i => <span key={i.readingId} className="absolute h-2 w-2 rounded-full bg-blue-700"
        title={new Date(i.at).toISOString() + " · " + number(i.final)}
        style={{ left: (100 * (new Date(i.at).getTime() - start) / duration) + "%", bottom: (95 * (i.final - min) / span) + "%" }} />)}
    </div>
  </figure>;
}
