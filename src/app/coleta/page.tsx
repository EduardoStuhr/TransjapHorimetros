import type { Metadata } from "next";

import { ReadingCaptureForm } from "@/features/offline/reading-capture-form";

export const metadata: Metadata = { title: "Registrar leitura" };

export default function ReadingCapturePage() {
  return <ReadingCaptureForm />;
}
