import { apiFetch } from "@/services/api-client";
import type { ReadingFilters } from "@/services/readings.service";

export interface ReadingReview {
  readingId: string; approved: boolean; reason: string; actorEmail: string; createdAt: string;
}
export interface OperationalSummary {
  totalMachines: number; activeMachines: number; readings: number; receivedToday: number;
  pendingReview: number; suspectReadings: number; openAnomalies: number;
  byWorkSite: { label: string; count: number }[]; byDay: { label: string; count: number }[];
  oldestMachines: { fleetNumber: number; model: string; lastReceivedAt: string | null }[];
}
export function getOperationalSummary(filters: ReadingFilters) {
  const params = new URLSearchParams();
  for (const key of ["machineId","fleetNumber","workSiteId","operatorId","status","readingType","hasAnomaly"] as const) {
    if (filters[key]) params.set(key, filters[key]);
  }
  if (filters.startDate) params.set("from", filters.startDate + "T00:00:00Z");
  if (filters.endDate) params.set("to", filters.endDate + "T23:59:59.999Z");
  return apiFetch<OperationalSummary>("/api/v1/operations/summary?" + params);
}
