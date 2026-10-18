import Link from "next/link";
import { Button } from "@/components/ui/button";
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
import { getReadingsPage } from "@/services/readings.service";
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
    meterUnit: getSearchParam(params, "meterUnit"),
    readingType: getSearchParam(params, "readingType"),
    hasPhotoEvidence: getSearchParam(params, "hasPhotoEvidence"),
    hasAnomaly: getSearchParam(params, "hasAnomaly"),
    machineId: getSearchParam(params, "machineId"),
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
  const rawPage = Number(getSearchParam(params, "page") || "1");
  const page = Number.isSafeInteger(rawPage) && rawPage >= 1 && rawPage <= 1000000 ? rawPage : 1;
  const [machines, readingsPage, workSites] = await Promise.all([
    getMachines(),
    getReadingsPage(filters, page),
    getWorkSites(),
  ]);

  const readings = readingsPage.items;
  function pageUrl(nextPage: number) {
    const query = new URLSearchParams(Object.entries(filters).filter(([, value]) => Boolean(value)));
    query.set("page", String(nextPage));
    return `/horimetros?${query.toString()}`;
  }
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
      <nav aria-label="Páginas de leituras" className="flex flex-wrap items-center justify-between gap-3 text-sm text-slate-600">
        <span>{readingsPage.totalItems} registros</span>
        <div className="flex items-center gap-3">
          {page > 1 && <Button asChild variant="outline"><Link href={pageUrl(page - 1)}>Anterior</Link></Button>}
          <span>{page} / {Math.max(1, readingsPage.totalPages)}</span>
          {page < readingsPage.totalPages && <Button asChild variant="outline"><Link href={pageUrl(page + 1)}>Próxima</Link></Button>}
        </div>
      </nav>
    </div>
  );
}
