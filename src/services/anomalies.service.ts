import { apiFetch, type PagedApiResponse } from "@/services/api-client";
import type { ReadingFilters } from "@/services/readings.service";
import type { Anomaly } from "@/types/domain";

export async function getAnomalies(): Promise<readonly Anomaly[]> {
  return (
    await apiFetch<PagedApiResponse<Anomaly>>(
      "/api/v1/anomalies?page=1&pageSize=100",
    )
  ).items;
}
export async function getAllAnomalies(
  filters: ReadingFilters = {},
): Promise<readonly Anomaly[]> {
  if (filters.hasAnomaly === "false") return [];
  const query = new URLSearchParams({ pageSize: "100" });
  for (const key of [
    "fleetNumber",
    "machineId",
    "workSiteId",
    "operatorId",
    "readingType",
  ] as const) {
    if (filters[key]) query.set(key, filters[key]);
  }
  if (filters.status) query.set("readingStatus", filters.status);
  if (filters.startDate) query.set("from", filters.startDate + "T00:00:00Z");
  if (filters.endDate) query.set("to", filters.endDate + "T23:59:59.999Z");
  const items: Anomaly[] = [];
  let totalPages = 1;
  for (let page = 1; page <= totalPages; page++) {
    query.set("page", String(page));
    const result = await apiFetch<PagedApiResponse<Anomaly>>(
      "/api/v1/anomalies?" + query,
    );
    items.push(...result.items);
    totalPages = result.totalPages;
  }
  return items;
}

export function getAnomaliesPage(page: number, status: string) {
  const q = new URLSearchParams({ page: String(page), pageSize: "20" });
  if (["OPEN", "ACKNOWLEDGED", "RESOLVED"].includes(status))
    q.set("status", status);
  return apiFetch<PagedApiResponse<Anomaly>>("/api/v1/anomalies?" + q);
}
