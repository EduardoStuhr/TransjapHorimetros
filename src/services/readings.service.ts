import { apiFetch, type PagedApiResponse } from "@/services/api-client";
import type {
  HourMeterReading,
  PhotoEvidence,
  ReadingStatus,
  ReadingType,
  MeterUnit,
  PhotoOcrResult,
  ReadingAnomalySummary,
  ReadingCorrectionSummary,
} from "@/types/domain";

export interface ReadingFilters {
  endDate?: string;
  fleetNumber?: string;
  startDate?: string;
  status?: ReadingStatus | "";
  workSiteId?: string;
  machineId?: string;
  meterUnit?: MeterUnit | "";
  readingType?: ReadingType | "";
  hasPhotoEvidence?: "true" | "false" | "";
  hasAnomaly?: "true" | "false" | "";
}

interface ApiReading {
  id: string;
  machineId: string;
  machineFleetNumber: number;
  machineModel: string;
  workSiteId: string | null;
  workSiteName: string | null;
  value: number;
  readingType: ReadingType;
  status: ReadingStatus;
  capturedAtDevice: string;
  receivedAtServer: string;
  syncedAt: string;
  clientEventId: string;
  createdAt: string;
  updatedAt: string;
  hasPhotoEvidence: boolean;
  meterUnit: MeterUnit | null;
  differenceFromPrevious: number | null;
  previousReadingId: string | null;
  correctionOfReadingId: string | null;
  createdByUserId: string | null;
  createdByEmail: string | null;
  deviceId: string | null;
  anomalyCount: number;
  openAnomalyCount: number;
  correctionCount: number;
  ocr: Pick<PhotoOcrResult, "status" | "detectedValue" | "confidence" | "matchesOperatorValue"> | null;
  anomalies: ReadingAnomalySummary[] | null;
  corrections: ReadingCorrectionSummary[] | null;
}

function mapReading(reading: ApiReading): HourMeterReading {
  return {
    id: reading.id,
    machineId: reading.machineId,
    machineFleetNumber: reading.machineFleetNumber,
    model: reading.machineModel,
    value: reading.value,
    differenceFromPrevious: reading.differenceFromPrevious,
    meterUnit: reading.meterUnit,
    readingType: reading.readingType,
    capturedAtDevice: reading.capturedAtDevice,
    receivedAtServer: reading.receivedAtServer,
    syncedAt: reading.syncedAt,
    clientEventId: reading.clientEventId,
    operatorName: reading.createdByEmail,
    workSiteId: reading.workSiteId,
    workSiteName: reading.workSiteName,
    status: reading.status,
    hasPhotoEvidence: reading.hasPhotoEvidence,
    ocrDetectedValue: reading.ocr?.detectedValue ?? null,
    ocrConfidence: reading.ocr?.confidence ?? null,
    ocrStatus: reading.ocr?.status ?? null,
    ocrMatchesOperatorValue: reading.ocr?.matchesOperatorValue ?? null,
    createdByUserId: reading.createdByUserId,
    createdByEmail: reading.createdByEmail,
    deviceId: reading.deviceId,
    previousReadingId: reading.previousReadingId,
    correctionOfReadingId: reading.correctionOfReadingId,
    anomalyCount: reading.anomalyCount,
    openAnomalyCount: reading.openAnomalyCount,
    correctionCount: reading.correctionCount,
    anomalies: reading.anomalies,
    corrections: reading.corrections,
    createdAt: reading.createdAt,
    updatedAt: reading.updatedAt,
  };
}

async function fetchReadings(path: string): Promise<readonly HourMeterReading[]> {
  const response = await apiFetch<PagedApiResponse<ApiReading>>(path);
  return response.items.map(mapReading);
}

function buildReadingsPath(
  filters: ReadingFilters,
  page = 1,
  pageSize = 100,
): string {
  const searchParams = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });

  if (filters.fleetNumber) {
    searchParams.set("fleetNumber", filters.fleetNumber);
  }

  if (filters.workSiteId) {
    searchParams.set("workSiteId", filters.workSiteId);
  }

  if (filters.status) {
    searchParams.set("status", filters.status);
  }

  for (const key of ["machineId", "meterUnit", "readingType", "hasPhotoEvidence", "hasAnomaly"] as const) {
    if (filters[key]) searchParams.set(key, filters[key]);
  }

  if (filters.startDate) {
    searchParams.set("from", `${filters.startDate}T00:00:00.000Z`);
  }

  if (filters.endDate) {
    searchParams.set("to", `${filters.endDate}T23:59:59.999Z`);
  }

  return `/api/v1/readings?${searchParams.toString()}`;
}

export async function getReadings(
  filters: ReadingFilters = {},
): Promise<readonly HourMeterReading[]> {
  return fetchReadings(buildReadingsPath(filters));
}

export async function getAllReadings(
  filters: ReadingFilters = {},
): Promise<readonly HourMeterReading[]> {
  const firstPage = await apiFetch<PagedApiResponse<ApiReading>>(
    buildReadingsPath(filters),
  );
  const pages = await Promise.all(
    Array.from({ length: Math.max(0, firstPage.totalPages - 1) }, (_, index) =>
      apiFetch<PagedApiResponse<ApiReading>>(
        buildReadingsPath(filters, index + 2),
      ),
    ),
  );

  return [firstPage, ...pages].flatMap((page) => page.items.map(mapReading));
}

export async function getMachineReadings(
  machineId: string,
): Promise<readonly HourMeterReading[]> {
  return fetchReadings(
    `/api/v1/machines/${encodeURIComponent(machineId)}/readings?page=1&pageSize=100`,
  );
}

export async function getReading(readingId: string): Promise<HourMeterReading> {
  const reading = await apiFetch<ApiReading>(
    `/api/v1/readings/${encodeURIComponent(readingId)}`,
  );
  return mapReading(reading);
}

export async function getPhotoEvidence(readingId: string) {
  return apiFetch<PhotoEvidence>(
    `/api/v1/readings/${encodeURIComponent(readingId)}/photo`,
  );
}

export async function getReadingsPage(filters: ReadingFilters = {}, page = 1) {
  const response = await apiFetch<PagedApiResponse<ApiReading>>(buildReadingsPath(filters, page, 20));
  return { ...response, items: response.items.map(mapReading) };
}
