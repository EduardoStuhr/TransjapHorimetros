import Link from "next/link";
import { formatAnomalyStatus, formatAnomalyType, formatDateTime, formatHourMeter } from "@/lib/domain-formatters";
import type { HourMeterReading } from "@/types/domain";
import { MeterUnitBadge, ReadingStatusBadge, formatReadingType } from "./reading-badges";
import { ReadingTracePanel } from "./reading-trace-panel";

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return <div><dt className="text-sm text-slate-500">{label}</dt><dd className="mt-1 break-words font-medium text-slate-900">{children}</dd></div>;
}
export function ReadingOperationalDetails({ reading }: { reading: HourMeterReading }) {
  return <div className="space-y-6">
    <div className="grid gap-6 md:grid-cols-2">
      <section className="rounded-lg border bg-white p-5"><h2 className="font-semibold">Leitura</h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Fact label="Valor informado">{formatHourMeter(reading.value)}</Fact>
          <Fact label="Unidade no registro"><MeterUnitBadge unit={reading.meterUnit} /></Fact>
          <Fact label="Tipo">{formatReadingType(reading.readingType)}</Fact>
          <Fact label="Status"><ReadingStatusBadge status={reading.status} /></Fact>
          <Fact label="Diferença para a anterior">{formatHourMeter(reading.differenceFromPrevious)}</Fact>
          <Fact label="Leitura anterior">{reading.previousReadingId ? <Link className="text-[#234f8c] underline" href={`/horimetros/${reading.previousReadingId}`}>Consultar registro anterior</Link> : "Sem referência histórica"}</Fact>
        </dl>
      </section>
      <section className="rounded-lg border bg-white p-5"><h2 className="font-semibold">Máquina e contexto operacional</h2>
        <dl className="mt-4 grid gap-4 sm:grid-cols-2">
          <Fact label="Frota"><Link className="text-[#234f8c] underline" href={`/frota/${reading.machineFleetNumber}`}>{reading.machineFleetNumber}</Link></Fact>
          <Fact label="Modelo">{reading.model}</Fact>
          <Fact label="Obra">{reading.workSiteName ?? "Sem obra associada"}</Fact>
          <Fact label="Informado por">{reading.createdByEmail ?? "Autoria não registrada no histórico"}</Fact>
          <Fact label="Dispositivo informado">{reading.deviceId ?? "Não informado"}</Fact>
        </dl>
      </section>
    </div>
    <section className="rounded-lg border bg-white p-5"><h2 className="font-semibold">Linha do tempo</h2>
      <dl className="mt-4 grid gap-4 sm:grid-cols-3">
        <Fact label="Capturada no dispositivo">{formatDateTime(reading.capturedAtDevice)}</Fact>
        <Fact label="Recebida no servidor">{formatDateTime(reading.receivedAtServer)}</Fact>
        <Fact label="Sincronizada">{formatDateTime(reading.syncedAt)}</Fact>
      </dl>
    </section>
    <section className="rounded-lg border bg-white p-5"><h2 className="font-semibold">Anomalias</h2>
      <p className="mt-2 text-sm text-slate-600">{reading.openAnomalyCount} aberta(s) · {reading.anomalyCount} registrada(s)</p>
      {reading.anomalies?.length ? <ul className="mt-4 divide-y">{reading.anomalies.map(a => <li key={a.id} className="py-3"><p className="font-medium">{formatAnomalyType(a.type)} <span className="text-sm font-normal text-slate-500">· {formatAnomalyStatus(a.status)}</span></p><p className="mt-1 text-sm text-slate-600">{a.description}</p></li>)}</ul> : <p className="mt-3 text-sm text-slate-500">Nenhuma anomalia registrada.</p>}
    </section>
    {(reading.correctionOfReadingId || reading.correctionCount > 0) && <section className="rounded-lg border bg-white p-5"><h2 className="font-semibold">Correções vinculadas</h2>
      <p className="mt-2 text-sm text-slate-600">Propostas de correção preservam o original e aguardam revisão. O valor operacional não é substituído automaticamente.</p>
      {reading.correctionOfReadingId && <Link className="mt-3 block text-sm text-[#234f8c] underline" href={`/horimetros/${reading.correctionOfReadingId}`}>Consultar leitura original</Link>}
      <ul className="mt-3 space-y-3">{reading.corrections?.map(c => <li key={c.id} className="flex flex-wrap items-center gap-3"><Link className="text-[#234f8c] underline" href={`/horimetros/${c.id}`}>Proposta: {formatHourMeter(c.value)}</Link><ReadingStatusBadge status={c.status} /><span className="text-sm text-slate-500">{formatDateTime(c.createdAt)}</span></li>)}</ul>
    </section>}
    <ReadingTracePanel eventId={reading.clientEventId} actorId={reading.createdByUserId} createdAt={reading.createdAt} updatedAt={reading.updatedAt} />
  </div>;
}
