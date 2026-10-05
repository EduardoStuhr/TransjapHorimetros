import type { Metadata } from "next";
import { BellRing, ShieldCheck } from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { PageHeading } from "@/components/shared/page-heading";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { anomalyDefinitions } from "@/features/anomalies/anomaly-definitions";
import { getAnomalies } from "@/services/anomalies.service";

export const metadata: Metadata = {
  title: "Alertas",
};

export default async function AlertsPage() {
  const anomalies = await getAnomalies();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Confiabilidade"
        title="Alertas"
        description="Revise futuras inconsistências de leitura sem substituir a evidência nem a análise humana."
      />

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.2fr)_minmax(340px,0.8fr)]">
        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <CardTitle className="text-base">Alertas identificados</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {anomalies.length === 0 ? (
              <EmptyState
                icon={ShieldCheck}
                title="Nenhum alerta identificado"
                description="Os alertas aparecerão quando o motor de regras processar leituras recebidas pela API."
              />
            ) : null}
          </CardContent>
        </Card>

        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <CardTitle className="flex items-center gap-2 text-base">
              <BellRing className="size-4 text-[#2c5b9e]" />
              Tipos monitorados
            </CardTitle>
          </CardHeader>
          <CardContent className="divide-y divide-slate-100 p-0">
            {anomalyDefinitions.map((definition) => (
              <div key={definition.type} className="px-5 py-4">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="text-sm font-bold text-slate-900">
                    {definition.label}
                  </p>
                  <Badge variant="outline" className="text-[11px]">
                    {definition.type}
                  </Badge>
                </div>
                <p className="mt-1 text-sm leading-6 text-slate-600">
                  {definition.description}
                </p>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
