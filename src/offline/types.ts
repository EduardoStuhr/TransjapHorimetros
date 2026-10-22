import type { AuthUser } from "@/lib/auth/types";
import type { MeterUnit, ReadingType } from "@/types/domain";

export const OFFLINE_SCHEMA_VERSION = 1;

export type RegisteredDeviceStatus = "PENDING" | "AUTHORIZED" | "REVOKED";

export interface StoredDeviceIdentity {
  key: "current";
  schemaVersion: 1;
  localDeviceId: string;
  registeredDeviceId: string | null;
  displayName: string;
  platform: "WEB_PWA";
  keyAlgorithm: "ECDSA_P256_SHA256";
  publicKeySpki: string;
  privateKey: CryptoKey;
  publicKeyFingerprint: string | null;
  status: "UNREGISTERED" | RegisteredDeviceStatus;
  lastValidatedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export type OfflineEventType = "READING_CREATED" | "PHOTO_UPLOADED";
export type OfflineEventStatus =
  | "PENDING"
  | "SYNCING"
  | "SYNCED"
  | "FAILED_RETRYABLE"
  | "FAILED_PERMANENT";

export interface OfflineReadingPayload {
  machineId: string;
  workSiteId: string;
  value: number;
  readingType: Exclude<ReadingType, "CORRECTION">;
  capturedAtDevice: string;
  deviceId: string;
  deviceIdentity?: {
    localDeviceId: string;
    registeredDeviceId: string;
    status: "AUTHORIZED";
  };
}

export interface OfflinePhotoPayload {
  parentReadingClientEventId: string;
  blobKey: string;
  fileName: string;
  mimeType: "image/jpeg" | "image/png" | "image/webp";
  size: number;
  sha256: string;
  capturedAtDevice: string;
  deviceId: string;
}

export interface OfflineEvent<TPayload = OfflineReadingPayload | OfflinePhotoPayload> {
  localEventId: string;
  clientEventId: string;
  eventType: OfflineEventType;
  payload: TPayload;
  createdAtLocal: string;
  capturedAtDevice: string;
  status: OfflineEventStatus;
  retryCount: number;
  lastAttemptAt: string | null;
  firstSyncAttemptAtLocal: string | null;
  nextAttemptAt: string | null;
  lastError: string | null;
  syncedAt: string | null;
  serverReadingId: string | null;
  parentReadingClientEventId: string | null;
  userId: string;
  userEmail: string;
  schemaVersion: number;
}

export type OfflineReadingEvent = OfflineEvent<OfflineReadingPayload> & {
  eventType: "READING_CREATED";
};

export type OfflinePhotoEvent = OfflineEvent<OfflinePhotoPayload> & {
  eventType: "PHOTO_UPLOADED";
};

export interface StoredPhotoBlob {
  blobKey: string;
  blob: Blob;
  createdAtLocal: string;
  fileName: string;
  mimeType: string;
  sha256: string;
  size: number;
}

export interface CachedMachine {
  id: string;
  fleetNumber: number;
  model: string;
  meterUnit: MeterUnit | null;
  status: "ACTIVE";
  activeQrToken: string | null;
  updatedAtServer: string;
  fetchedAt: string;
  cacheVersion: number;
}

export interface CachedWorkSite {
  id: string;
  name: string;
  active: true;
  updatedAtServer: string;
  fetchedAt: string;
  cacheVersion: number;
}

export interface OfflineReferenceDataResponse {
  schemaVersion: number;
  generatedAt: string;
  machines: Array<{
    id: string;
    fleetNumber: number;
    model: string;
    meterUnit: MeterUnit | null;
    status: "ACTIVE";
    activeQrToken: string | null;
    updatedAt: string;
  }>;
  workSites: Array<{
    id: string;
    name: string;
    active: true;
    updatedAt: string;
  }>;
}

export interface QueueSnapshot {
  events: OfflineEvent[];
  pending: number;
  syncing: number;
  synced: number;
  retryableFailures: number;
  permanentFailures: number;
  blockedByDependency: number;
  otherUserPending: number;
}

export interface EnqueueReadingInput {
  reading: OfflineReadingPayload;
  photo: File | Blob;
  photoFileName: string;
  photoMimeType: OfflinePhotoPayload["mimeType"];
  photoSha256: string;
  user: Pick<AuthUser, "id" | "email">;
}

export interface EnqueueReadingResult {
  readingEvent: OfflineReadingEvent;
  photoEvent: OfflinePhotoEvent;
}

export interface CleanupOptions {
  blobRetentionMs: number;
  eventRetentionMs: number;
  now?: Date;
}

export interface OfflineStore {
  enqueueEvent(event: OfflineEvent): Promise<void>;
  enqueueReadingWithPhoto(input: EnqueueReadingInput): Promise<EnqueueReadingResult>;
  getEvent(localEventId: string): Promise<OfflineEvent | undefined>;
  getEventByClientEventId(clientEventId: string): Promise<OfflineEvent | undefined>;
  getEventsForUser(userId: string): Promise<OfflineEvent[]>;
  getPendingEvents(userId: string, now?: Date, includeBackoff?: boolean): Promise<OfflineEvent[]>;
  getQueueSnapshot(userId: string): Promise<QueueSnapshot>;
  markSyncing(localEventId: string, attemptedAt: string): Promise<void>;
  markSynced(localEventId: string, syncedAt: string, serverReadingId?: string): Promise<void>;
  markRetryableFailure(localEventId: string, error: string, nextAttemptAt: string): Promise<void>;
  markPermanentFailure(localEventId: string, error: string): Promise<void>;
  resetStaleSyncing(userId: string): Promise<void>;
  retryEvent(localEventId: string): Promise<void>;
  storePhotoBlob(photo: StoredPhotoBlob): Promise<void>;
  getPhotoBlob(blobKey: string): Promise<StoredPhotoBlob | undefined>;
  deletePhotoBlobWhenSafe(blobKey: string): Promise<void>;
  removeSyncedDataWhenSafe(options: CleanupOptions): Promise<void>;
  replaceReferenceData(data: OfflineReferenceDataResponse): Promise<void>;
  getCachedMachines(): Promise<CachedMachine[]>;
  getCachedWorkSites(): Promise<CachedWorkSite[]>;
  resolveCachedMachine(value: string): Promise<CachedMachine | undefined>;
  getReferenceDataFetchedAt(): Promise<string | null>;
  getOrCreateDeviceId(): Promise<string>;
  getDeviceIdentity(): Promise<StoredDeviceIdentity | null>;
  saveDeviceIdentity(identity: StoredDeviceIdentity): Promise<void>;
  cacheAuthenticatedUser(user: AuthUser): Promise<void>;
  getCachedAuthenticatedUser(): Promise<AuthUser | null>;
  clearCachedAuthenticatedUser(): Promise<void>;
  close(): void;
}
