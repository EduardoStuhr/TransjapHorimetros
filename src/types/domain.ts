export interface Machine {
  fleetNumber: number;
  model: string;
  manufacturer?: string | null;
  registrationStatus?: "ACTIVE" | "INACTIVE" | null;
  operationalStatus?: string | null;
  currentHourMeter?: number | null;
  lastReadingAt?: string | null;
  workSiteName?: string | null;
  qrCodeStatus?: "REGISTERED" | "MISSING" | null;
}

export type ReadingType =
  | "OPENING"
  | "CLOSING"
  | "INTERMEDIATE"
  | "MAINTENANCE"
  | "CORRECTION";

export type ReadingStatus =
  | "VALIDATED"
  | "PENDING"
  | "SUSPECT"
  | "CORRECTED"
  | "REJECTED";

export interface HourMeterReading {
  id: string;
  machineId: string;
  machineFleetNumber: number;
  model: string;
  value: number;
  differenceFromPrevious: number | null;
  readingType: ReadingType;
  capturedAtDevice: string;
  receivedAtServer: string;
  syncedAt: string | null;
  clientEventId: string;
  operatorName: string | null;
  workSiteName: string | null;
  status: ReadingStatus;
  photoEvidenceUrl: string | null;
  ocrDetectedValue: number | null;
  ocrConfidence: number | null;
}

export interface WorkSite {
  id: string;
  name: string;
}

export interface MachineQrCode {
  id: string;
  machineId: string;
  token: string;
  active: boolean;
  createdAt: string;
  revokedAt: string | null;
}

export interface PhotoEvidence {
  id: string;
  readingId: string;
  storagePath: string;
  hash: string;
  mimeType: string;
  size: number;
  createdAt: string;
}

export interface OcrResult {
  id: string;
  readingId: string;
  rawText: string;
  detectedValue: number | null;
  confidence: number | null;
  engine: string;
  engineVersion: string;
  processedAt: string;
}

export type AnomalyType =
  | "READING_DECREASE"
  | "IMPOSSIBLE_HOUR_INCREASE"
  | "DUPLICATE_READING"
  | "MISSING_READING"
  | "LOW_OCR_CONFIDENCE"
  | "OCR_OPERATOR_DIVERGENCE"
  | "LATE_SYNC";

export interface Anomaly {
  id: string;
  type: AnomalyType;
  readingId: string;
  createdAt: string;
}

export interface AuditLog {
  id: string;
  action: string;
  entity: string;
  entityId: string;
  createdAt: string;
  correlationId: string;
}
