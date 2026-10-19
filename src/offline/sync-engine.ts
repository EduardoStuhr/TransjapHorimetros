import { sha256Hex } from "@/offline/crypto";
import { SyncTransportError, type SyncResponse, type SyncTransport } from "@/offline/sync-transport";
import type {
  OfflineEvent,
  OfflinePhotoEvent,
  OfflineReadingEvent,
  OfflineStore,
} from "@/offline/types";

const retryableStatuses = new Set([408, 429, 500, 502, 503, 504]);
const permanentStatuses = new Set([400, 403, 404, 413, 422]);
const backoffSteps = [5_000, 30_000, 120_000, 600_000, 1_800_000];

export type ConnectivityState = "ONLINE" | "OFFLINE" | "UNKNOWN";

export interface SyncRunResult {
  processed: number;
  synced: number;
  retryableFailures: number;
  permanentFailures: number;
  blockedByDependency: number;
  authenticationRequired: boolean;
  skippedBecauseLocked: boolean;
}

export interface SyncEngineOptions {
  now?: () => Date;
  onConnectivityChange?: (state: ConnectivityState) => void;
  onEventChanged?: (event: OfflineEvent) => void;
  onAuthenticationRequired?: () => void;
}

function retryDelay(retryCount: number) {
  return backoffSteps[Math.min(retryCount, backoffSteps.length - 1)];
}

function emptyResult(): SyncRunResult {
  return {
    processed: 0,
    synced: 0,
    retryableFailures: 0,
    permanentFailures: 0,
    blockedByDependency: 0,
    authenticationRequired: false,
    skippedBecauseLocked: false,
  };
}

function isIdempotentConflict(error: SyncTransportError) {
  return error.status === 409 && Boolean(error.resourceId);
}

function responseReadingId(response: SyncResponse) {
  return response.resourceId;
}

let fallbackLock = false;

async function withSyncLock<T>(operation: () => Promise<T>): Promise<T | null> {
  if (typeof navigator !== "undefined" && navigator.locks) {
    return navigator.locks.request(
      "transjap-offline-sync",
      { mode: "exclusive", ifAvailable: true },
      async (lock) => lock ? operation() : null,
    );
  }
  if (fallbackLock) return null;
  fallbackLock = true;
  try {
    return await operation();
  } finally {
    fallbackLock = false;
  }
}

export class OfflineSyncService {
  private readonly now: () => Date;

  constructor(
    private readonly store: OfflineStore,
    private readonly transport: SyncTransport,
    private readonly options: SyncEngineOptions = {},
  ) {
    this.now = options.now ?? (() => new Date());
  }

  async sync(userId: string, manual = false): Promise<SyncRunResult> {
    const result = await withSyncLock(() => this.processQueue(userId, manual));
    if (result) return result;
    return { ...emptyResult(), skippedBecauseLocked: true };
  }

  private async processQueue(userId: string, manual: boolean) {
    const result = emptyResult();
    await this.store.resetStaleSyncing(userId);
    let madeProgress = true;
    const visited = new Set<string>();

    while (madeProgress) {
      madeProgress = false;
      const candidates = await this.store.getPendingEvents(userId, this.now(), manual);
      for (const event of candidates) {
        if (visited.has(event.localEventId)) continue;
        const dependency = await this.resolveDependency(event);
        if (dependency.blocked) {
          result.blockedByDependency += 1;
          continue;
        }

        visited.add(event.localEventId);
        madeProgress = true;
        result.processed += 1;
        const shouldStop = await this.processEvent(event, dependency.serverReadingId, result);
        if (shouldStop) return result;
      }
    }

    await this.store.removeSyncedDataWhenSafe({
      blobRetentionMs: 5 * 60_000,
      eventRetentionMs: 7 * 24 * 60 * 60_000,
      now: this.now(),
    });
    return result;
  }

  private async resolveDependency(event: OfflineEvent) {
    if (event.eventType !== "PHOTO_UPLOADED" || !event.parentReadingClientEventId) {
      return { blocked: false, serverReadingId: null as string | null };
    }
    const parent = await this.store.getEventByClientEventId(event.parentReadingClientEventId);
    if (!parent || parent.status !== "SYNCED" || !parent.serverReadingId) {
      return { blocked: true, serverReadingId: null as string | null };
    }
    return { blocked: false, serverReadingId: parent.serverReadingId };
  }

