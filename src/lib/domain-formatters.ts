import type {
  Anomaly,
  AnomalyType,
  Machine,
  ReadingStatus,
} from "@/types/domain";

export function formatMachineRegistrationStatus(
  status: Machine["registrationStatus"],
) {
  if (status === "ACTIVE") {
    return "Ativa";
  }

  if (status === "INACTIVE") {
    return "Inativa";
  }

  return "Sem registro";
}

export function formatQrCodeStatus(status: Machine["qrCodeStatus"]) {
  if (status === "REGISTERED") {
    return "Cadastrado";
  }

  if (status === "MISSING") {
    return "Ausente";
  }

  return "Não informado";
}

export function formatHourMeter(value: number | null | undefined) {
  if (value === null || value === undefined) {
    return "—";
  }

  return new Intl.NumberFormat("pt-BR", {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(value);
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return "—";
  }

  return new Intl.DateTimeFormat("pt-BR", {
    dateStyle: "short",
    timeStyle: "short",
  }).format(new Date(value));
}

export function formatReadingStatus(status: ReadingStatus) {
  const labels: Record<ReadingStatus, string> = {
    VALIDATED: "Validado",
    PENDING: "Pendente",
    SUSPECT: "Suspeito",
    CORRECTED: "Corrigido",
    REJECTED: "Rejeitado",
  };

  return labels[status];
}

export function formatAnomalyType(type: AnomalyType) {
  const labels: Record<AnomalyType, string> = {
    READING_DECREASE: "Leitura menor que a anterior",
    IMPOSSIBLE_HOUR_INCREASE: "Aumento impossível de horas",
    DUPLICATE_READING: "Leitura duplicada",
    MISSING_READING: "Leitura ausente",
    LOW_OCR_CONFIDENCE: "Baixa confiança do OCR",
    OCR_OPERATOR_DIVERGENCE: "Divergência entre OCR e operador",
    LATE_SYNC: "Sincronização atrasada",
  };

  return labels[type];
}

export function formatAnomalySeverity(severity: Anomaly["severity"]) {
  const labels: Record<Anomaly["severity"], string> = {
    INFO: "Informativa",
    WARNING: "Atenção",
    CRITICAL: "Crítica",
  };

  return labels[severity];
}

export function formatAnomalyStatus(status: Anomaly["status"]) {
  const labels: Record<Anomaly["status"], string> = {
    OPEN: "Aberta",
    ACKNOWLEDGED: "Reconhecida",
    RESOLVED: "Resolvida",
  };

  return labels[status];
}
