import Link from "next/link";
import { connection } from "next/server";
import {
  BellRing,
  CircleGauge,
  ClipboardClock,
  FileSearch,
  HardHat,
  History,
  MapPinned,
  RadioTower,
  TriangleAlert,
} from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { PageHeading } from "@/components/shared/page-heading";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { MetricCard } from "@/features/dashboard/components/metric-card";
import { ReadingsTable } from "@/features/readings/components/readings-table";
import { formatAnomalyType } from "@/lib/domain-formatters";
import { getAnomalies } from "@/services/anomalies.service";
import { getDashboardSummary } from "@/services/dashboard.service";
import { getReadings } from "@/services/readings.service";

export default async function DashboardPage() {
  await connection();
  const [summary, readings, anomalies] = await Promise.all([
    getDashboardSummary(),
    getReadings(),
    getAnomalies(),
  ]);

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Visão geral"
        title="Controle operacional da frota"
        description="Acompanhe o cadastro de máquinas, as leituras e as inconsistências registradas no banco operacional."
      />

      <section aria-labelledby="indicadores-title">
        <h3 id="indicadores-title" className="sr-only">
          Indicadores
        </h3>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-6">
          <MetricCard
            label="Total de máquinas"
            value={summary.totalMachines}
            helper="Cadastro real no banco operacional"
            icon={HardHat}
            emphasized
          />
          <MetricCard
            label="Atualizadas hoje"
            value={summary.updatedToday}
            helper="Recebidas pelo servidor hoje"
            icon={RadioTower}
          />
          <MetricCard
            label="Sem leitura"
            value={summary.withoutReading}
            helper={
              summary.withoutReadingIsDefined
                ? "Conforme regra operacional"
                : "Regra diária não definida"
            }
            icon={ClipboardClock}
          />
          <MetricCard
            label="Leituras pendentes"
            value={summary.pendingReadings}
            helper="Aguardando validação"
            icon={History}
          />
          <MetricCard
            label="Leituras suspeitas"
            value={summary.suspectReadings}
            helper="Exigem revisão humana"
            icon={TriangleAlert}
          />
          <MetricCard
            label="Alertas"
            value={summary.alerts}
            helper="Anomalias abertas"
            icon={BellRing}
          />
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,0.65fr)]">
        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <CardTitle className="text-base">Últimas leituras</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {readings.length === 0 ? (
              <EmptyState
                icon={CircleGauge}
                title="Nenhuma leitura registrada"
                description="Nenhum horímetro foi recebido pela API até o momento."
                compact
              />
            ) : (
              <ReadingsTable readings={readings.slice(0, 5)} />
            )}
          </CardContent>
        </Card>

        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <CardTitle className="text-base">Atalhos rápidos</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-2 p-4">
            <Button asChild variant="outline" className="h-11 justify-start">
              <Link href="/frota">
                <HardHat />
                Consultar frota
              </Link>
            </Button>
            <Button asChild variant="outline" className="h-11 justify-start">
              <Link href="/horimetros">
                <FileSearch />
                Consultar horímetros
              </Link>
            </Button>
            <Button asChild variant="outline" className="h-11 justify-start">
              <Link href="/obras">
                <MapPinned />
                Consultar obras
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <CardTitle className="text-base">Alertas recentes</CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            {anomalies.length === 0 ? (
              <EmptyState
                icon={BellRing}
                title="Nenhum alerta identificado"
                description="Nenhuma anomalia aberta foi detectada nas leituras."
                compact
              />
            ) : (
              <div className="divide-y divide-slate-100">
                {anomalies.slice(0, 5).map((anomaly) => (
                  <div key={anomaly.id} className="px-5 py-4">
                    <p className="text-sm font-bold text-slate-900">
                      Frota {anomaly.machineFleetNumber} ·{" "}
                      {formatAnomalyType(anomaly.type)}
                    </p>
                    <p className="mt-1 text-sm text-slate-600">
                      {anomaly.description}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <CardTitle className="text-base">
              Máquinas sem atualização
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
            <EmptyState
              icon={ClipboardClock}
              title="Métrica ainda não definida"
              description={summary.withoutReadingDefinition}
              compact
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
