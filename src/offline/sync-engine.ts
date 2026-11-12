import { sha256Hex } from "@/offline/crypto";
import {
  SyncTransportError,
  type SyncResponse,
  type SyncTransport,
} from "@/offline/sync-transport";
import type {
  OfflineEvent,
  OfflinePhotoEvent,
  OfflineReadingEvent,
  OfflineStore,
} from "@/offline/types";

const retryableStatuses = new Set([408, 425, 429]);
const permanentStatuses = new Set([400, 403, 404, 413, 422]);
export const MAX_SYNC_EVENTS_PER_CYCLE = 50;
const leaseDurationMs = 120_000;

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
  random?: () => number;
  onConnectivityChange?: (state: ConnectivityState) => void;
  onEventChanged?: (event: OfflineEvent) => void;
  onAuthenticationRequired?: () => void;
}

function retryDelay(retryCount: number, random = Math.random) {
  const base = Math.min(
    5_000 * 2 ** Math.min(Math.max(retryCount, 0), 9),
    1_800_000,
  );
  return Math.round(base * (1 + random() * 0.25));
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

function responseReadingId(response: SyncResponse) {
  return response.resourceId;
}

let fallbackLock = false;

async function withSyncLock<T>(operation: () => Promise<T>): Promise<T | null> {
  if (typeof navigator !== "undefined" && navigator.locks) {
    return navigator.locks.request(
      "transjap-offline-sync",
      { mode: "exclusive", ifAvailable: true },
      async (lock) => (lock ? operation() : null),
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

  async sync(
    userId: string,
    manual = false,
    signal?: AbortSignal,
  ): Promise<SyncRunResult> {
    const owner = crypto.randomUUID();
    const result = await withSyncLock(async () => {
      if (
        !(await this.store.acquireSyncLease(owner, Date.now(), leaseDurationMs))
      )
        return null;
      try {
        return await this.processQueue(userId, manual, owner, signal);
      } finally {
        await this.store.releaseSyncLease(owner);
      }
    });
    if (result) return result;
    return { ...emptyResult(), skippedBecauseLocked: true };
  }

  private async processQueue(
    userId: string,
    manual: boolean,
    owner: string,
    signal?: AbortSignal,
  ) {
    const result = emptyResult();
    await this.store.resetStaleSyncing(userId);
    let madeProgress = true;
    const visited = new Set<string>();

    while (madeProgress) {
      madeProgress = false;
      const candidates = await this.store.getPendingEvents(
        userId,
        this.now(),
        manual,
      );
      for (const event of candidates) {
        if (signal?.aborted || result.processed >= MAX_SYNC_EVENTS_PER_CYCLE)
          return result;
        if (visited.has(event.localEventId)) continue;
        if (
          !(await this.store.acquireSyncLease(
            owner,
            Date.now(),
            leaseDurationMs,
          ))
        )
          return result;
        const dependency = await this.resolveDependency(event);
        if (dependency.blocked) {
          result.blockedByDependency += 1;
          continue;
        }

        visited.add(event.localEventId);
        madeProgress = true;
        result.processed += 1;
        const shouldStop = await this.processEvent(
          event,
          dependency.serverReadingId,
          result,
          signal,
        );
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
    if (
      event.eventType !== "PHOTO_UPLOADED" ||
      !event.parentReadingClientEventId
    ) {
      return { blocked: false, serverReadingId: null as string | null };
    }
    const parent = await this.store.getEventByClientEventId(
      event.parentReadingClientEventId,
    );
    if (!parent || parent.status !== "SYNCED" || !parent.serverReadingId) {
      return { blocked: true, serverReadingId: null as string | null };
    }
    return { blocked: false, serverReadingId: parent.serverReadingId };
  }

  private async processEvent(
    event: OfflineEvent,
    dependencyReadingId: string | null,
    result: SyncRunResult,
    signal?: AbortSignal,
  ) {
    const attemptedAt = this.now().toISOString();
    await this.store.markSyncing(event.localEventId, attemptedAt);
    this.options.onEventChanged?.({
      ...event,
      status: "SYNCING",
      lastAttemptAt: attemptedAt,
    });

    try {
      const response =
        event.eventType === "READING_CREATED"
          ? await this.transport.createReading(
              event as OfflineReadingEvent,
              signal,
            )
          : await this.uploadPhoto(
              event as OfflinePhotoEvent,
              dependencyReadingId,
              signal,
            );
      const readingId =
        event.eventType === "READING_CREATED"
          ? responseReadingId(response)
          : dependencyReadingId;
      if (!readingId)
        throw new SyncTransportError(
          "O servidor não informou o identificador da leitura.",
          409,
        );
      const syncedAt = this.now().toISOString();
      await this.store.markSynced(event.localEventId, syncedAt, readingId);
      result.synced += 1;
      this.options.onConnectivityChange?.("ONLINE");
      this.options.onEventChanged?.({
        ...event,
        status: "SYNCED",
        syncedAt,
        serverReadingId: readingId,
      });
      return false;
    } catch (caught) {
      const error =
        caught instanceof SyncTransportError
          ? caught
          : new SyncTransportError(
              "Falha inesperada ao sincronizar. A tentativa será repetida.",
              null,
            );

      if (error.status === 401) {
        const nextAttemptAt = new Date(
          this.now().getTime() +
            Math.max(
              retryDelay(event.retryCount, this.options.random),
              error.retryAfterMs,
            ),
        ).toISOString();
        await this.store.markRetryableFailure(
          event.localEventId,
          "Sessão expirada. Entre novamente para sincronizar.",
          nextAttemptAt,
        );
        result.retryableFailures += 1;
        result.authenticationRequired = true;
        this.options.onConnectivityChange?.("ONLINE");
        this.options.onAuthenticationRequired?.();
        return true;
      }

      if (
        error.status === null ||
        retryableStatuses.has(error.status) ||
        error.status >= 500
      ) {
        const nextAttemptAt = new Date(
          this.now().getTime() +
            Math.max(
              retryDelay(event.retryCount, this.options.random),
              error.retryAfterMs,
            ),
        ).toISOString();
        await this.store.markRetryableFailure(
          event.localEventId,
          error.message,
          nextAttemptAt,
          error.retryAfterMs > 0
            ? new Date(this.now().getTime() + error.retryAfterMs).toISOString()
            : undefined,
        );
        result.retryableFailures += 1;
        this.options.onConnectivityChange?.(
          error.status === null ? "OFFLINE" : "ONLINE",
        );
        return Boolean(signal?.aborted);
      }

      const message =
        error.status === 409
          ? `Conflito que precisa de revisão: ${error.message}`
          : error.message;
      if (
        permanentStatuses.has(error.status ?? 0) ||
        (error.status >= 400 && error.status < 500)
      ) {
        await this.store.markPermanentFailure(event.localEventId, message);
        result.permanentFailures += 1;
        this.options.onConnectivityChange?.("ONLINE");
        return false;
      }

      const nextAttemptAt = new Date(
        this.now().getTime() +
          Math.max(
            retryDelay(event.retryCount, this.options.random),
            error.retryAfterMs,
          ),
      ).toISOString();
      await this.store.markRetryableFailure(
        event.localEventId,
        error.message,
        nextAttemptAt,
        error.retryAfterMs > 0
          ? new Date(this.now().getTime() + error.retryAfterMs).toISOString()
          : undefined,
      );
      result.retryableFailures += 1;
      return false;
    }
  }

  private async uploadPhoto(
    event: OfflinePhotoEvent,
    readingId: string | null,
    signal?: AbortSignal,
  ) {
    if (!readingId)
      throw new SyncTransportError(
        "A leitura da foto ainda não foi sincronizada.",
        409,
      );
    const photo = await this.store.getPhotoBlob(event.payload.blobKey);
    if (!photo)
      throw new SyncTransportError(
        "A foto salva neste dispositivo não foi encontrada.",
        422,
      );
    if (
      photo.size !== event.payload.size ||
      photo.mimeType !== event.payload.mimeType
    ) {
      throw new SyncTransportError(
        "A foto local não corresponde aos metadados salvos.",
        422,
      );
    }
    const currentHash = await sha256Hex(photo.blob);
    if (currentHash !== event.payload.sha256) {
      throw new SyncTransportError(
        "A integridade da foto local não pôde ser confirmada.",
        422,
      );
    }
    return this.transport.uploadPhoto(event, readingId, photo, signal);
  }
}

export const syncBackoffDelay = retryDelay;
