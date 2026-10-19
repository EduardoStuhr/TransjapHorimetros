import type { AuthUser } from "@/lib/auth/types";
import {
  OFFLINE_SCHEMA_VERSION,
  type CachedMachine,
  type CachedWorkSite,
  type CleanupOptions,
  type EnqueueReadingInput,
  type EnqueueReadingResult,
  type OfflineEvent,
  type OfflinePhotoEvent,
  type OfflineReadingEvent,
  type OfflineReferenceDataResponse,
  type OfflineStore,
  type QueueSnapshot,
  type StoredPhotoBlob,
} from "@/offline/types";

export const OFFLINE_DB_NAME = "transjap-horimetros-offline";
export const OFFLINE_DB_VERSION = 1;

const stores = {
  events: "events",
  photos: "photos",
  machines: "machines",
  worksites: "worksites",
  metadata: "metadata",
} as const;

interface MetadataRecord<T = unknown> {
  key: string;
  value: T;
}

function requestResult<T>(request: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error ?? new Error("Falha ao acessar o armazenamento offline."));
  });
}

function transactionDone(transaction: IDBTransaction): Promise<void> {
  return new Promise((resolve, reject) => {
    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error ?? new Error("Falha na transação offline."));
    transaction.onabort = () => reject(transaction.error ?? new Error("Transação offline cancelada."));
  });
}

function sortEvents(events: OfflineEvent[]) {
  return events.sort((left, right) =>
    left.createdAtLocal.localeCompare(right.createdAtLocal)
      || left.localEventId.localeCompare(right.localEventId));
}

function newBaseEvent(
  input: EnqueueReadingInput,
  eventType: OfflineEvent["eventType"],
  clientEventId: string,
  localEventId: string,
  createdAtLocal: string,
): Omit<OfflineEvent, "payload"> {
  return {
    localEventId,
    clientEventId,
    eventType,
    createdAtLocal,
    capturedAtDevice: input.reading.capturedAtDevice,
    status: "PENDING",
    retryCount: 0,
    lastAttemptAt: null,
    firstSyncAttemptAtLocal: null,
    nextAttemptAt: null,
    lastError: null,
    syncedAt: null,
    serverReadingId: null,
    parentReadingClientEventId: null,
    userId: input.user.id,
    userEmail: input.user.email,
    schemaVersion: OFFLINE_SCHEMA_VERSION,
  };
}

function parseFleetNumber(value: string): number | null {
  const normalized = value.trim();
  if (/^\d{4}$/.test(normalized)) {
    const physicalQrFleet = Number(normalized);
    return physicalQrFleet > 0 ? physicalQrFleet : null;
  }
  const match = /^(?:FROTA(?:\s+|\s*:\s*))?([1-9]\d*)$/i.exec(normalized);
  if (!match) return null;
  const parsed = Number(match[1]);
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : null;
}

export class IndexedDbOfflineStore implements OfflineStore {
  private databasePromise: Promise<IDBDatabase> | null = null;

  constructor(private readonly databaseName = OFFLINE_DB_NAME) {}

