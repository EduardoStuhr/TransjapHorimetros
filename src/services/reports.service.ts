export interface ReportAvailability {
  canExport: boolean;
  reason: string;
}

export async function getReportAvailability(): Promise<ReportAvailability> {
  const readings = await getReadings();

  return {
    canExport: false,
    reason:
      readings.length === 0
        ? "Não há dados disponíveis para exportação."
        : "As consultas usam dados reais da API; exportação em PDF/Excel não faz parte desta versão.",
  };
}
import { getReadings } from "@/services/readings.service";
