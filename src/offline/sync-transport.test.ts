import { describe, expect, it, vi } from "vitest";

import { BrowserSyncTransport, SyncTransportError } from "@/offline/sync-transport";
import type { OfflinePhotoEvent, OfflineReadingEvent, StoredPhotoBlob } from "@/offline/types";

describe("BrowserSyncTransport", () => {
  const readingEvent: OfflineReadingEvent = {
    localEventId: "loc-read-1",
    clientEventId: "client-event-123",
    eventType: "READING_CREATED",
    createdAtLocal: "2026-10-19T10:00:00.000Z",
    capturedAtDevice: "2026-10-19T09:55:00.000Z",
    status: "PENDING",
    retryCount: 0,
    lastAttemptAt: null,
    firstSyncAttemptAtLocal: null,
    nextAttemptAt: null,
    lastError: null,
    syncedAt: null,
    serverReadingId: null,
    parentReadingClientEventId: null,
    userId: "user-1",
    userEmail: "user@example.com",
    schemaVersion: 1,
    payload: {
      machineId: "m-1",
      workSiteId: "w-1",
      value: 250.5,
      readingType: "INTERMEDIATE",
      capturedAtDevice: "2026-10-19T09:55:00.000Z",
      deviceId: "dev-abc",
      deviceIdentity: {
        localDeviceId: "dev-abc",
        registeredDeviceId: "registered-1",
        status: "AUTHORIZED",
      },
    },
  };

  const photoEvent: OfflinePhotoEvent = {
    localEventId: "loc-photo-1",
    clientEventId: "client-photo-456",
    eventType: "PHOTO_UPLOADED",
    createdAtLocal: "2026-10-19T10:00:00.000Z",
    capturedAtDevice: "2026-10-19T09:55:00.000Z",
    status: "PENDING",
    retryCount: 0,
    lastAttemptAt: null,
    firstSyncAttemptAtLocal: null,
    nextAttemptAt: null,
    lastError: null,
    syncedAt: null,
    serverReadingId: null,
    parentReadingClientEventId: "client-event-123",
    userId: "user-1",
    userEmail: "user@example.com",
    schemaVersion: 1,
    payload: {
      parentReadingClientEventId: "client-event-123",
      blobKey: "photo:loc-photo-1",
      fileName: "horimetro.jpg",
      mimeType: "image/jpeg",
      size: 4,
      sha256: "aabbcc",
      capturedAtDevice: "2026-10-19T09:55:00.000Z",
      deviceId: "dev-abc",
    },
  };

  const storedBlob: StoredPhotoBlob = {
    blobKey: "photo:loc-photo-1",
    blob: new Blob([new Uint8Array([1, 2, 3, 4])], { type: "image/jpeg" }),
    createdAtLocal: "2026-10-19T10:00:00.000Z",
    fileName: "horimetro.jpg",
    mimeType: "image/jpeg",
    sha256: "aabbcc",
    size: 4,
  };

  it("POST /api/readings com sucesso 201 retorna resourceId e payload", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ id: "server-reading-999" }), {
        status: 201,
        headers: { "Content-Type": "application/json" },
      }),
    );
    vi.stubGlobal("fetch", fetchMock);

    const transport = new BrowserSyncTransport(async () => "device-session-token");
    const result = await transport.createReading(readingEvent);

    expect(result.status).toBe(201);
    expect(result.resourceId).toBe("server-reading-999");
    expect(fetchMock).toHaveBeenCalledWith("/api/readings", expect.objectContaining({
      method: "POST",
      body: JSON.stringify({
        machineId: "m-1",
        workSiteId: "w-1",
        value: 250.5,
        readingType: "INTERMEDIATE",
        capturedAtDevice: "2026-10-19T09:55:00.000Z",
        deviceId: "dev-abc",
        clientEventId: "client-event-123",
      }),
      headers: expect.objectContaining({ "X-Device-Session": "device-session-token" }),
    }));
  });

  it("nao envia a identidade registrada declarada pela fila sem prova", async () => {
    vi.stubGlobal("fetch", vi.fn());
    const transport = new BrowserSyncTransport(async () => { throw new Error("Dispositivo revogado"); });
    await expect(transport.createReading(readingEvent)).rejects.toMatchObject({
      status: 403,
      message: "Dispositivo revogado",
    });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("POST /api/readings/:id/photo envia FormData com campos de rastreabilidade e X-Device-Session", async () => {
    let capturedBody: FormData | null = null;
    let capturedHeaders: HeadersInit | undefined;
    const fetchMock = vi.fn().mockImplementation(async (_url: string, init: RequestInit) => {
      capturedBody = init.body as FormData;
      capturedHeaders = init.headers;
      return new Response(JSON.stringify({ readingId: "server-reading-999" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    });
    vi.stubGlobal("fetch", fetchMock);

    const transport = new BrowserSyncTransport(async () => "photo-session-token");
    const result = await transport.uploadPhoto(photoEvent, "server-reading-999", storedBlob);

    expect(result.status).toBe(200);
    expect(result.resourceId).toBe("server-reading-999");
    expect(capturedBody).toBeDefined();
    const formData = capturedBody as unknown as FormData;
    expect(formData.get("capturedAtDevice")).toBe("2026-10-19T09:55:00.000Z");
    expect(formData.get("deviceId")).toBe("dev-abc");
    expect(formData.get("clientEventId")).toBe("client-photo-456");
    expect(formData.get("file")).toBeDefined();
    expect(capturedHeaders).toEqual(expect.objectContaining({
      "X-Device-Session": "photo-session-token",
    }));
  });

  it("uploadPhoto falha com 403 se dispositivo foi revogado sem enviar requisicao", async () => {
    vi.stubGlobal("fetch", vi.fn());
    const transport = new BrowserSyncTransport(async () => { throw new Error("Dispositivo revogado"); });
    await expect(transport.uploadPhoto(photoEvent, "server-reading-999", storedBlob)).rejects.toMatchObject({
      status: 403,
      message: "Dispositivo revogado",
    });
    expect(fetch).not.toHaveBeenCalled();
  });

  it("sanitiza mensagens sensíveis em caso de falha de transporte", async () => {
    const fetchMock = vi.fn().mockImplementation(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            detail: "Falha na conexão connection string: Server=prod;Password=secret123 com Bearer eyJhbGciOi...",
          }),
          { status: 500, headers: { "Content-Type": "application/json" } },
        ),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const transport = new BrowserSyncTransport();
    await expect(transport.createReading(readingEvent)).rejects.toThrow(
      /dado sensível removido/,
    );
    await expect(transport.createReading(readingEvent)).rejects.toThrow(
      /credencial removida/,
    );
  });

  it("trata erro de rede sem HTTP status gerando SyncTransportError(status=null)", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError("Failed to fetch"));
    vi.stubGlobal("fetch", fetchMock);

    const transport = new BrowserSyncTransport();
    await expect(transport.createReading(readingEvent)).rejects.toThrow(SyncTransportError);
    await expect(transport.createReading(readingEvent)).rejects.toMatchObject({
      status: null,
      message: "Sem comunicação com o servidor. A tentativa será repetida.",
    });
  });
});