  private open(): Promise<IDBDatabase> {
    if (this.databasePromise) return this.databasePromise;

    this.databasePromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(this.databaseName, OFFLINE_DB_VERSION);
      request.onupgradeneeded = () => {
        const database = request.result;
        if (!database.objectStoreNames.contains(stores.events)) {
          const eventStore = database.createObjectStore(stores.events, { keyPath: "localEventId" });
          eventStore.createIndex("clientEventId", "clientEventId", { unique: true });
          eventStore.createIndex("userId", "userId");
          eventStore.createIndex("status", "status");
          eventStore.createIndex("createdAtLocal", "createdAtLocal");
        }
        if (!database.objectStoreNames.contains(stores.photos)) {
          database.createObjectStore(stores.photos, { keyPath: "blobKey" });
        }
        if (!database.objectStoreNames.contains(stores.machines)) {
          database.createObjectStore(stores.machines, { keyPath: "id" });
        }
        if (!database.objectStoreNames.contains(stores.worksites)) {
          database.createObjectStore(stores.worksites, { keyPath: "id" });
        }
        if (!database.objectStoreNames.contains(stores.metadata)) {
          database.createObjectStore(stores.metadata, { keyPath: "key" });
        }
      };
      request.onsuccess = () => {
        request.result.onversionchange = () => request.result.close();
        resolve(request.result);
      };
      request.onerror = () => {
        this.databasePromise = null;
        reject(request.error ?? new Error("Não foi possível abrir o armazenamento offline."));
      };
      request.onblocked = () => reject(new Error("Feche outras abas para atualizar o armazenamento offline."));
    });

    return this.databasePromise;
  }

  close() {
    void this.databasePromise?.then((database) => database.close());
    this.databasePromise = null;
  }

  async enqueueEvent(event: OfflineEvent) {
    const database = await this.open();
    const transaction = database.transaction(stores.events, "readwrite");
    transaction.objectStore(stores.events).add(event);
    await transactionDone(transaction);
  }

  async enqueueReadingWithPhoto(input: EnqueueReadingInput): Promise<EnqueueReadingResult> {
    const createdAtLocal = new Date().toISOString();
    const readingClientEventId = crypto.randomUUID();
    const photoClientEventId = crypto.randomUUID();
    const readingLocalEventId = crypto.randomUUID();
    const photoLocalEventId = crypto.randomUUID();
    const blobKey = `photo:${photoLocalEventId}`;

    const readingEvent: OfflineReadingEvent = {
      ...newBaseEvent(input, "READING_CREATED", readingClientEventId, readingLocalEventId, createdAtLocal),
      eventType: "READING_CREATED",
      payload: input.reading,
    };
    const photoEvent: OfflinePhotoEvent = {
      ...newBaseEvent(input, "PHOTO_UPLOADED", photoClientEventId, photoLocalEventId, createdAtLocal),
      eventType: "PHOTO_UPLOADED",
      parentReadingClientEventId: readingClientEventId,
      payload: {
        parentReadingClientEventId: readingClientEventId,
        blobKey,
        fileName: input.photoFileName,
        mimeType: input.photoMimeType,
        size: input.photo.size,
        sha256: input.photoSha256,
        capturedAtDevice: input.reading.capturedAtDevice,
        deviceId: input.reading.deviceId,
      },
    };
    const storedPhoto: StoredPhotoBlob = {
      blobKey,
      blob: input.photo,
      createdAtLocal,
      fileName: input.photoFileName,
      mimeType: input.photoMimeType,
      sha256: input.photoSha256,
      size: input.photo.size,
    };

    const database = await this.open();
    const transaction = database.transaction([stores.events, stores.photos], "readwrite");
    transaction.objectStore(stores.events).add(readingEvent);
    transaction.objectStore(stores.photos).add(storedPhoto);
    transaction.objectStore(stores.events).add(photoEvent);
    await transactionDone(transaction);
    return { readingEvent, photoEvent };
  }

  async getEvent(localEventId: string) {
    const database = await this.open();
    const transaction = database.transaction(stores.events, "readonly");
    return requestResult<OfflineEvent | undefined>(transaction.objectStore(stores.events).get(localEventId));
  }

  async getEventByClientEventId(clientEventId: string) {
    const database = await this.open();
    const transaction = database.transaction(stores.events, "readonly");
    return requestResult<OfflineEvent | undefined>(
      transaction.objectStore(stores.events).index("clientEventId").get(clientEventId),
    );
  }

  async getEventsForUser(userId: string) {
    const database = await this.open();
    const transaction = database.transaction(stores.events, "readonly");
    const events = await requestResult<OfflineEvent[]>(
      transaction.objectStore(stores.events).index("userId").getAll(userId),
    );
    return sortEvents(events);
  }

  async getPendingEvents(userId: string, now = new Date(), includeBackoff = false) {
    const events = await this.getEventsForUser(userId);
    const timestamp = now.getTime();
    return events.filter((event) => {
      if (event.status === "PENDING") return true;
      if (event.status !== "FAILED_RETRYABLE") return false;
      return includeBackoff || !event.nextAttemptAt || new Date(event.nextAttemptAt).getTime() <= timestamp;
    });
  }

  async getQueueSnapshot(userId: string): Promise<QueueSnapshot> {
    const database = await this.open();
    const transaction = database.transaction(stores.events, "readonly");
    const allEvents = sortEvents(await requestResult<OfflineEvent[]>(transaction.objectStore(stores.events).getAll()));
    const events = allEvents.filter((event) => event.userId === userId);
    const byClientEventId = new Map(events.map((event) => [event.clientEventId, event]));
    const blockedByDependency = events.filter((event) => {
      if (event.eventType !== "PHOTO_UPLOADED" || !event.parentReadingClientEventId) return false;
      return byClientEventId.get(event.parentReadingClientEventId)?.status !== "SYNCED";
    }).length;
    const activeStatuses = new Set(["PENDING", "SYNCING", "FAILED_RETRYABLE", "FAILED_PERMANENT"]);

    return {
      events,
      pending: events.filter((event) => event.status === "PENDING").length,
      syncing: events.filter((event) => event.status === "SYNCING").length,
      synced: events.filter((event) => event.status === "SYNCED").length,
      retryableFailures: events.filter((event) => event.status === "FAILED_RETRYABLE").length,
      permanentFailures: events.filter((event) => event.status === "FAILED_PERMANENT").length,
      blockedByDependency,
      otherUserPending: allEvents.filter(
        (event) => event.userId !== userId && activeStatuses.has(event.status),
      ).length,
    };
  }

  private async updateEvent(localEventId: string, updater: (event: OfflineEvent) => void) {
    const database = await this.open();
    const transaction = database.transaction(stores.events, "readwrite");
    const eventStore = transaction.objectStore(stores.events);
    const event = await requestResult<OfflineEvent | undefined>(eventStore.get(localEventId));
    if (!event) {
      transaction.abort();
      throw new Error("Evento offline não encontrado.");
    }
    updater(event);
    eventStore.put(event);
    await transactionDone(transaction);
  }

  markSyncing(localEventId: string, attemptedAt: string) {
    return this.updateEvent(localEventId, (event) => {
      event.status = "SYNCING";
      event.lastAttemptAt = attemptedAt;
      event.firstSyncAttemptAtLocal ??= attemptedAt;
      event.lastError = null;
    });
  }

  markSynced(localEventId: string, syncedAt: string, serverReadingId?: string) {
    return this.updateEvent(localEventId, (event) => {
      event.status = "SYNCED";
      event.syncedAt = syncedAt;
      event.nextAttemptAt = null;
      event.lastError = null;
      if (serverReadingId) event.serverReadingId = serverReadingId;
    });
  }

  markRetryableFailure(localEventId: string, error: string, nextAttemptAt: string) {
    return this.updateEvent(localEventId, (event) => {
      event.status = "FAILED_RETRYABLE";
      event.retryCount += 1;
      event.lastError = error;
      event.nextAttemptAt = nextAttemptAt;
    });
  }

  markPermanentFailure(localEventId: string, error: string) {
    return this.updateEvent(localEventId, (event) => {
      event.status = "FAILED_PERMANENT";
      event.retryCount += 1;
      event.lastError = error;
      event.nextAttemptAt = null;
    });
  }

  retryEvent(localEventId: string) {
    return this.updateEvent(localEventId, (event) => {
      if (event.status !== "FAILED_RETRYABLE") return;
      event.status = "PENDING";
      event.nextAttemptAt = null;
      event.lastError = null;
    });
  }

  async resetStaleSyncing(userId: string) {
    const events = await this.getEventsForUser(userId);
    await Promise.all(events.filter((event) => event.status === "SYNCING").map((event) =>
      this.updateEvent(event.localEventId, (stored) => {
        stored.status = "FAILED_RETRYABLE";
        stored.lastError = "A sincronização anterior foi interrompida e será retomada.";
        stored.nextAttemptAt = null;
      })));
  }

  async storePhotoBlob(photo: StoredPhotoBlob) {
    const database = await this.open();
    const transaction = database.transaction(stores.photos, "readwrite");
    transaction.objectStore(stores.photos).put(photo);
    await transactionDone(transaction);
  }

  async getPhotoBlob(blobKey: string) {
    const database = await this.open();
    const transaction = database.transaction(stores.photos, "readonly");
    return requestResult<StoredPhotoBlob | undefined>(transaction.objectStore(stores.photos).get(blobKey));
  }

  async deletePhotoBlobWhenSafe(blobKey: string) {
    const database = await this.open();
    const transaction = database.transaction(stores.photos, "readwrite");
    transaction.objectStore(stores.photos).delete(blobKey);
    await transactionDone(transaction);
  }

  async removeSyncedDataWhenSafe(options: CleanupOptions) {
    const now = options.now ?? new Date();
    const blobCutoff = now.getTime() - options.blobRetentionMs;
    const eventCutoff = now.getTime() - options.eventRetentionMs;
    const database = await this.open();
    const transaction = database.transaction([stores.events, stores.photos], "readwrite");
    const eventStore = transaction.objectStore(stores.events);
    const photoStore = transaction.objectStore(stores.photos);
    const events = await requestResult<OfflineEvent[]>(eventStore.getAll());

    for (const event of events) {
      if (event.status !== "SYNCED" || !event.syncedAt) continue;
      const syncedAt = new Date(event.syncedAt).getTime();
      if (event.eventType === "PHOTO_UPLOADED" && syncedAt <= blobCutoff) {
        const payload = event.payload as OfflinePhotoEvent["payload"];
        photoStore.delete(payload.blobKey);
      }
      if (syncedAt <= eventCutoff) eventStore.delete(event.localEventId);
    }

    await transactionDone(transaction);
  }

  async replaceReferenceData(data: OfflineReferenceDataResponse) {
    const fetchedAt = new Date().toISOString();
    const database = await this.open();
    const transaction = database.transaction([stores.machines, stores.worksites, stores.metadata], "readwrite");
    const machineStore = transaction.objectStore(stores.machines);
    const worksiteStore = transaction.objectStore(stores.worksites);
    machineStore.clear();
    worksiteStore.clear();
    for (const machine of data.machines) {
      const cached: CachedMachine = {
        id: machine.id,
        fleetNumber: machine.fleetNumber,
        model: machine.model,
        meterUnit: machine.meterUnit,
        status: "ACTIVE",
        activeQrToken: machine.activeQrToken,
        updatedAtServer: machine.updatedAt,
        fetchedAt,
        cacheVersion: data.schemaVersion,
      };
      machineStore.put(cached);
    }
    for (const workSite of data.workSites) {
      const cached: CachedWorkSite = {
        id: workSite.id,
        name: workSite.name,
        active: true,
        updatedAtServer: workSite.updatedAt,
        fetchedAt,
        cacheVersion: data.schemaVersion,
      };
      worksiteStore.put(cached);
    }
    transaction.objectStore(stores.metadata).put({ key: "reference-data-fetched-at", value: fetchedAt });
    await transactionDone(transaction);
  }

  async getCachedMachines() {
    const database = await this.open();
    const transaction = database.transaction(stores.machines, "readonly");
    const machines = await requestResult<CachedMachine[]>(transaction.objectStore(stores.machines).getAll());
    return machines.sort((left, right) => left.fleetNumber - right.fleetNumber);
  }

  async getCachedWorkSites() {
    const database = await this.open();
    const transaction = database.transaction(stores.worksites, "readonly");
    const workSites = await requestResult<CachedWorkSite[]>(transaction.objectStore(stores.worksites).getAll());
    return workSites.sort((left, right) => left.name.localeCompare(right.name, "pt-BR"));
  }

  async resolveCachedMachine(value: string) {
    const normalized = value.trim();
    const machines = await this.getCachedMachines();
    const qrMatch = machines.find((machine) => machine.activeQrToken === normalized);
    if (qrMatch) return qrMatch;
    const fleetNumber = parseFleetNumber(normalized);
    return fleetNumber ? machines.find((machine) => machine.fleetNumber === fleetNumber) : undefined;
  }

  async getReferenceDataFetchedAt() {
    return this.getMetadata<string>("reference-data-fetched-at");
  }

  async getOrCreateDeviceId() {
    const existing = await this.getMetadata<string>("device-id");
    if (existing) return existing;
    const deviceId = `web-${crypto.randomUUID()}`;
    await this.putMetadata("device-id", deviceId);
    return deviceId;
  }

  cacheAuthenticatedUser(user: AuthUser) {
    return this.putMetadata("authenticated-user", user);
  }

  async getCachedAuthenticatedUser() {
    return (await this.getMetadata<AuthUser>("authenticated-user")) ?? null;
  }

  async clearCachedAuthenticatedUser() {
    const database = await this.open();
    const transaction = database.transaction(stores.metadata, "readwrite");
    transaction.objectStore(stores.metadata).delete("authenticated-user");
    await transactionDone(transaction);
  }

  private async getMetadata<T>(key: string): Promise<T | null> {
    const database = await this.open();
    const transaction = database.transaction(stores.metadata, "readonly");
    const record = await requestResult<MetadataRecord<T> | undefined>(
      transaction.objectStore(stores.metadata).get(key),
    );
    return record?.value ?? null;
  }

  private async putMetadata<T>(key: string, value: T) {
    const database = await this.open();
    const transaction = database.transaction(stores.metadata, "readwrite");
    transaction.objectStore(stores.metadata).put({ key, value });
    await transactionDone(transaction);
  }
}

let singleton: IndexedDbOfflineStore | null = null;

export function getOfflineStore() {
  singleton ??= new IndexedDbOfflineStore();
  return singleton;
}
