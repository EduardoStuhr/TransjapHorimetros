import { afterEach, describe, expect, it } from "vitest";

import { sha256Hex } from "@/offline/crypto";
import { IndexedDbOfflineStore } from "@/offline/indexed-db-store";
import { OfflineSyncService } from "@/offline/sync-engine";
import { SyncTransportError, type SyncResponse, type SyncTransport } from "@/offline/sync-transport";
import type { EnqueueReadingInput, OfflinePhotoEvent, OfflineReadingEvent, StoredPhotoBlob } from "@/offline/types";

const databases: Array<{ name: string; store: IndexedDbOfflineStore }> = [];

function createStore() {
  const name = `transjap-sync-test-${crypto.randomUUID()}`;
  const store = new IndexedDbOfflineStore(name);
  databases.push({ name, store });
  return store;
}

async function input(userId = "user-1"): Promise<EnqueueReadingInput> {
  const photo = new Blob([new Uint8Array([0xff, 0xd8, 0xff, 0xd9])], { type: "image/jpeg" });
  return {
    reading: {
      machineId: crypto.randomUUID(),
      workSiteId: crypto.randomUUID(),
      value: 100,
      readingType: "INTERMEDIATE",
      capturedAtDevice: "2026-10-19T12:00:00Z",
      deviceId: "web-test",
    },
    photo,
    photoFileName: "meter.jpg",
    photoMimeType: "image/jpeg",
    photoSha256: await sha256Hex(photo),
    user: { id: userId, email: `${userId}@example.com` },
  };
}

class FakeTransport implements SyncTransport {
  calls: string[] = [];
  readingIds: string[] = [];
  reading: (event: OfflineReadingEvent) => Promise<SyncResponse> = async () => ({
    status: 201,
    resourceId: crypto.randomUUID(),
    body: null,
  });
  photo: (event: OfflinePhotoEvent, readingId: string, photo: StoredPhotoBlob) => Promise<SyncResponse> = async () => ({
    status: 201,
    resourceId: crypto.randomUUID(),
    body: null,
  });

  async createReading(event: OfflineReadingEvent) {
    this.calls.push(`reading:${event.clientEventId}`);
    return this.reading(event);
  }

  async uploadPhoto(event: OfflinePhotoEvent, readingId: string, photo: StoredPhotoBlob) {
    this.calls.push(`photo:${event.clientEventId}`);
    this.readingIds.push(readingId);
    return this.photo(event, readingId, photo);
  }
}

afterEach(async () => {
  for (const database of databases.splice(0)) {
    database.store.close();
    await new Promise<void>((resolve) => {
      const request = indexedDB.deleteDatabase(database.name);
      request.onsuccess = () => resolve();
      request.onerror = () => resolve();
      request.onblocked = () => resolve();
    });
  }
});

