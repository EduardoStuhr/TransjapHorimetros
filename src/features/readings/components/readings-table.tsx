import Link from "next/link";
import { ExternalLink, Gauge, ImageIcon } from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { MeterUnitBadge, ReadingStatusBadge, formatReadingType } from "./reading-badges";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  formatDateTime,
  formatHourMeter,
} from "@/lib/domain-formatters";
import type { HourMeterReading } from "@/types/domain";

const headers = [
  "Data/Hora",
  "Frota",
  "Modelo",
  "Valor informado",
  "Unidade",
  "Tipo",
  "Diferença",
  "Obra",
  "Status",
  "Foto",
  "Alertas",
  "Ações",
];

export function ReadingsTable({
  emptyDescription = "Nenhuma leitura foi registrada pela API.",
  readings,
}: {
  emptyDescription?: string;
  readings: readonly HourMeterReading[];
}) {
  return (
    <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="overflow-x-auto">
        <Table>
          <TableHeader className="bg-slate-100">
            <TableRow>
              {headers.map((header) => (
                <TableHead
                  key={header}
                  className="h-12 whitespace-nowrap text-xs font-bold uppercase tracking-wide text-slate-600"
                >
                  {header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {readings.length === 0 ? (
              <TableRow>
                <TableCell colSpan={headers.length} className="p-0">
                  <EmptyState
                    icon={Gauge}
                    title="Nenhum horímetro registrado até o momento"
                    description={emptyDescription}
                  />
                </TableCell>
              </TableRow>
            ) : (
              readings.map((reading) => (
                <TableRow key={reading.id}>
                  <TableCell className="whitespace-nowrap">
                    {formatDateTime(reading.capturedAtDevice)}
                  </TableCell>
                  <TableCell>
                    <Link
                      href={"/frota/" + reading.machineFleetNumber}
                      className="font-bold text-[#234f8c] hover:underline"
                    >
                      {reading.machineFleetNumber}
                    </Link>
                  </TableCell>
                  <TableCell className="min-w-52">{reading.model}</TableCell>
                  <TableCell>{formatHourMeter(reading.value)}</TableCell>
                  <TableCell><MeterUnitBadge unit={reading.meterUnit} /></TableCell>
                  <TableCell className="whitespace-nowrap">{formatReadingType(reading.readingType)}</TableCell>
                  <TableCell>
                    {formatHourMeter(reading.differenceFromPrevious)}
                  </TableCell>
                  <TableCell>{reading.workSiteName ?? "Sem registro"}</TableCell>
                  <TableCell>
                    <ReadingStatusBadge status={reading.status} />
                  </TableCell>
                  <TableCell>
                    {reading.hasPhotoEvidence ? (
                      <Button asChild variant="ghost" size="icon-sm">
                        <Link
                          href={`/horimetros/${reading.id}`}
                          aria-label="Abrir foto da leitura"
                        >
                          <ImageIcon />
                        </Link>
                      </Button>
                    ) : (
                      <span className="text-slate-500">Sem foto</span>
                    )}
                  </TableCell>
                  <TableCell aria-label={`${reading.openAnomalyCount} anomalias abertas de ${reading.anomalyCount}`}>
                    {reading.anomalyCount ? `${reading.openAnomalyCount} / ${reading.anomalyCount}` : "—"}
                  </TableCell>
                  <TableCell>
                    <Button asChild variant="ghost" size="icon-sm">
                      <Link
                        href={`/horimetros/${reading.id}`}
                        aria-label={
                          "Abrir leitura da frota " + reading.machineFleetNumber
                        }
                      >
                        <ExternalLink />
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
