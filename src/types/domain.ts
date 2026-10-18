export interface Machine {
  id: string;
  fleetNumber: number;
  model: string;
  meterUnit?: MeterUnit | null;
  currentReadingMeterUnit?: MeterUnit | null;
  manufacturer?: string | null;
  registrationStatus?: "ACTIVE" | "INACTIVE" | null;
  operationalStatus?: string | null;
  currentHourMeter?: number | null;
  lastReadingAt?: string | null;
  workSiteName?: string | null;
  qrCodeStatus?: "REGISTERED" | "MISSING" | null;
}

export interface MachineDetails extends Machine {
  recentReadings: readonly HourMeterReading[];
  alertCount: number;
  qrCode: string | null;
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

export type MeterUnit = "HOURS" | "KM";

export interface HourMeterReading {
  id: string;
  machineId: string;
  machineFleetNumber: number;
  model: string;
  meterUnit: MeterUnit | null;
  value: number;
  differenceFromPrevious: number | null;
  readingType: ReadingType;
  capturedAtDevice: string;
  receivedAtServer: string;
  syncedAt: string | null;
  clientEventId: string;
  operatorName: string | null;
  workSiteId: string | null;
  workSiteName: string | null;
  status: ReadingStatus;
  hasPhotoEvidence: boolean;
  ocrDetectedValue: number | null;
  ocrConfidence: number | null;
  ocrStatus: PhotoOcrResult["status"] | null;
  ocrMatchesOperatorValue: boolean | null;
  createdByUserId: string | null;
  createdByEmail: string | null;
  deviceId: string | null;
  previousReadingId: string | null;
  correctionOfReadingId: string | null;
  anomalyCount: number;
  openAnomalyCount: number;
  correctionCount: number;
  anomalies: readonly ReadingAnomalySummary[] | null;
  corrections: readonly ReadingCorrectionSummary[] | null;
  createdAt: string;
  updatedAt: string;
}

export interface ReadingAnomalySummary {
  id: string;
  type: AnomalyType;
  status: Anomaly["status"];
  severity: Anomaly["severity"];
  description: string;
}
export interface ReadingCorrectionSummary { id: string; value: number; status: ReadingStatus; createdAt: string }

export interface WorkSite {
  id: string;
  name: string;
  code: string;
  active: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MachineQrCode {
  id: string;
  machineId: string;
  token: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  deactivatedAt: string | null;
}

export interface PhotoEvidence {
  id: string;
  readingId: string;
  originalFileName: string;
  contentType: "image/jpeg" | "image/png" | "image/webp";
  fileSizeBytes: number;
  sha256: string;
  capturedAtDevice: string | null;
  uploadedAtServer: string;
  width: number | null;
  height: number | null;
  status: "ACTIVE" | "REVOKED" | "INVALID";
  createdByUserId: string | null;
  createdByEmail: string | null;
  createdAt: string;
  deviceId: string | null;
  clientEventId: string | null;
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
  | "LATE_SYNC"
  | "CLOCK_SKEW"
  | "DAY_TRANSITION_MISMATCH"
  | "WRONG_WORK_SITE";

export interface Anomaly {
  id: string;
  type: AnomalyType;
  readingId: string;
  machineId: string;
  machineFleetNumber: number;
  severity: "INFO" | "WARNING" | "CRITICAL";
  description: string;
  status: "OPEN" | "ACKNOWLEDGED" | "RESOLVED";
  createdAt: string;
}

export interface DashboardSummary {
  totalMachines: number;
  updatedToday: number;
  withoutReading: number;
  withoutReadingIsDefined: boolean;
  withoutReadingDefinition: string;
  pendingReadings: number;
  suspectReadings: number;
  alerts: number;
}

export interface AuditLog {
  id: string;
  action: string;
  entity: string;
  entityId: string;
  createdAt: string;
  correlationId: string;
}

export interface PhotoOcrResult {
  id: string;
  photoEvidenceId: string;
  status: "PENDING" | "PROCESSING" | "SUCCEEDED" | "FAILED" | "NO_VALUE_DETECTED" | "LOW_CONFIDENCE";
  detectedValue: number | null;
  confidence: number | null;
  processedAtServer: string | null;
  provider: string;
  providerModel: string | null;
  matchesOperatorValue: boolean | null;
  absoluteDifference: number | null;
  percentageDifference: number | null;
  errorCode: string | null;
  errorMessage: string | null;
  createdAt: string;
}
