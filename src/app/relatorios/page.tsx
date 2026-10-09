import type { Metadata } from "next";
import Link from "next/link";
import { connection } from "next/server";
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
import {
  getReportAvailability,
  reportDefinitions,
} from "@/services/reports.service";

export const metadata: Metadata = {
  title: "Relatórios",
};

const reportIcons = {
  readings: Gauge,
  machines: HardHat,
  worksites: Building2,
  anomalies: AlertTriangle,
} as const;

export default async function ReportsPage() {
  await connection();
  const availability = await getReportAvailability();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Análise"
        title="Relatórios"
        description="Exporte em Excel ou PDF os dados operacionais disponíveis na API."
      />

      <div className="grid gap-4 md:grid-cols-2">
        {reportDefinitions.map((report) => {
          const Icon = reportIcons[report.type];
          const itemCount =
            availability.find((item) => item.type === report.type)?.count ?? 0;
          const canExport = itemCount > 0;

          return (
            <Card key={report.type} className="gap-4 border-slate-200 shadow-sm">
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
                <Badge variant={canExport ? "default" : "secondary"}>
                  {canExport ? `${itemCount} registros` : "Sem dados"}
                </Badge>
              </CardHeader>
              <CardContent>
                <div className="flex flex-wrap gap-2">
                  {canExport ? (
                    <>
                      <Button asChild variant="outline">
                        <Link href={`/api/reports/${report.type}/xlsx`}>
                          <FileSpreadsheet />
                          Exportar Excel
                        </Link>
                      </Button>
                      <Button asChild variant="outline">
                        <Link href={`/api/reports/${report.type}/pdf`}>
                          <FileDown />
                          Exportar PDF
                        </Link>
                      </Button>
                    </>
                  ) : (
                    <>
                      <Button variant="outline" disabled>
                        <FileSpreadsheet />
                        Exportar Excel
                      </Button>
                      <Button variant="outline" disabled>
                        <FileDown />
                        Exportar PDF
                      </Button>
                    </>
                  )}
                </div>
                {!canExport ? (
                  <p className="mt-3 text-sm text-slate-600">
                    Não há dados reais disponíveis para exportar.
                  </p>
                ) : null}
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
