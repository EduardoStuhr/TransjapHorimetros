import { apiFetch } from "@/services/api-client";
import type { ReadingFilters } from "@/services/readings.service";

export interface ReadingReview {
  readingId: string;
  approved: boolean;
  reason: string;
  actorEmail: string;
  createdAt: string;
}
export interface OperationalSummary {
  totalMachines: number;
  machinesWithReadings: number;
  approvedReadings: number;
  withoutRecentReading: number;
  activeMachines: number;
  readings: number;
  receivedToday: number;
  pendingReview: number;
  suspectReadings: number;
  openAnomalies: number;
  byWorkSite: { label: string; count: number }[];
  byDay: { label: string; count: number }[];
  oldestMachines: {
    fleetNumber: number;
    model: string;
    lastReceivedAt: string | null;
  }[];
}
export function getOperationalSummary(filters: ReadingFilters) {
  const params = new URLSearchParams();
  for (const key of [
    "reviewed",
    "ocrStatus",
    "machineId",
    "fleetNumber",
    "workSiteId",
    "operatorId",
    "status",
    "readingType",
    "hasAnomaly",
  ] as const) {
    if (filters[key]) params.set(key, filters[key]);
  }
  if (filters.startDate) params.set("from", filters.startDate + "T00:00:00Z");
  if (filters.endDate) params.set("to", filters.endDate + "T23:59:59.999Z");
  return apiFetch<OperationalSummary>("/api/v1/operations/summary?" + params);
}


export interface UtilizationRow {
  machineId: string;
  fleetNumber: number;
  model: string;
  readingId: string;
  at: string;
  initial: number;
  final: number;
  hours: number | null;
  reason: string;
  workSiteId: string | null;
  workSite: string | null;
}
export interface UtilizationReport {
  hours: number | null;
  calculatedIntervals: number;
  unavailableIntervals: number;
  items: UtilizationRow[];
  totalItems: number;
  page: number;
  pageSize: number;
}
export function getUtilization(filters: ReadingFilters, exportAll = false) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    if (!value) continue;
    if (key === "startDate") params.set("from", value + "T00:00:00Z");
    else if (key === "endDate") params.set("to", value + "T23:59:59.999Z");
    else params.set(key, value);
  }
  if (exportAll) params.set("export", "true");
  return apiFetch<UtilizationReport>("/api/v1/operations/utilization?" + params);
}
