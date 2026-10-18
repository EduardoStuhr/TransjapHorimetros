import { ReadingOperationalDetails } from "@/features/readings/components/reading-operational-details";
import { ReadingStatusBadge } from "@/features/readings/components/reading-badges";
import { PhotoOcrPanel } from "@/features/readings/components/photo-ocr-panel";
import type { PhotoOcrResult } from "@/types/domain";
import { apiFetch } from "@/services/api-client";
import type { Metadata } from "next";
import Link from "next/link";
import { notFound, unstable_rethrow } from "next/navigation";
import { connection } from "next/server";
import { Camera, ChevronLeft } from "lucide-react";

import { PageHeading } from "@/components/shared/page-heading";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PhotoEvidencePanel } from "@/features/readings/components/photo-evidence-panel";
import { formatDateTime } from "@/lib/domain-formatters";
import { ApiError } from "@/services/api-client";
import { getPhotoEvidence, getReading } from "@/services/readings.service";

export const metadata: Metadata = {
  title: "Detalhes da leitura",
};

interface ReadingDetailPageProps {
  params: Promise<{ readingId: string }>;
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
  let ocr: PhotoOcrResult | null = null;
  let ocrLoadError = false;
  if (evidence) {
    try { ocr = await apiFetch<PhotoOcrResult>(`/api/v1/readings/${encodeURIComponent(reading.id)}/photo/ocr`); }
    catch (error) {
      unstable_rethrow(error);
      if (!(error instanceof ApiError && error.status === 404)) ocrLoadError = true;
    }
  }
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
          <ReadingStatusBadge status={reading.status} />
        }
      />

      <div className="space-y-6">
        <ReadingOperationalDetails reading={reading} />

        <Card className="border-slate-200 shadow-sm">
          <CardHeader className="border-b">
            <CardTitle className="flex items-center gap-2">
              <Camera className="size-5 text-[#2c5b9e]" />
              Evidência fotográfica
            </CardTitle>
          </CardHeader>
          <CardContent>
            <PhotoEvidencePanel readingId={reading.id} evidence={evidence} />
            {evidence && <PhotoOcrPanel readingId={reading.id} operatorValue={reading.value} result={ocr} loadError={ocrLoadError} />}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
