import Link from "next/link";
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
import { getAnomalies } from "@/services/anomalies.service";
import { getMachines } from "@/services/machines.service";
import { getReadings } from "@/services/readings.service";

export default async function DashboardPage() {
  const [machines, readings, anomalies] = await Promise.all([
    getMachines(),
    getReadings(),
    getAnomalies(),
  ]);

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Visão geral"
        title="Controle operacional da frota"
        description="Acompanhe o cadastro de máquinas e, após a integração com a API, as leituras e inconsistências operacionais."
      />

      <section aria-labelledby="indicadores-title">
        <h3 id="indicadores-title" className="sr-only">
          Indicadores
        </h3>
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-6">
          <MetricCard
            label="Total de máquinas"
            value={machines.length}
            helper="Cadastro de frota fornecido"
            icon={HardHat}
            emphasized
          />
          <MetricCard
            label="Atualizadas hoje"
            value={0}
            helper="Aguardando leituras"
            icon={RadioTower}
          />
          <MetricCard
            label="Sem leitura"
            value={0}
            helper="Aguardando integração"
            icon={ClipboardClock}
          />
          <MetricCard
            label="Leituras pendentes"
            value={readings.filter((item) => item.status === "PENDING").length}
            helper="Nenhum registro recebido"
            icon={History}
          />
          <MetricCard
            label="Leituras suspeitas"
            value={readings.filter((item) => item.status === "SUSPECT").length}
            helper="Nenhum registro recebido"
            icon={TriangleAlert}
          />
          <MetricCard
            label="Alertas"
            value={anomalies.length}
            helper="Nenhum alerta identificado"
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
            <EmptyState
              icon={CircleGauge}
              title="Nenhuma leitura registrada"
              description="Os registros aparecerão aqui quando forem enviados pelo aplicativo de campo ou cadastrados pela API."
              compact
            />
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
            <EmptyState
              icon={BellRing}
              title="Nenhum alerta identificado"
              description="Inconsistências serão exibidas quando o processamento de leituras estiver disponível."
              compact
            />
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
              title="Nenhuma máquina sem atualização"
              description="Os indicadores serão atualizados conforme os registros forem recebidos."
              compact
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
