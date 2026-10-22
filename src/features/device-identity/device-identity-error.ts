export class DeviceIdentityError extends Error {
  readonly permanent = true;

  constructor(message: string) {
    super(message);
    this.name = "DeviceIdentityError";
  }
}

export function isPermanentDeviceIdentityError(error: unknown): error is DeviceIdentityError {
  return error instanceof DeviceIdentityError;
}
