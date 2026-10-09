import { apiFetch, type PagedApiResponse } from "@/services/api-client";
import type {
  HourMeterReading,
  ReadingStatus,
  ReadingType,
} from "@/types/domain";

export interface ReadingFilters {
  endDate?: string;
  fleetNumber?: string;
  startDate?: string;
  status?: ReadingStatus | "";
  workSiteId?: string;
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
}

function mapReading(reading: ApiReading): HourMeterReading {
  return {
    id: reading.id,
    machineId: reading.machineId,
    machineFleetNumber: reading.machineFleetNumber,
    model: reading.machineModel,
    value: reading.value,
    differenceFromPrevious: null,
    readingType: reading.readingType,
    capturedAtDevice: reading.capturedAtDevice,
    receivedAtServer: reading.receivedAtServer,
    syncedAt: reading.syncedAt,
    clientEventId: reading.clientEventId,
    operatorName: null,
    workSiteId: reading.workSiteId,
    workSiteName: reading.workSiteName,
    status: reading.status,
    photoEvidenceUrl: null,
    ocrDetectedValue: null,
    ocrConfidence: null,
  };
}

async function fetchReadings(path: string): Promise<readonly HourMeterReading[]> {
  const response = await apiFetch<PagedApiResponse<ApiReading>>(path);
  return response.items.map(mapReading);
}

function buildReadingsPath(
  filters: ReadingFilters,
  page = 1,
): string {
  const searchParams = new URLSearchParams({
    page: String(page),
    pageSize: "100",
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
