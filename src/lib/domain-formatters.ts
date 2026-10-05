import type { Machine, ReadingStatus } from "@/types/domain";

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
