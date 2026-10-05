import type { Metadata } from "next";

import { DataSourceNotice } from "@/components/shared/data-source-notice";
import { PageHeading } from "@/components/shared/page-heading";
import { FleetTable } from "@/features/machines/components/fleet-table";
import { getMachines } from "@/services/machines.service";

export const metadata: Metadata = {
  title: "Frota",
};

export default async function FleetPage() {
  const machines = await getMachines();

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Cadastro"
        title="Frota"
        description="Consulte as máquinas cadastradas e acesse os detalhes operacionais de cada equipamento."
      />
      <DataSourceNotice>
        Exibindo {machines.length} máquinas do cadastro de frota fornecido. Os
        campos operacionais permanecem sem registro até a integração com a API.
      </DataSourceNotice>
      <FleetTable data={machines} />
    </div>
  );
}
