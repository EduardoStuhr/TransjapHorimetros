import type { Metadata } from "next";

import { PageHeading } from "@/components/shared/page-heading";
import { SyncQueuePanel } from "@/features/offline/sync-queue-panel";

export const metadata: Metadata = { title: "Sincronização" };

export default function SyncPage() {
  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Offline first"
        title="Sincronização"
        description="Acompanhe leituras e fotos salvas neste dispositivo. Falhas nunca apagam o evento original."
      />
      <SyncQueuePanel />
    </div>
  );
}
