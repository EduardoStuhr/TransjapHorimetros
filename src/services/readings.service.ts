import { apiFetch, type PagedApiResponse } from "@/services/api-client";
import type {
  HourMeterReading,
  ReadingStatus,
  ReadingType,
} from "@/types/domain";

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

export async function getReadings(): Promise<readonly HourMeterReading[]> {
  return fetchReadings("/api/v1/readings?page=1&pageSize=100");
}

export async function getMachineReadings(
  machineId: string,
): Promise<readonly HourMeterReading[]> {
  return fetchReadings(
    `/api/v1/machines/${encodeURIComponent(machineId)}/readings?page=1&pageSize=100`,
  );
}