  private async processEvent(
    event: OfflineEvent,
    dependencyReadingId: string | null,
    result: SyncRunResult,
  ) {
    const attemptedAt = this.now().toISOString();
    await this.store.markSyncing(event.localEventId, attemptedAt);
    this.options.onEventChanged?.({ ...event, status: "SYNCING", lastAttemptAt: attemptedAt });

    try {
      const response = event.eventType === "READING_CREATED"
        ? await this.transport.createReading(event as OfflineReadingEvent)
        : await this.uploadPhoto(event as OfflinePhotoEvent, dependencyReadingId);
      const readingId = event.eventType === "READING_CREATED"
        ? responseReadingId(response)
        : dependencyReadingId;
      if (!readingId) throw new SyncTransportError("O servidor não informou o identificador da leitura.", 409);
      const syncedAt = this.now().toISOString();
      await this.store.markSynced(event.localEventId, syncedAt, readingId);
      result.synced += 1;
      this.options.onConnectivityChange?.("ONLINE");
      this.options.onEventChanged?.({ ...event, status: "SYNCED", syncedAt, serverReadingId: readingId });
      return false;
    } catch (caught) {
      const error = caught instanceof SyncTransportError
        ? caught
        : new SyncTransportError("Falha inesperada ao sincronizar. A tentativa será repetida.", null);

      if (isIdempotentConflict(error)) {
        const syncedAt = this.now().toISOString();
        const readingId = event.eventType === "READING_CREATED" ? error.resourceId! : dependencyReadingId!;
        await this.store.markSynced(event.localEventId, syncedAt, readingId);
        result.synced += 1;
        this.options.onConnectivityChange?.("ONLINE");
        return false;
      }

      if (error.status === 401) {
        const nextAttemptAt = new Date(this.now().getTime() + retryDelay(event.retryCount)).toISOString();
        await this.store.markRetryableFailure(event.localEventId, "Sessão expirada. Entre novamente para sincronizar.", nextAttemptAt);
        result.retryableFailures += 1;
        result.authenticationRequired = true;
        this.options.onConnectivityChange?.("ONLINE");
        this.options.onAuthenticationRequired?.();
        return true;
      }

      if (error.status === null || retryableStatuses.has(error.status)) {
        const nextAttemptAt = new Date(this.now().getTime() + retryDelay(event.retryCount)).toISOString();
        await this.store.markRetryableFailure(event.localEventId, error.message, nextAttemptAt);
        result.retryableFailures += 1;
        this.options.onConnectivityChange?.(error.status === null ? "OFFLINE" : "ONLINE");
        return false;
      }

      const message = error.status === 409
        ? `Conflito que precisa de revisão: ${error.message}`
        : error.message;
      if (permanentStatuses.has(error.status ?? 0) || error.status === 409) {
        await this.store.markPermanentFailure(event.localEventId, message);
        result.permanentFailures += 1;
        this.options.onConnectivityChange?.("ONLINE");
        return false;
      }

      const nextAttemptAt = new Date(this.now().getTime() + retryDelay(event.retryCount)).toISOString();
      await this.store.markRetryableFailure(event.localEventId, error.message, nextAttemptAt);
      result.retryableFailures += 1;
      return false;
    }
  }

  private async uploadPhoto(event: OfflinePhotoEvent, readingId: string | null) {
    if (!readingId) throw new SyncTransportError("A leitura da foto ainda não foi sincronizada.", 409);
    const photo = await this.store.getPhotoBlob(event.payload.blobKey);
    if (!photo) throw new SyncTransportError("A foto salva neste dispositivo não foi encontrada.", 422);
    if (photo.size !== event.payload.size || photo.mimeType !== event.payload.mimeType) {
      throw new SyncTransportError("A foto local não corresponde aos metadados salvos.", 422);
    }
    const currentHash = await sha256Hex(photo.blob);
    if (currentHash !== event.payload.sha256) {
      throw new SyncTransportError("A integridade da foto local não pôde ser confirmada.", 422);
    }
    return this.transport.uploadPhoto(event, readingId, photo);
  }
}

export const syncBackoffDelay = retryDelay;
