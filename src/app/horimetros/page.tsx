import type { Metadata } from "next";

import { DataSourceNotice } from "@/components/shared/data-source-notice";
import { PageHeading } from "@/components/shared/page-heading";
import { ReadingsFilters } from "@/features/readings/components/readings-filters";
import { ReadingsTable } from "@/features/readings/components/readings-table";
import {
  emptyReadingsFilters,
  readingsFilterSchema,
} from "@/schemas/readings-filter.schema";
import { getMachines } from "@/services/machines.service";
import { getReadings } from "@/services/readings.service";
import { getWorkSites } from "@/services/worksites.service";

export const metadata: Metadata = {
  title: "Horímetros",
};

interface ReadingsPageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

function getSearchParam(
  searchParams: Record<string, string | string[] | undefined>,
  key: string,
): string {
  const value = searchParams[key];
  return typeof value === "string" ? value : "";
}

export default async function ReadingsPage({
  searchParams,
}: ReadingsPageProps) {
  const params = await searchParams;
  const parsedFilters = readingsFilterSchema.safeParse({
    endDate: getSearchParam(params, "endDate"),
    fleetNumber: getSearchParam(params, "fleetNumber"),
    startDate: getSearchParam(params, "startDate"),
    status: getSearchParam(params, "status"),
    workSiteId: getSearchParam(params, "workSiteId"),
  });
  const filters = parsedFilters.success
    ? parsedFilters.data
    : emptyReadingsFilters;
  const hasFilters = Object.values(filters).some(Boolean);
  const [machines, readings, workSites] = await Promise.all([
    getMachines(),
    getReadings(filters),
    getWorkSites(),
  ]);

  return (
    <div className="space-y-6">
      <PageHeading
        eyebrow="Registros"
        title="Horímetros"
        description="Consulte as leituras recebidas pela API e refine os resultados pelos campos operacionais disponíveis."
      />
      <DataSourceNotice>
        {hasFilters
          ? `${readings.length} leitura(s) encontrada(s) para os filtros selecionados.`
          : `Exibindo ${readings.length} leitura(s) recebida(s) pela API.`}
      </DataSourceNotice>
      <ReadingsFilters
        key={JSON.stringify(filters)}
        filters={filters}
        machines={machines}
        workSites={workSites}
      />
      <ReadingsTable
        readings={readings}
        emptyDescription={
          hasFilters
            ? "Não há leituras para os filtros selecionados."
            : "Nenhuma leitura foi registrada pela API."
        }
      />
    </div>
  );
}
