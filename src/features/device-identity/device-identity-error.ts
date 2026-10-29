import type { StoredDeviceIdentity } from "@/offline/types";

export class DeviceIdentityError extends Error {
  constructor(message: string, readonly permanent = true) {
    super(message);
    this.name = "DeviceIdentityError";
  }
}

export function isPermanentDeviceIdentityError(error: unknown): error is DeviceIdentityError {
  return error instanceof DeviceIdentityError && error.permanent;
}

export function deviceAuthorizationError(status?: StoredDeviceIdentity["status"]) {
  return new DeviceIdentityError(status === "REVOKED"
    ? "Este dispositivo foi revogado. A fila local permanece preservada para revisao."
    : status === "IDENTITY_INVALID"
      ? "A identidade local e invalida. A fila foi preservada; solicite um novo cadastro."
      : "Este dispositivo ainda nao foi autorizado.",
  status === "REVOKED" || status === "IDENTITY_INVALID");
}
