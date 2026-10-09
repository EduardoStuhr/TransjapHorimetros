import { apiFetch, type PagedApiResponse } from "@/services/api-client";
import type { Anomaly } from "@/types/domain";

export async function getAnomalies(): Promise<readonly Anomaly[]> {
  const response = await apiFetch<PagedApiResponse<Anomaly>>(
    "/api/v1/anomalies?page=1&pageSize=100",
  );
  return response.items;
}

export async function getAllAnomalies(): Promise<readonly Anomaly[]> {
  const firstPage = await apiFetch<PagedApiResponse<Anomaly>>(
    "/api/v1/anomalies?page=1&pageSize=100",
  );
  const pages = await Promise.all(
    Array.from({ length: Math.max(0, firstPage.totalPages - 1) }, (_, index) =>
      apiFetch<PagedApiResponse<Anomaly>>(
        `/api/v1/anomalies?page=${index + 2}&pageSize=100`,
      ),
    ),
  );

  return [firstPage, ...pages].flatMap((page) => page.items);
}
