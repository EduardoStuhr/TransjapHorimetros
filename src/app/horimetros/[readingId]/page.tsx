import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { connection } from "next/server";
import { Camera, ChevronLeft, CircleGauge, Clock, HardHat, MapPinned } from "lucide-react";

import { PageHeading } from "@/components/shared/page-heading";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PhotoEvidencePanel } from "@/features/readings/components/photo-evidence-panel";
import { formatDateTime, formatHourMeter, formatReadingStatus } from "@/lib/domain-formatters";
import { ApiError } from "@/services/api-client";
import { getPhotoEvidence, getReading } from "@/services/readings.service";

export const metadata: Metadata = {
  title: "Detalhes da leitura",
};

interface ReadingDetailPageProps {
  params: Promise<{ readingId: string }>;
}

function DetailItem({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string;
  icon: typeof Clock;
}) {
  return (
    <div className="flex gap-3 border-b border-slate-100 py-4 last:border-0">
      <Icon className="mt-0.5 size-5 shrink-0 text-[#2c5b9e]" />
      <div>
        <dt className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</dt>
        <dd className="mt-1 text-sm font-semibold text-slate-900">{value}</dd>
      </div>
    </div>
  );
}

export default async function ReadingDetailPage({ params }: ReadingDetailPageProps) {
  await connection();
  const { readingId } = await params;
  let reading: Awaited<ReturnType<typeof getReading>>;

  try {
    reading = await getReading(readingId);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      notFound();
    }
    throw error;
  }

  const evidence = reading.hasPhotoEvidence
    ? await getPhotoEvidence(reading.id)
    : null;
  const statusClassNames = {
    VALIDATED: "border-emerald-200 bg-emerald-50 text-emerald-800",
    PENDING: "border-amber-200 bg-amber-50 text-amber-800",
    SUSPECT: "border-orange-200 bg-orange-50 text-orange-800",
    CORRECTED: "border-blue-200 bg-blue-50 text-blue-800",
    REJECTED: "border-red-200 bg-red-50 text-red-800",
  } as const;

  return (
    <div className="space-y-6">
      <Button asChild variant="ghost" size="sm" className="-ml-3">
        <Link href="/horimetros">
          <ChevronLeft />
          Voltar para horímetros
        </Link>
      </Button>

      <PageHeading
        eyebrow="Detalhes da leitura"
        title={`Frota ${reading.machineFleetNumber}`}
        description={`${reading.model} · ${formatDateTime(reading.capturedAtDevice)}`}
        action={
          <Badge variant="outline" className={statusClassNames[reading.status]}>
            {formatReadingStatus(reading.status)}
          </Badge>
        }
      />

      <div className="grid gap-6 lg:grid-cols-[340px_minmax(0,1fr)]">
        <Card className="h-fit border-slate-200 py-0 shadow-sm">
          <CardContent className="p-5">
            <dl>
              <DetailItem label="Frota" value={String(reading.machineFleetNumber)} icon={HardHat} />
              <DetailItem label="Horímetro" value={formatHourMeter(reading.value)} icon={CircleGauge} />
              <DetailItem label="Capturada em" value={formatDateTime(reading.capturedAtDevice)} icon={Clock} />
              <DetailItem label="Recebida em" value={formatDateTime(reading.receivedAtServer)} icon={Clock} />
              <DetailItem label="Obra" value={reading.workSiteName ?? "Sem registro"} icon={MapPinned} />
            </dl>
          </CardContent>
        </Card>

        <Card className="border-slate-200 shadow-sm">
          <CardHeader className="border-b">
            <CardTitle className="flex items-center gap-2">
              <Camera className="size-5 text-[#2c5b9e]" />
              Evidência fotográfica
            </CardTitle>
          </CardHeader>
          <CardContent>
            <PhotoEvidencePanel readingId={reading.id} evidence={evidence} />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
