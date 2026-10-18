import { Badge } from "@/components/ui/badge";
import { formatReadingStatus } from "@/lib/domain-formatters";
import type { MeterUnit, ReadingStatus, ReadingType } from "@/types/domain";

const statusClasses: Record<ReadingStatus, string> = {
  VALIDATED: "border-emerald-200 bg-emerald-50 text-emerald-800",
  PENDING: "border-amber-200 bg-amber-50 text-amber-800",
  SUSPECT: "border-orange-200 bg-orange-50 text-orange-800",
  CORRECTED: "border-blue-200 bg-blue-50 text-blue-800",
  REJECTED: "border-red-200 bg-red-50 text-red-800",
};
export function ReadingStatusBadge({ status }: { status: ReadingStatus }) {
  return <Badge variant="outline" className={statusClasses[status]}>{formatReadingStatus(status)}</Badge>;
}
export function MeterUnitBadge({ unit }: { unit: MeterUnit | null | undefined }) {
  return <Badge variant="outline" className="border-slate-200 bg-slate-50 text-slate-700">{unit === "HOURS" ? "HOURS · Horas" : unit === "KM" ? "KM" : "Unidade histórica não informada"}</Badge>;
}
export function formatReadingType(type: ReadingType) {
  return { OPENING: "Abertura", CLOSING: "Fechamento", INTERMEDIATE: "Intermediária", MAINTENANCE: "Manutenção", CORRECTION: "Proposta de correção" }[type];
}
