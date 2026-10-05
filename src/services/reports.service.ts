export interface ReportAvailability {
  canExport: boolean;
  reason: string;
}

export async function getReportAvailability(): Promise<ReportAvailability> {
  return {
    canExport: false,
    reason: "Não há dados disponíveis para exportação.",
  };
}
