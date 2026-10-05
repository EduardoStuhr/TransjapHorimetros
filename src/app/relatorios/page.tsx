import type { Metadata } from "next";
import {
  AlertTriangle,
  Building2,
  FileDown,
  FileSpreadsheet,
  Gauge,
  HardHat,
} from "lucide-react";

import { PageHeading } from "@/components/shared/page-heading";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getReportAvailability } from "@/services/reports.service";

export const metadata: Metadata = {
  title: "Relatórios",
};

const reportTypes = [
  {
    title: "Relatório de horímetros",
    description: "Leituras por período, status e evolução de horas.",
    icon: Gauge,
  },
  {
    title: "Relatório por máquina",
    description: "Histórico consolidado de uma máquina da frota.",
    icon: HardHat,
  },
  {
    title: "Relatório por obra",
    description: "Leituras agrupadas pelo local de operação.",
    icon: Building2,
  },
  {
    title: "Relatório de inconsistências",
    description: "Alertas e leituras que exigem revisão humana.",
    icon: AlertTriangle,
  },
] as const;

export default async function ReportsPage() {
  const availability = await getReportAvailability();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Análise"
        title="Relatórios"
        description="Estrutura preparada para consultas operacionais e exportações após o recebimento dos primeiros dados."
      />

      <div className="grid gap-4 md:grid-cols-2">
        {reportTypes.map((report) => {
          const Icon = report.icon;

          return (
            <Card
              key={report.title}
              className="gap-4 border-slate-200 shadow-sm"
            >
              <CardHeader className="flex-row items-start justify-between gap-4">
                <div className="flex gap-3">
                  <div className="flex size-10 shrink-0 items-center justify-center rounded-md bg-blue-50 text-[#2c5b9e]">
                    <Icon className="size-5" />
                  </div>
                  <div>
                    <CardTitle className="text-base">{report.title}</CardTitle>
                    <p className="mt-1 text-sm leading-6 text-slate-600">
                      {report.description}
                    </p>
                  </div>
                </div>
                <Badge variant="secondary">Preparado</Badge>
              </CardHeader>
              <CardContent className="flex flex-wrap gap-2">
                <Button variant="outline" disabled={!availability.canExport}>
                  <FileSpreadsheet />
                  Exportar Excel
                </Button>
                <Button variant="outline" disabled={!availability.canExport}>
                  <FileDown />
                  Exportar PDF
                </Button>
              </CardContent>
            </Card>
          );
        })}
      </div>

      <div
        role="status"
        className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-950"
      >
        {availability.reason}
      </div>
    </div>
  );
}