describe("OfflineSyncService", () => {
  it("sincroniza leitura antes da foto e persiste o ID retornado pelo servidor", async () => {
    const store = createStore();
    await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    const readingId = crypto.randomUUID();
    transport.reading = async () => ({ status: 201, resourceId: readingId, body: { id: readingId } });
    const engine = new OfflineSyncService(store, transport);

    const result = await engine.sync("user-1");
    const events = await store.getEventsForUser("user-1");

    expect(result.synced).toBe(2);
    expect(transport.calls[0]).toMatch(/^reading:/);
    expect(transport.calls[1]).toMatch(/^photo:/);
    expect(transport.readingIds).toEqual([readingId]);
    expect(events.every((event) => event.status === "SYNCED")).toBe(true);
    expect(events.every((event) => event.serverReadingId === readingId)).toBe(true);
  });

  it.each([200, 201])("aceita HTTP %i como sucesso idempotente", async (status) => {
    const store = createStore();
    await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    transport.reading = async () => ({ status, resourceId: "reading-1", body: { id: "reading-1" } });
    transport.photo = async () => ({ status, resourceId: "photo-1", body: { id: "photo-1" } });
    const result = await new OfflineSyncService(store, transport).sync("user-1");
    expect(result.synced).toBe(2);
  });

  it.each([400, 403, 404, 422])("classifica HTTP %i como permanente sem apagar o evento", async (status) => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    transport.reading = async () => { throw new SyncTransportError(`erro ${status}`, status); };
    const result = await new OfflineSyncService(store, transport).sync("user-1");
    expect(result.permanentFailures).toBe(1);
    expect((await store.getEvent(queued.readingEvent.localEventId))?.status).toBe("FAILED_PERMANENT");
    expect(await store.getPhotoBlob(queued.photoEvent.payload.blobKey)).toBeDefined();
  });

  it.each([408, 429, 500, 502, 503, 504, null])("classifica %s como retryable e preserva ClientEventId no retry", async (status) => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    let attempts = 0;
    transport.reading = async () => {
      attempts += 1;
      if (attempts === 1) throw new SyncTransportError("temporário", status);
      return { status: 200, resourceId: "reading-1", body: { id: "reading-1" } };
    };
    const engine = new OfflineSyncService(store, transport);
    await engine.sync("user-1");
    expect((await store.getEvent(queued.readingEvent.localEventId))?.status).toBe("FAILED_RETRYABLE");
    await engine.sync("user-1", true);
    const readingCalls = transport.calls.filter((call) => call.startsWith("reading:"));
    expect(readingCalls).toEqual([
      `reading:${queued.readingEvent.clientEventId}`,
      `reading:${queued.readingEvent.clientEventId}`,
    ]);
  });

  it("pausa em 401, preserva fila e não envia eventos de outro usuário", async () => {
    const store = createStore();
    const own = await store.enqueueReadingWithPhoto(await input("user-1"));
    await store.enqueueReadingWithPhoto(await input("user-2"));
    const transport = new FakeTransport();
    transport.reading = async () => { throw new SyncTransportError("expired", 401); };
    const result = await new OfflineSyncService(store, transport).sync("user-1");
    const snapshot = await store.getQueueSnapshot("user-1");
    expect(result.authenticationRequired).toBe(true);
    expect(result.processed).toBe(1);
    expect(snapshot.otherUserPending).toBe(2);
    expect(await store.getEvent(own.readingEvent.localEventId)).toBeDefined();
  });

  it("persiste backoff crescente a partir da primeira falha", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    transport.reading = async () => { throw new SyncTransportError("temporário", 500); };
    const now = new Date("2026-10-19T12:00:00Z");
    await new OfflineSyncService(store, transport, { now: () => now }).sync("user-1");
    const failed = await store.getEvent(queued.readingEvent.localEventId);
    expect(failed?.retryCount).toBe(1);
    expect(failed?.nextAttemptAt).toBe("2026-10-19T12:00:05.000Z");
  });

  it("continua eventos independentes quando uma leitura falha permanentemente", async () => {
    const store = createStore();
    const first = await store.enqueueReadingWithPhoto(await input());
    const second = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    transport.reading = async (event) => {
      if (event.clientEventId === first.readingEvent.clientEventId) {
        throw new SyncTransportError("inválida", 422);
      }
      return { status: 201, resourceId: "reading-b", body: { id: "reading-b" } };
    };
    const result = await new OfflineSyncService(store, transport).sync("user-1");
    expect(result.permanentFailures).toBe(1);
    expect((await store.getEvent(first.readingEvent.localEventId))?.status).toBe("FAILED_PERMANENT");
    expect((await store.getEvent(first.photoEvent.localEventId))?.status).toBe("PENDING");
    expect((await store.getEvent(second.readingEvent.localEventId))?.status).toBe("SYNCED");
    expect((await store.getEvent(second.photoEvent.localEventId))?.status).toBe("SYNCED");
  });

  it("trata 409 com recurso como sucesso e conflito real como permanente", async () => {
    const store = createStore();
    const first = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    transport.reading = async (event) => {
      if (event.clientEventId === first.readingEvent.clientEventId) {
        throw new SyncTransportError("já existe", 409, "reading-existing");
      }
      throw new SyncTransportError("conflito real", 409);
    };
    const result = await new OfflineSyncService(store, transport).sync("user-1");
    expect(result.synced).toBeGreaterThanOrEqual(1);
    expect((await store.getEvent(first.readingEvent.localEventId))?.serverReadingId).toBe("reading-existing");

    const second = await store.enqueueReadingWithPhoto(await input());
    const result2 = await new OfflineSyncService(store, transport).sync("user-1", true);
    expect(result2.permanentFailures).toBeGreaterThanOrEqual(1);
    expect((await store.getEvent(second.readingEvent.localEventId))?.status).toBe("FAILED_PERMANENT");
  });
});
