import type { OfflinePhotoEvent, OfflineReadingEvent, StoredPhotoBlob } from "@/offline/types";
import { isPermanentDeviceIdentityError } from "@/features/device-identity/device-identity-error";

export interface SyncResponse {
  status: number;
  resourceId: string | null;
  body: Record<string, unknown> | null;
}

export interface SyncTransport {
  createReading(event: OfflineReadingEvent): Promise<SyncResponse>;
  uploadPhoto(event: OfflinePhotoEvent, readingId: string, photo: StoredPhotoBlob): Promise<SyncResponse>;
}

export class SyncTransportError extends Error {
  constructor(
    message: string,
    public readonly status: number | null,
    public readonly resourceId: string | null = null,
  ) {
    super(message);
    this.name = "SyncTransportError";
  }
}

function safeMessage(body: Record<string, unknown> | null, status: number) {
  const errors = body?.errors && typeof body.errors === "object"
    ? Object.values(body.errors as Record<string, unknown>)
      .flatMap((value) => Array.isArray(value) ? value : [])
      .filter((value): value is string => typeof value === "string")
    : [];
  const candidate = errors.join(" ")
    || (typeof body?.detail === "string" ? body.detail : "")
    || (typeof body?.title === "string" ? body.title : "")
    || `A API recusou o evento (HTTP ${status}).`;
  return candidate
    .replace(/[\r\n\t]+/g, " ")
    .replace(/Bearer\s+\S+/gi, "[credencial removida]")
    .replace(/(?:password|senha|connection\s*string)\s*[:=]\s*\S+/gi, "[dado sensível removido]")
    .slice(0, 300);
}

async function parseResponse(response: Response): Promise<SyncResponse> {
  const text = await response.text();
  let body: Record<string, unknown> | null = null;
  try {
    body = text ? JSON.parse(text) as Record<string, unknown> : null;
  } catch {
    body = null;
  }
  const resourceId = typeof body?.id === "string"
    ? body.id
    : typeof body?.readingId === "string"
      ? body.readingId
      : null;
  if (!response.ok) {
    throw new SyncTransportError(safeMessage(body, response.status), response.status, resourceId);
  }
  return { status: response.status, resourceId, body };
}

async function requestWithTimeout(input: RequestInfo | URL, init: RequestInit) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 30_000);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } catch (error) {
    const message = error instanceof DOMException && error.name === "AbortError"
      ? "A conexão demorou demais. A tentativa será repetida."
      : "Sem comunicação com o servidor. A tentativa será repetida.";
    throw new SyncTransportError(message, null);
  } finally {
    clearTimeout(timeout);
  }
}

function deviceSessionFailure(error: unknown) {
  const message = error instanceof Error ? error.message : "Nao foi possivel comprovar o dispositivo.";
  if (isPermanentDeviceIdentityError(error) || /revogad/i.test(message)) {
    return new SyncTransportError(message, 403);
  }
  const status = typeof error === "object" && error !== null && "status" in error
    && typeof error.status === "number"
    ? error.status
    : null;
  if (status === 401 || status === 403 || status === 404 || status === 422 || (status !== null && status >= 500)) {
    return new SyncTransportError(message, status);
  }
  return new SyncTransportError(message, 425);
}

export class BrowserSyncTransport implements SyncTransport {
  constructor(private readonly getDeviceSession: () => Promise<string> = async () => "") {}

  async createReading(event: OfflineReadingEvent) {
    let deviceSession: string;
    try {
      deviceSession = await this.getDeviceSession();
    } catch (caught) {
      throw deviceSessionFailure(caught);
    }
    const payload = { ...event.payload };
    delete payload.deviceIdentity;
    const response = await requestWithTimeout("/api/readings", {
      method: "POST",
      cache: "no-store",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-Device-Session": deviceSession,
      },
      body: JSON.stringify({
        ...payload,
        clientEventId: event.clientEventId,
      }),
    });
    return parseResponse(response);
  }

  async uploadPhoto(event: OfflinePhotoEvent, readingId: string, photo: StoredPhotoBlob) {
    let deviceSession: string;
    try {
      deviceSession = await this.getDeviceSession();
    } catch (caught) {
      throw deviceSessionFailure(caught);
    }
    const form = new FormData();
    form.set("file", photo.blob, photo.fileName);
    form.set("capturedAtDevice", event.payload.capturedAtDevice);
    form.set("deviceId", event.payload.deviceId);
    form.set("clientEventId", event.clientEventId);
    const response = await requestWithTimeout(
      `/api/readings/${encodeURIComponent(readingId)}/photo`,
      {
        method: "POST",
        cache: "no-store",
        body: form,
        headers: {
          Accept: "application/json",
          "X-Device-Session": deviceSession,
        },
      },
    );
    return parseResponse(response);
  }
}
