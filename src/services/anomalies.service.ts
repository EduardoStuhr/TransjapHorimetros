import { apiFetch, type PagedApiResponse } from "@/services/api-client";
import type { Anomaly } from "@/types/domain";

export async function getAnomalies(): Promise<readonly Anomaly[]> {
  const response = await apiFetch<PagedApiResponse<Anomaly>>(
    "/api/v1/anomalies?page=1&pageSize=100",
  );
  return response.items;
}
