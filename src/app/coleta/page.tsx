import type { Metadata } from "next";

import { PageHeading } from "@/components/shared/page-heading";
import { ReadingCaptureForm } from "@/features/offline/reading-capture-form";

export const metadata: Metadata = { title: "Registrar leitura" };

export default function ReadingCapturePage() {
  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Operação de campo"
        title="Registrar leitura"
        description="Registre a leitura e a evidência mesmo sem internet. O envio respeita a ordem, a autoria e a idempotência do evento."
      />
      <ReadingCaptureForm />
    </div>
  );
}
