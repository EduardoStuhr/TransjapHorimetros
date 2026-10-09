import type { Metadata } from "next";
import { connection } from "next/server";

import { DataSourceNotice } from "@/components/shared/data-source-notice";
import { PageHeading } from "@/components/shared/page-heading";
import { FleetTable } from "@/features/machines/components/fleet-table";
import { getMachines } from "@/services/machines.service";

export const metadata: Metadata = {
  title: "Frota",
};

export default async function FleetPage() {
  await connection();
  const machines = await getMachines();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Cadastro"
        title="Frota"
        description="Consulte as máquinas cadastradas e acesse os detalhes operacionais de cada equipamento."
      />
      <DataSourceNotice>
        Exibindo {machines.length} máquinas consultadas na API oficial.
        Campos sem dado operacional permanecem identificados como sem registro.
      </DataSourceNotice>
      <FleetTable data={machines} />
    </div>
  );
}
