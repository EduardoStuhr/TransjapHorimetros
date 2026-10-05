import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import {
  BellRing,
  Camera,
  ChevronLeft,
  CircleGauge,
  HardHat,
  Info,
  MapPinned,
  QrCode,
} from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { PageHeading } from "@/components/shared/page-heading";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  formatDateTime,
  formatHourMeter,
  formatMachineRegistrationStatus,
  formatQrCodeStatus,
} from "@/lib/domain-formatters";
import { getMachineByFleetNumber } from "@/services/machines.service";

interface MachinePageProps {
  params: Promise<{ fleetNumber: string }>;
}

export async function generateMetadata({
  params,
}: MachinePageProps): Promise<Metadata> {
  const { fleetNumber } = await params;
  const machine = await getMachineByFleetNumber(Number(fleetNumber));

  return {
    title: machine ? "Frota " + machine.fleetNumber : "Máquina não encontrada",
  };
}

function DetailItem({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string;
  icon: typeof HardHat;
}) {
  return (
    <div className="flex gap-3 border-b border-slate-100 py-4 last:border-0">
      <Icon className="mt-0.5 size-5 shrink-0 text-[#2c5b9e]" />
      <div>
        <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">
          {label}
        </dt>
        <dd className="mt-1 text-sm font-semibold text-slate-900">{value}</dd>
      </div>
    </div>
  );
}

export default async function MachineDetailPage({ params }: MachinePageProps) {
  const { fleetNumber } = await params;
  const machine = await getMachineByFleetNumber(Number(fleetNumber));

  if (!machine) {
    notFound();
  }

  return (
    <div className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-3">
        <Link href="/frota">
          <ChevronLeft />
          Voltar para a frota
        </Link>
      </Button>

      <PageHeading
        eyebrow="Detalhes da máquina"
        title={"Frota " + machine.fleetNumber}
        description={machine.model}
        action={
          <Badge
            variant="outline"
            className="border-slate-300 bg-white px-3 py-1.5 text-slate-600"
          >
            Status sem registro
          </Badge>
        }
      />

      <div className="grid gap-6 xl:grid-cols-[360px_minmax(0,1fr)]">
        <Card className="h-fit border-slate-200 py-0 shadow-sm">
          <CardContent className="p-5">
            <div className="mb-2 flex items-center gap-3 border-b border-slate-200 pb-5">
              <div className="flex size-12 items-center justify-center rounded-md bg-[#173665] text-white">
                <HardHat className="size-6" />
              </div>
              <div>
                <p className="text-xs font-bold uppercase tracking-wide text-slate-500">
                  Máquina
                </p>
                <p className="text-lg font-bold text-slate-950">
                  {machine.model}
                </p>
              </div>
            </div>
            <dl>
              <DetailItem
                label="Frota"
                value={String(machine.fleetNumber)}
                icon={HardHat}
              />
              <DetailItem
                label="Status cadastral"
                value={formatMachineRegistrationStatus(
                  machine.registrationStatus,
                )}
                icon={Info}
              />
              <DetailItem
                label="QR Code"
                value={formatQrCodeStatus(machine.qrCodeStatus)}
                icon={QrCode}
              />
              <DetailItem
                label="Horímetro atual"
                value={formatHourMeter(machine.currentHourMeter)}
                icon={CircleGauge}
              />
              <DetailItem
                label="Última leitura"
                value={formatDateTime(machine.lastReadingAt)}
                icon={CircleGauge}
              />
              <DetailItem
                label="Obra atual"
                value={machine.workSiteName ?? "Sem registro"}
                icon={MapPinned}
              />
            </dl>
          </CardContent>
        </Card>

        <Tabs defaultValue="history" className="min-w-0">
          <TabsList className="h-auto w-full justify-start overflow-x-auto rounded-md border bg-white p-1">
            <TabsTrigger value="history">Histórico</TabsTrigger>
            <TabsTrigger value="evidence">Evidências</TabsTrigger>
            <TabsTrigger value="alerts">Alertas</TabsTrigger>
            <TabsTrigger value="information">Informações</TabsTrigger>
          </TabsList>
          <TabsContent
            value="history"
            className="mt-4 rounded-lg border bg-white shadow-sm"
          >
            <EmptyState
              icon={CircleGauge}
              title="Nenhum histórico de horímetros"
              description="As leituras desta máquina serão exibidas após o recebimento dos primeiros registros."
            />
          </TabsContent>
          <TabsContent
            value="evidence"
            className="mt-4 rounded-lg border bg-white shadow-sm"
          >
            <EmptyState
              icon={Camera}
              title="Nenhuma evidência disponível"
              description="As fotografias associadas às leituras aparecerão aqui quando forem enviadas."
            />
          </TabsContent>
          <TabsContent
            value="alerts"
            className="mt-4 rounded-lg border bg-white shadow-sm"
          >
            <EmptyState
              icon={BellRing}
              title="Nenhum alerta identificado"
              description="Possíveis inconsistências desta máquina serão exibidas nesta área."
            />
          </TabsContent>
          <TabsContent
            value="information"
            className="mt-4 rounded-lg border bg-white p-5 shadow-sm"
          >
            <dl className="grid gap-x-8 sm:grid-cols-2">
              <DetailItem
                label="Número da frota"
                value={String(machine.fleetNumber)}
                icon={HardHat}
              />
              <DetailItem label="Modelo" value={machine.model} icon={Info} />
              <DetailItem
                label="Fabricante"
                value={machine.manufacturer ?? "Sem registro"}
                icon={Info}
              />
              <DetailItem
                label="Cadastro operacional"
                value={machine.operationalStatus ?? "Sem registro"}
                icon={Info}
              />
            </dl>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
