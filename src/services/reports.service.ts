import { getAllAnomalies } from "@/services/anomalies.service";
import { getAllReadings } from "@/services/readings.service";
import type { Anomaly, HourMeterReading } from "@/types/domain";

export const reportDefinitions = [
  {
    type: "readings",
    title: "Relatório de horímetros",
    description: "Leituras por período, status e evolução de horas.",
  },
  {
    type: "machines",
    title: "Relatório por máquina",
    description: "Histórico de leituras ordenado por máquina da frota.",
  },
  {
    type: "worksites",
    title: "Relatório por obra",
    description: "Leituras agrupadas pelo local de operação informado.",
  },
  {
    type: "anomalies",
    title: "Relatório de inconsistências",
    description: "Alertas reais identificados pelo motor de regras.",
  },
] as const;

export type ReportType = (typeof reportDefinitions)[number]["type"];

export interface ReportAvailability {
  count: number;
  type: ReportType;
}

export type ReportData =
  | {
      items: readonly Anomaly[];
      type: "anomalies";
    }
  | {
      items: readonly HourMeterReading[];
      type: Exclude<ReportType, "anomalies">;
    };

export function isReportType(value: string): value is ReportType {
  return reportDefinitions.some((report) => report.type === value);
}

export function getReportDefinition(type: ReportType) {
  return reportDefinitions.find((report) => report.type === type)!;
}

export async function getReportData(type: ReportType): Promise<ReportData> {
  if (type === "anomalies") {
    return { type, items: await getAllAnomalies() };
  }

  const readings = [...(await getAllReadings())];

  if (type === "machines") {
    readings.sort(
      (left, right) =>
        left.machineFleetNumber - right.machineFleetNumber ||
        left.capturedAtDevice.localeCompare(right.capturedAtDevice),
    );
  }

  if (type === "worksites") {
    readings.sort(
      (left, right) =>
        (left.workSiteName ?? "Sem obra").localeCompare(
          right.workSiteName ?? "Sem obra",
          "pt-BR",
        ) || left.capturedAtDevice.localeCompare(right.capturedAtDevice),
    );
  }

  return { type, items: readings };
}

export async function getReportAvailability(): Promise<
  readonly ReportAvailability[]
> {
  const [readings, anomalies] = await Promise.all([
    getAllReadings(),
    getAllAnomalies(),
  ]);

  return reportDefinitions.map((report) => ({
    type: report.type,
    count: report.type === "anomalies" ? anomalies.length : readings.length,
  }));
}
