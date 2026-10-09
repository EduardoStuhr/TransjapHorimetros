import Link from "next/link";
import { ExternalLink, Gauge, ImageIcon } from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { Badge } from "@/components/ui/badge";
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
  formatReadingStatus,
} from "@/lib/domain-formatters";
import type { HourMeterReading } from "@/types/domain";

const headers = [
  "Data/Hora",
  "Frota",
  "Modelo",
  "Horímetro",
  "Diferença",
  "Operador",
  "Obra",
  "Status",
  "Foto",
  "Ações",
];

export function ReadingsTable({
  emptyDescription = "Nenhuma leitura foi registrada pela API.",
  readings,
}: {
  emptyDescription?: string;
  readings: readonly HourMeterReading[];
}) {
  const statusClassNames = {
    VALIDATED: "border-emerald-200 bg-emerald-50 text-emerald-800",
    PENDING: "border-amber-200 bg-amber-50 text-amber-800",
    SUSPECT: "border-orange-200 bg-orange-50 text-orange-800",
    CORRECTED: "border-blue-200 bg-blue-50 text-blue-800",
    REJECTED: "border-red-200 bg-red-50 text-red-800",
  } as const;

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
                  <TableCell>
                    {formatHourMeter(reading.differenceFromPrevious)}
                  </TableCell>
                  <TableCell>{reading.operatorName ?? "Sem registro"}</TableCell>
                  <TableCell>{reading.workSiteName ?? "Sem registro"}</TableCell>
                  <TableCell>
                    <Badge
                      variant="outline"
                      className={statusClassNames[reading.status]}
                    >
                      {formatReadingStatus(reading.status)}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {reading.photoEvidenceUrl ? (
                      <Button asChild variant="ghost" size="icon-sm">
                        <a
                          href={reading.photoEvidenceUrl}
                          target="_blank"
                          rel="noreferrer"
                          aria-label="Abrir foto da leitura"
                        >
                          <ImageIcon />
                        </a>
                      </Button>
                    ) : (
                      <span className="text-slate-500">Sem foto</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <Button asChild variant="ghost" size="icon-sm">
                      <Link
                        href={"/frota/" + reading.machineFleetNumber}
                        aria-label={
                          "Abrir frota " + reading.machineFleetNumber
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
