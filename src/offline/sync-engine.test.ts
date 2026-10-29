import { afterEach, describe, expect, it } from "vitest";

import { deviceAuthorizationError } from "@/features/device-identity/device-identity-error";
import { sha256Hex } from "@/offline/crypto";
import { IndexedDbOfflineStore } from "@/offline/indexed-db-store";
import { OfflineSyncService } from "@/offline/sync-engine";
import { BrowserSyncTransport, SyncTransportError, type SyncResponse, type SyncTransport } from "@/offline/sync-transport";
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
  it.each(["PENDING", "UNREGISTERED", undefined] as const)(
    "preserva fila e foto para nova tentativa quando o dispositivo esta %s e retoma apos autorizacao",
    async (status) => {
      const store = createStore();
      const queued = await store.enqueueReadingWithPhoto(await input());
      const transport = new BrowserSyncTransport(async () => { throw deviceAuthorizationError(status); });
      const result = await new OfflineSyncService(store, transport).sync("user-1");
      expect(result.retryableFailures).toBe(1);
      expect(result.permanentFailures).toBe(0);
      expect((await store.getEvent(queued.readingEvent.localEventId))?.status).toBe("FAILED_RETRYABLE");
      expect((await store.getEvent(queued.photoEvent.localEventId))?.status).toBe("PENDING");
      expect(await store.getPhotoBlob(queued.photoEvent.payload.blobKey)).toBeDefined();
      const approved = await new OfflineSyncService(store, new FakeTransport()).sync("user-1", true);
      expect(approved.synced).toBe(2);
      expect((await store.getEvent(queued.readingEvent.localEventId))?.clientEventId)
        .toBe(queued.readingEvent.clientEventId);
    },
  );

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

  it("valida o ciclo completo de 16 etapas offline: captura, persistência, reload, sync sequencial e retenção", async () => {
    // 1. Usuário autenticado
    const userId = "operador-campo-1";
    const store = createStore();

    // 2. Dispositivo possui cache de máquinas e obras
    const machineId = crypto.randomUUID();
    const workSiteId = crypto.randomUUID();
    await store.replaceReferenceData({
      schemaVersion: 1,
      generatedAt: "2026-10-19T08:00:00Z",
      machines: [{
        id: machineId,
        fleetNumber: 105,
        model: "Retroescavadeira",
        meterUnit: "HOURS",
        status: "ACTIVE",
        activeQrToken: "QR-105",
        updatedAt: "2026-10-19T08:00:00Z",
      }],
      workSites: [{ id: workSiteId, name: "Obra Transjap", active: true, updatedAt: "2026-10-19T08:00:00Z" }],
    });

    // 3. Internet cai / 4. Operador cria leitura offline / 5. Foto salva no IndexedDB
    const photoBlob = new Blob([new Uint8Array([10, 20, 30, 40])], { type: "image/jpeg" });
    const enqueued = await store.enqueueReadingWithPhoto({
      reading: {
        machineId,
        workSiteId,
        value: 1250.5,
        readingType: "INTERMEDIATE",
        capturedAtDevice: "2026-10-19T09:00:00Z",
        deviceId: "dev-field-1",
      },
      photo: photoBlob,
      photoFileName: "horimetro-105.jpg",
      photoMimeType: "image/jpeg",
      photoSha256: await sha256Hex(photoBlob),
      user: { id: userId, email: "operador@transjap.com.br" },
    });

    // 6. Leitura e foto ficam PENDING
    expect(enqueued.readingEvent.status).toBe("PENDING");
    expect(enqueued.photoEvent.status).toBe("PENDING");
    const readingClientId = enqueued.readingEvent.clientEventId;
    const photoClientId = enqueued.photoEvent.clientEventId;

    // 7. Browser/app é recarregado (fechar e reabrir store no mesmo banco)
    store.close();
    const reopenedStore = new IndexedDbOfflineStore(databases[databases.length - 1].name);

    // 8. Fila continua existindo íntegra
    const reloadedEvents = await reopenedStore.getEventsForUser(userId);
    expect(reloadedEvents).toHaveLength(2);
    expect(reloadedEvents.find((e) => e.eventType === "READING_CREATED")?.clientEventId).toBe(readingClientId);
    expect(reloadedEvents.find((e) => e.eventType === "PHOTO_UPLOADED")?.clientEventId).toBe(photoClientId);

    // 9. Internet volta e inicia sync
    const transport = new FakeTransport();
    const serverReadingId = "reading-srv-789";
    transport.reading = async (event) => {
      // 10. Leitura sincroniza e 11. Servidor devolve ReadingId
      expect(event.clientEventId).toBe(readingClientId);
      return { status: 201, resourceId: serverReadingId, body: { id: serverReadingId } };
    };
    transport.photo = async (event, readingId) => {
      // 12. Foto usa esse ReadingId e 13. Upload é confirmado
      expect(event.clientEventId).toBe(photoClientId);
      expect(readingId).toBe(serverReadingId);
      return { status: 201, resourceId: "photo-srv-456", body: { id: "photo-srv-456" } };
    };

    const engine = new OfflineSyncService(reopenedStore, transport);
    const result = await engine.sync(userId);

    // 14. OCR é elegível no servidor (não chamado localmente)
    // 15. Fila termina 100% sincronizada
    expect(result.synced).toBe(2);
    expect(result.processed).toBe(2);
    const syncedEvents = await reopenedStore.getEventsForUser(userId);
    expect(syncedEvents.every((e) => e.status === "SYNCED")).toBe(true);

    // 16. Blob local é removido apenas quando seguro (após retention)
    await reopenedStore.removeSyncedDataWhenSafe({
      blobRetentionMs: 0,
      eventRetentionMs: 7 * 24 * 60 * 60_000,
      now: new Date(),
    });
    expect(await reopenedStore.getPhotoBlob(enqueued.photoEvent.payload.blobKey)).toBeUndefined();
    expect(await reopenedStore.getEventsForUser(userId)).toHaveLength(2);
    reopenedStore.close();
  });

  it("prova explicitamente que retries NÃO geram novo ClientEventId e reutilizam o mesmo identificador", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    const observedClientEventIds: string[] = [];

    transport.reading = async (event) => {
      observedClientEventIds.push(event.clientEventId);
      if (observedClientEventIds.length < 3) {
        throw new SyncTransportError("instabilidade temporária", 503);
      }
      return { status: 200, resourceId: "reading-res-ok", body: { id: "reading-res-ok" } };
    };

    const engine = new OfflineSyncService(store, transport);

    // 1ª tentativa -> 503
    await engine.sync("user-1");
    // 2ª tentativa -> 503
    await engine.sync("user-1", true);
    // 3ª tentativa -> 200 sucesso
    await engine.sync("user-1", true);

    expect(observedClientEventIds).toHaveLength(3);
    // Todos os 3 envios usaram RIGOROSAMENTE o mesmo ClientEventId gerado na criação
    expect(observedClientEventIds.every((id) => id === queued.readingEvent.clientEventId)).toBe(true);
    expect((await store.getEvent(queued.readingEvent.localEventId))?.status).toBe("SYNCED");
  });

  it("valida idempotência de foto: replay, timeout simulado e conflito com evidência existente", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    let photoAttempts = 0;
    const photoClientIds: string[] = [];

    transport.reading = async () => ({ status: 200, resourceId: "reading-fixed-id", body: null });
    transport.photo = async (event) => {
      photoAttempts += 1;
      photoClientIds.push(event.clientEventId);
      if (photoAttempts === 1) {
        // Simula timeout no upload da foto
        throw new SyncTransportError("timeout de upload", null);
      }
      // Replay da foto responde com 200 idempotente
      return { status: 200, resourceId: "photo-fixed-id", body: null };
    };

    const engine = new OfflineSyncService(store, transport);
    // Tentativa 1: leitura sincroniza, foto sofre timeout
    const res1 = await engine.sync("user-1");
    expect(res1.synced).toBe(1);
    expect(res1.retryableFailures).toBe(1);

    // Tentativa 2: foto retenta usando o MESMO clientEventId e mesmo hash
    const res2 = await engine.sync("user-1", true);
    expect(res2.synced).toBe(1);
    expect(photoClientIds).toEqual([
      queued.photoEvent.clientEventId,
      queued.photoEvent.clientEventId,
    ]);
    expect((await store.getEvent(queued.photoEvent.localEventId))?.status).toBe("SYNCED");
  });

  it("401 pausa sincronização, preserva fila e blob, e retoma com mesmo ClientEventId após reautenticação", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    let authenticated = false;
    let authRequiredFired = false;

    transport.reading = async () => {
      if (!authenticated) {
        throw new SyncTransportError("Sessão expirada.", 401);
      }
      return { status: 201, resourceId: "reading-reauth-id", body: { id: "reading-reauth-id" } };
    };

    const engine = new OfflineSyncService(store, transport, {
      onAuthenticationRequired: () => {
        authRequiredFired = true;
      },
    });

    // Sync com sessão expirada
    const run1 = await engine.sync("user-1");
    expect(run1.authenticationRequired).toBe(true);
    expect(authRequiredFired).toBe(true);

    const eventDuring401 = await store.getEvent(queued.readingEvent.localEventId);
    expect(eventDuring401?.status).toBe("FAILED_RETRYABLE");
    expect(eventDuring401?.status).not.toBe("FAILED_PERMANENT");
    // Blob preservado
    expect(await store.getPhotoBlob(queued.photoEvent.payload.blobKey)).toBeDefined();

    // Simula reautenticação do usuário
    authenticated = true;
    const run2 = await engine.sync("user-1", true);
    expect(run2.synced).toBe(2);

    const eventAfterAuth = await store.getEvent(queued.readingEvent.localEventId);
    expect(eventAfterAuth?.status).toBe("SYNCED");
    expect(eventAfterAuth?.clientEventId).toBe(queued.readingEvent.clientEventId);
  });

  it("valida sequência exata de backoff: 5s, 30s, 2min, 10min, 30min", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();
    transport.reading = async () => {
      throw new SyncTransportError("falha temporária 500", 500);
    };

    const baseTime = new Date("2026-10-19T12:00:00.000Z");
    let currentTime = baseTime;
    const engine = new OfflineSyncService(store, transport, {
      now: () => currentTime,
    });

    const expectedDelaysMs = [5_000, 30_000, 120_000, 600_000, 1_800_000, 1_800_000];

    for (let attempt = 0; attempt < expectedDelaysMs.length; attempt++) {
      await engine.sync("user-1", true);
      const event = await store.getEvent(queued.readingEvent.localEventId);
      expect(event?.retryCount).toBe(attempt + 1);
      const expectedNext = new Date(currentTime.getTime() + expectedDelaysMs[attempt]).toISOString();
      expect(event?.nextAttemptAt).toBe(expectedNext);
      // Avança o tempo
      currentTime = new Date(currentTime.getTime() + expectedDelaysMs[attempt] + 1_000);
    }
  });

  it("garante isolamento entre abas concorrentes via bloqueio", async () => {
    const store = createStore();
    await store.enqueueReadingWithPhoto(await input());
    const transport = new FakeTransport();

    let releaseLock: () => void = () => {};
    const holdPromise = new Promise<void>((resolve) => {
      releaseLock = resolve;
    });

    transport.reading = async () => {
      await holdPromise;
      return { status: 201, resourceId: "r-locked", body: { id: "r-locked" } };
    };

    const engine1 = new OfflineSyncService(store, transport);
    const engine2 = new OfflineSyncService(store, transport);

    const firstRunPromise = engine1.sync("user-1");
    // Chamada concorrente imediata
    const secondRun = await engine2.sync("user-1");
    expect(secondRun.skippedBecauseLocked).toBe(true);

    releaseLock();
    const firstRun = await firstRunPromise;
    expect(firstRun.synced).toBe(2);
  });

  it("revogado enquanto offline: reading vira FAILED_PERMANENT, photo permanece PENDING/BLOCKED, blob e ClientEventId permanecem íntegros", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input("user-1"));
    const readingClientId = queued.readingEvent.clientEventId;
    const photoClientId = queued.photoEvent.clientEventId;

    const transport = new FakeTransport();
    transport.reading = async () => {
      throw new SyncTransportError("Este dispositivo foi revogado. A fila local permanece preservada para revisao.", 403);
    };

    const engine = new OfflineSyncService(store, transport);
    const result = await engine.sync("user-1");

    expect(result.permanentFailures).toBe(1);
    expect(result.synced).toBe(0);

    const readingEvent = await store.getEvent(queued.readingEvent.localEventId);
    const photoEvent = await store.getEvent(queued.photoEvent.localEventId);
    const photoBlob = await store.getPhotoBlob(queued.photoEvent.payload.blobKey);

    expect(readingEvent?.status).toBe("FAILED_PERMANENT");
    expect(readingEvent?.clientEventId).toBe(readingClientId);

    // Foto dependente nunca foi tentada pois leitura não sincronizou
    expect(photoEvent?.status).toBe("PENDING");
    expect(photoEvent?.clientEventId).toBe(photoClientId);

    // Nenhum dado apagado, blob permanece
    expect(photoBlob).toBeDefined();

    // Cleanup com retenção zero não remove dados com falha ou pendentes
    await store.removeSyncedDataWhenSafe({
      blobRetentionMs: 0,
      eventRetentionMs: 0,
      now: new Date(),
    });

    expect(await store.getPhotoBlob(queued.photoEvent.payload.blobKey)).toBeDefined();
    expect(await store.getEvent(queued.readingEvent.localEventId)).toBeDefined();
    expect(await store.getEvent(queued.photoEvent.localEventId)).toBeDefined();
  });

  it("revogação entre leitura e foto: leitura sincroniza, photo sofre 403 e vira FAILED_PERMANENT sem apagar blob local", async () => {
    const store = createStore();
    const queued = await store.enqueueReadingWithPhoto(await input("user-1"));
    const readingClientId = queued.readingEvent.clientEventId;
    const photoClientId = queued.photoEvent.clientEventId;
    const serverReadingId = "reading-srv-revoked-after";

    const transport = new FakeTransport();
    transport.reading = async () => ({
      status: 201,
      resourceId: serverReadingId,
      body: { id: serverReadingId },
    });
    transport.photo = async () => {
      throw new SyncTransportError("Este dispositivo foi revogado. A fila local foi preservada, mas nao pode ser sincronizada.", 403);
    };

    const engine = new OfflineSyncService(store, transport);
    const result = await engine.sync("user-1");

    expect(result.synced).toBe(1);
    expect(result.permanentFailures).toBe(1);

    const readingEvent = await store.getEvent(queued.readingEvent.localEventId);
    const photoEvent = await store.getEvent(queued.photoEvent.localEventId);
    const photoBlob = await store.getPhotoBlob(queued.photoEvent.payload.blobKey);

    expect(readingEvent?.status).toBe("SYNCED");
    expect(readingEvent?.serverReadingId).toBe(serverReadingId);
    expect(readingEvent?.clientEventId).toBe(readingClientId);

    expect(photoEvent?.status).toBe("FAILED_PERMANENT");
    expect(photoEvent?.clientEventId).toBe(photoClientId);

    // Blob da foto DEVE permanecer no IndexedDB para auditoria / revisão
    expect(photoBlob).toBeDefined();

    await store.removeSyncedDataWhenSafe({
      blobRetentionMs: 0,
      eventRetentionMs: 0,
      now: new Date(),
    });

    // Blob da foto com falha permanente NÃO pode ser apagado
    expect(await store.getPhotoBlob(queued.photoEvent.payload.blobKey)).toBeDefined();
  });
});
