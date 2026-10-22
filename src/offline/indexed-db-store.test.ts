import { afterEach, describe, expect, it } from "vitest";

import { IndexedDbOfflineStore, OFFLINE_DB_VERSION } from "@/offline/indexed-db-store";
import { sha256Hex } from "@/offline/crypto";
import { generateDeviceIdentity } from "@/features/device-identity/device-crypto";
import type { EnqueueReadingInput } from "@/offline/types";

const databases: string[] = [];

function createStore() {
  const name = `transjap-test-${crypto.randomUUID()}`;
  databases.push(name);
  return { name, store: new IndexedDbOfflineStore(name) };
}

async function readingInput(): Promise<EnqueueReadingInput> {
  const photo = new Blob([new Uint8Array([1, 2, 3])], { type: "image/jpeg" });
  return {
    reading: {
      machineId: crypto.randomUUID(),
      workSiteId: crypto.randomUUID(),
      value: 123.45,
      readingType: "INTERMEDIATE",
      capturedAtDevice: "2026-10-19T12:00:00.000Z",
      deviceId: "web-test",
    },
    photo,
    photoFileName: "meter.jpg",
    photoMimeType: "image/jpeg",
    photoSha256: await sha256Hex(photo),
    user: { id: "user-1", email: "operator@example.com" },
  };
}

afterEach(async () => {
  for (const name of databases.splice(0)) {
    await new Promise<void>((resolve) => {
      const request = indexedDB.deleteDatabase(name);
      request.onsuccess = () => resolve();
      request.onerror = () => resolve();
      request.onblocked = () => resolve();
    });
  }
});

