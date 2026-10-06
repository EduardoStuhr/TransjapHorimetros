import { ApiError, apiFetch, type PagedApiResponse } from "@/services/api-client";
import { getMachineReadings } from "@/services/readings.service";
import type { Machine, MachineDetails } from "@/types/domain";

interface ApiLatestReading {
  value: number;
  capturedAtDevice: string;
  workSiteName: string | null;
}

interface ApiMachineListItem {
  id: string;
  fleetNumber: number;
  model: string;
  status: "ACTIVE" | "INACTIVE";
  latestReading: ApiLatestReading | null;
}

interface ApiMachineDetails extends ApiMachineListItem {
  alertCount: number;
  qrCode: string | null;
}

function mapMachine(machine: ApiMachineListItem): Machine {
  return {
    id: machine.id,
    fleetNumber: machine.fleetNumber,
    model: machine.model,
    manufacturer: null,
    registrationStatus: machine.status,
    operationalStatus: null,
    currentHourMeter: machine.latestReading?.value ?? null,
    lastReadingAt: machine.latestReading?.capturedAtDevice ?? null,
    workSiteName: machine.latestReading?.workSiteName ?? null,
    qrCodeStatus: null,
  };
}

export async function getMachines(): Promise<readonly Machine[]> {
  const response = await apiFetch<PagedApiResponse<ApiMachineListItem>>(
    "/api/v1/machines?page=1&pageSize=100",
  );
  return response.items.map(mapMachine);
}

export async function getMachineByFleetNumber(
  fleetNumber: number,
): Promise<MachineDetails | undefined> {
  try {
    const machine = await apiFetch<ApiMachineDetails>(
      `/api/v1/machines/by-fleet/${fleetNumber}`,
    );
    const recentReadings = await getMachineReadings(machine.id);

    return {
      ...mapMachine(machine),
      recentReadings,
      alertCount: machine.alertCount,
      qrCode: machine.qrCode,
    };
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return undefined;
    }

    throw error;
  }
}
