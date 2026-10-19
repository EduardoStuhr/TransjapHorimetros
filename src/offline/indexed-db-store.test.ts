import { afterEach, describe, expect, it } from "vitest";

import { IndexedDbOfflineStore, OFFLINE_DB_VERSION } from "@/offline/indexed-db-store";
import { sha256Hex } from "@/offline/crypto";
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
});
