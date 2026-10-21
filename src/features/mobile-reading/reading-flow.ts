import type { CachedMachine, OfflinePhotoPayload, OfflineReadingPayload } from "@/offline/types";

export const MOBILE_READING_STEPS = [
  "IDENTIFY_MACHINE",
  "ENTER_READING",
  "CAPTURE_PHOTO",
  "REVIEW",
  "SAVING",
  "SUCCESS",
] as const;

export type MobileReadingStep = (typeof MOBILE_READING_STEPS)[number];
export type ReadingTypeOption = OfflineReadingPayload["readingType"];

export const READING_TYPE_OPTIONS: ReadonlyArray<{
  value: ReadingTypeOption;
  label: string;
}> = [
  { value: "INTERMEDIATE", label: "Intermediária" },
  { value: "OPENING", label: "Abertura" },
  { value: "CLOSING", label: "Fechamento" },
  { value: "MAINTENANCE", label: "Manutenção" },
];

export const MAXIMUM_PHOTO_SIZE_BYTES = 10 * 1024 * 1024;
export const ALLOWED_PHOTO_TYPES = new Set<OfflinePhotoPayload["mimeType"]>([
  "image/jpeg",
  "image/png",
  "image/webp",
]);

const permittedTransitions: Record<MobileReadingStep, readonly MobileReadingStep[]> = {
  IDENTIFY_MACHINE: ["ENTER_READING"],
  ENTER_READING: ["IDENTIFY_MACHINE", "CAPTURE_PHOTO"],
  CAPTURE_PHOTO: ["ENTER_READING", "REVIEW"],
  REVIEW: ["CAPTURE_PHOTO", "SAVING"],
  SAVING: ["REVIEW", "SUCCESS"],
  SUCCESS: ["IDENTIFY_MACHINE"],
};

export function canTransitionReadingStep(from: MobileReadingStep, to: MobileReadingStep) {
  return permittedTransitions[from].includes(to);
}

export function tryAcquireSubmissionLock(lock: { current: boolean }) {
  if (lock.current) return false;
  lock.current = true;
  return true;
}

export function parseReadingValue(value: string): number | null {
  const normalized = value.trim();
  if (!/^\d+(?:[.,]\d{1,2})?$/.test(normalized)) return null;
  const parsed = Number(normalized.replace(",", "."));
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
}

export function validateReadingDetails(input: {
  machine: CachedMachine | null;
  readingValue: string;
  workSiteId: string;
  capturedAtLocal: string;
}) {
  if (!input.machine) return "Identifique uma máquina antes de continuar.";
  if (!input.machine.meterUnit) return "A máquina não possui unidade de medição configurada.";
  if (parseReadingValue(input.readingValue) === null) {
    return "Informe uma leitura válida, com no máximo duas casas decimais.";
  }
  if (!input.workSiteId) return "Selecione a obra da coleta.";
  const capturedAt = new Date(input.capturedAtLocal);
  if (Number.isNaN(capturedAt.getTime())) return "Informe um horário de captura válido.";
  return null;
}

export function validatePhoto(file: File | Blob | null) {
  if (!file) return "Tire uma foto do mostrador para continuar.";
  if (!ALLOWED_PHOTO_TYPES.has(file.type as OfflinePhotoPayload["mimeType"])) {
    return "Use uma foto JPEG, PNG ou WEBP.";
  }
  if (file.size > MAXIMUM_PHOTO_SIZE_BYTES) return "A foto deve ter no máximo 10 MB.";
  return null;
}

export function isReadingBelowPrevious(value: string, previousValue: number | null | undefined) {
  const parsed = parseReadingValue(value);
  return parsed !== null && previousValue != null && parsed < previousValue;
}

export function localDateTimeValue(now = new Date()) {
  const offset = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - offset).toISOString().slice(0, 16);
}

export function cacheAgeState(fetchedAt: string | null, now = new Date()) {
  if (!fetchedAt) return "EMPTY" as const;
  const timestamp = new Date(fetchedAt).getTime();
  if (!Number.isFinite(timestamp)) return "STALE" as const;
  return now.getTime() - timestamp > 24 * 60 * 60_000 ? "STALE" as const : "FRESH" as const;
}

export function meterUnitLabel(unit: CachedMachine["meterUnit"]) {
  if (unit === "HOURS") return "horas";
  if (unit === "KM") return "km";
  return "não configurada";
}
