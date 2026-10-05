import type { Metadata } from "next";

import { DataSourceNotice } from "@/components/shared/data-source-notice";
import { PageHeading } from "@/components/shared/page-heading";
import { ReadingsFilters } from "@/features/readings/components/readings-filters";
import { ReadingsTable } from "@/features/readings/components/readings-table";
import { getMachines } from "@/services/machines.service";
import { getReadings } from "@/services/readings.service";

export const metadata: Metadata = {
  title: "Horímetros",
};

export default async function ReadingsPage() {
  const [machines, readings] = await Promise.all([
    getMachines(),
    getReadings(),
  ]);

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Registros"
        title="Horímetros"
        description="Consulte leituras, evidências e status de validação quando os registros começarem a ser recebidos."
      />
      <DataSourceNotice>
        Ainda não existem leituras integradas. Os filtros e a estrutura da tabela
        já estão preparados para a API.
      </DataSourceNotice>
      <ReadingsFilters machines={machines} />
      <ReadingsTable readings={readings} />
    </div>
  );
}
