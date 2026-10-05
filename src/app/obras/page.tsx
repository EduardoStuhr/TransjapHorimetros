import type { Metadata } from "next";
import { MapPinned } from "lucide-react";

import { EmptyState } from "@/components/shared/empty-state";
import { PageHeading } from "@/components/shared/page-heading";
import { Card } from "@/components/ui/card";
import { WorkSiteCreateDialog } from "@/features/worksites/components/worksite-create-dialog";
import { getWorkSites } from "@/services/worksites.service";

export const metadata: Metadata = {
  title: "Obras",
};

export default async function WorkSitesPage() {
  const workSites = await getWorkSites();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Operação"
        title="Obras"
        description="Organize os locais de operação que serão associados às máquinas e leituras."
        action={<WorkSiteCreateDialog />}
      />
      {workSites.length === 0 ? (
        <Card className="gap-0 border-slate-200 py-0 shadow-sm">
          <EmptyState
            icon={MapPinned}
            title="Nenhuma obra cadastrada"
            description="Cadastros de obras aparecerão aqui quando a API estiver disponível."
          />
        </Card>
      ) : null}
    </div>
  );
}
