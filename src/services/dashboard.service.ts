import { apiFetch } from "@/services/api-client";
import type { DashboardSummary } from "@/types/domain";

export async function getDashboardSummary(): Promise<DashboardSummary> {
  return apiFetch<DashboardSummary>("/api/v1/dashboard/summary");
}