describe("IndexedDbOfflineStore", () => {
  it("persiste leitura, dependência e blob atomicamente após reabrir o banco", async () => {
    const { name, store } = createStore();
    const queued = await store.enqueueReadingWithPhoto(await readingInput());
    const readingClientEventId = queued.readingEvent.clientEventId;
    const photoClientEventId = queued.photoEvent.clientEventId;
    store.close();

    const reopened = new IndexedDbOfflineStore(name);
    const events = await reopened.getEventsForUser("user-1");
    const photo = await reopened.getPhotoBlob(queued.photoEvent.payload.blobKey);

    expect(events).toHaveLength(2);
    expect(events.find((event) => event.eventType === "READING_CREATED")?.clientEventId).toBe(readingClientEventId);
    expect(events.find((event) => event.eventType === "PHOTO_UPLOADED")?.clientEventId).toBe(photoClientEventId);
    expect(queued.photoEvent.parentReadingClientEventId).toBe(readingClientEventId);
    expect(photo?.size).toBe(3);
    reopened.close();
  });

  it("versiona o schema, mantém cache confiável e resolve QR ou frota", async () => {
    const { name, store } = createStore();
    const machineId = crypto.randomUUID();
    await store.replaceReferenceData({
      schemaVersion: 1,
      generatedAt: new Date().toISOString(),
      machines: [
        {
          id: machineId,
          fleetNumber: 1234,
          model: "Escavadeira",
          meterUnit: "HOURS",
          status: "ACTIVE",
          activeQrToken: "TJ-OFFLINE",
          updatedAt: "2026-10-19T10:00:00Z",
        },
        {
          id: "fleet-68",
          fleetNumber: 68,
          model: "Trator",
          meterUnit: "HOURS",
          status: "ACTIVE",
          activeQrToken: null,
          updatedAt: "2026-10-19T10:00:00Z",
        },
      ],
      workSites: [{ id: crypto.randomUUID(), name: "Obra A", active: true, updatedAt: "2026-10-19T10:00:00Z" }],
    });

    expect((await store.resolveCachedMachine("TJ-OFFLINE"))?.id).toBe(machineId);
    expect((await store.resolveCachedMachine("FROTA 1234"))?.id).toBe(machineId);
    expect((await store.resolveCachedMachine("1234"))?.id).toBe(machineId);
    expect((await store.resolveCachedMachine("0068"))?.id).toBe("fleet-68");
    expect((await store.getCachedWorkSites())[0].name).toBe("Obra A");
    store.close();

    const version = await new Promise<number>((resolve, reject) => {
      const request = indexedDB.open(name);
      request.onsuccess = () => {
        resolve(request.result.version);
        request.result.close();
      };
      request.onerror = () => reject(request.error);
    });
    expect(version).toBe(OFFLINE_DB_VERSION);
  });

  it("remove blob confirmado sem apagar eventos com falha ou pendentes", async () => {
    const { store } = createStore();
    const queued = await store.enqueueReadingWithPhoto(await readingInput());
    await store.markSynced(queued.readingEvent.localEventId, "2026-10-19T12:01:00Z", crypto.randomUUID());
    await store.markSynced(queued.photoEvent.localEventId, "2026-10-19T12:02:00Z", crypto.randomUUID());
    await store.removeSyncedDataWhenSafe({
      blobRetentionMs: 0,
      eventRetentionMs: 30 * 24 * 60 * 60_000,
      now: new Date("2026-10-19T12:03:00Z"),
    });

    expect(await store.getPhotoBlob(queued.photoEvent.payload.blobKey)).toBeUndefined();
    expect(await store.getEventsForUser("user-1")).toHaveLength(2);
    store.close();
  });

  it("persiste e reutiliza o mesmo deviceId entre chamadas e reinicializações", async () => {
    const { name, store } = createStore();
    const deviceId1 = await store.getOrCreateDeviceId();
    const deviceId2 = await store.getOrCreateDeviceId();
    expect(deviceId1).toBe(deviceId2);
    expect(deviceId1).toMatch(/^web-[0-9a-f-]+$/);
    store.close();

    const reopened = new IndexedDbOfflineStore(name);
    const deviceId3 = await reopened.getOrCreateDeviceId();
    expect(deviceId3).toBe(deviceId1);
    reopened.close();
  });

  it("persiste a CryptoKey privada nao exportavel no store dedicado", async () => {
    const { name, store } = createStore();
    const identity = await generateDeviceIdentity(await store.getOrCreateDeviceId(), "Tablet teste");
    await store.saveDeviceIdentity(identity);
    store.close();

    const reopened = new IndexedDbOfflineStore(name);
    const restored = await reopened.getDeviceIdentity();
    expect(restored?.privateKey).toBeInstanceOf(CryptoKey);
    expect(restored?.privateKey.extractable).toBe(false);
    expect(restored?.publicKeySpki).toBe(identity.publicKeySpki);
    reopened.close();
  });

  it("migra banco v1 para v2 preservando fila, fotos e metadados", async () => {
    const name = `transjap-v1-${crypto.randomUUID()}`;
    databases.push(name);
    await new Promise<void>((resolve, reject) => {
      const request = indexedDB.open(name, 1);
      request.onupgradeneeded = () => {
        const db = request.result;
        const events = db.createObjectStore("events", { keyPath: "localEventId" });
        events.createIndex("clientEventId", "clientEventId", { unique: true });
        events.createIndex("userId", "userId");
        events.createIndex("status", "status");
        events.createIndex("createdAtLocal", "createdAtLocal");
        db.createObjectStore("photos", { keyPath: "blobKey" });
        db.createObjectStore("machines", { keyPath: "id" });
        db.createObjectStore("worksites", { keyPath: "id" });
        db.createObjectStore("metadata", { keyPath: "key" });
      };
      request.onsuccess = () => {
        const db = request.result;
        const tx = db.transaction(["events", "photos", "machines", "worksites", "metadata"], "readwrite");
        tx.objectStore("events").put({ localEventId: "event-1", clientEventId: "client-1", userId: "user-1", status: "PENDING", createdAtLocal: "2026-10-22T12:00:00Z" });
        tx.objectStore("photos").put({ blobKey: "photo-1", blob: new Blob(["photo"]) });
        tx.objectStore("machines").put({ id: "machine-1", fleetNumber: 1 });
        tx.objectStore("worksites").put({ id: "site-1", name: "Obra" });
        tx.objectStore("metadata").put({ key: "device-id", value: "web-existing" });
        tx.oncomplete = () => { db.close(); resolve(); };
        tx.onerror = () => reject(tx.error);
      };
      request.onerror = () => reject(request.error);
    });

    const upgraded = new IndexedDbOfflineStore(name);
    expect((await upgraded.getEventsForUser("user-1"))[0]?.localEventId).toBe("event-1");
    expect((await upgraded.getPhotoBlob("photo-1"))?.blob).toBeInstanceOf(Blob);
    expect(await upgraded.getOrCreateDeviceId()).toBe("web-existing");
    expect(await upgraded.getDeviceIdentity()).toBeNull();
    upgraded.close();

    const db = await new Promise<IDBDatabase>((resolve, reject) => {
      const request = indexedDB.open(name);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    expect(db.version).toBe(OFFLINE_DB_VERSION);
    expect(db.objectStoreNames.contains("device_identity")).toBe(true);
    expect(db.objectStoreNames.contains("events")).toBe(true);
    expect(db.objectStoreNames.contains("photos")).toBe(true);
    db.close();
  });

  it("cleanup preserva rigorosamente eventos e blobs em PENDING, SYNCING, FAILED_RETRYABLE e FAILED_PERMANENT", async () => {
    const { store } = createStore();
    const queued1 = await store.enqueueReadingWithPhoto(await readingInput());
    const queued2 = await store.enqueueReadingWithPhoto(await readingInput());
    const queued3 = await store.enqueueReadingWithPhoto(await readingInput());

    await store.markSyncing(queued1.readingEvent.localEventId, "2026-10-19T11:00:00Z");
    await store.markRetryableFailure(queued2.readingEvent.localEventId, "erro rede", "2026-10-19T11:05:00Z");
    await store.markPermanentFailure(queued3.readingEvent.localEventId, "erro 422");

    // Executa cleanup com retenção zero
    await store.removeSyncedDataWhenSafe({
      blobRetentionMs: 0,
      eventRetentionMs: 0,
      now: new Date("2026-10-19T12:00:00Z"),
    });

    const userEvents = await store.getEventsForUser("user-1");
    // Todos os 6 eventos (3 leituras + 3 fotos) devem continuar intactos
    expect(userEvents).toHaveLength(6);
    expect(await store.getPhotoBlob(queued1.photoEvent.payload.blobKey)).toBeDefined();
    expect(await store.getPhotoBlob(queued2.photoEvent.payload.blobKey)).toBeDefined();
    expect(await store.getPhotoBlob(queued3.photoEvent.payload.blobKey)).toBeDefined();
    store.close();
  });

  it("upgrade de schema do IndexedDB preserva dados existentes sem perda ou wipe", async () => {
    const { name, store } = createStore();
    const queued = await store.enqueueReadingWithPhoto(await readingInput());
    store.close();

    // Simula abertura em versão 2 adicionando novo store sem apagar existentes
    const upgradeSuccess = await new Promise<boolean>((resolve, reject) => {
      const upgradeRequest = indexedDB.open(name, OFFLINE_DB_VERSION + 1);
      upgradeRequest.onupgradeneeded = () => {
        const db = upgradeRequest.result;
        expect(db.objectStoreNames.contains("events")).toBe(true);
        expect(db.objectStoreNames.contains("photos")).toBe(true);
        if (!db.objectStoreNames.contains("audit_v2")) {
          db.createObjectStore("audit_v2", { keyPath: "id" });
        }
      };
      upgradeRequest.onsuccess = () => {
        const db = upgradeRequest.result;
        expect(db.version).toBe(OFFLINE_DB_VERSION + 1);
        db.close();
        resolve(true);
      };
      upgradeRequest.onerror = () => reject(upgradeRequest.error);
    });

    expect(upgradeSuccess).toBe(true);

    // Reabre e verifica que os dados gravados na v1 continuam íntegros
    const reopenedDb = await new Promise<IDBDatabase>((resolve, reject) => {
      const req = indexedDB.open(name, OFFLINE_DB_VERSION + 1);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });

    const tx = reopenedDb.transaction(["events", "photos"], "readonly");
    const event = await new Promise((resolve) => {
      const getReq = tx.objectStore("events").get(queued.readingEvent.localEventId);
      getReq.onsuccess = () => resolve(getReq.result);
    });
    const photo = await new Promise((resolve) => {
      const getReq = tx.objectStore("photos").get(queued.photoEvent.payload.blobKey);
      getReq.onsuccess = () => resolve(getReq.result);
    });

    expect(event).toBeDefined();
    expect(photo).toBeDefined();
    reopenedDb.close();
  });
});
