import type { RegisteredDeviceStatus } from "@/offline/types";

export interface RegisteredDeviceResponse {
  id: string;
  localDeviceId: string;
  displayName: string;
  platform: string;
  status: RegisteredDeviceStatus;
  keyAlgorithm: string;
  publicKeyFingerprint: string;
  requestedByUserId: string;
  requestedByEmail: string;
  requestedAt: string;
  authorizedByUserId: string | null;
  authorizedByEmail: string | null;
  authorizedAt: string | null;
  revokedByUserId: string | null;
  revokedByEmail: string | null;
  revokedAt: string | null;
  lastSeenAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CurrentDeviceStatusResponse {
  id: string;
  localDeviceId: string;
  displayName: string;
  platform: string;
  status: RegisteredDeviceStatus;
  keyAlgorithm: string;
  publicKeyFingerprint: string;
  lastSeenAt: string | null;
  requestedAt: string;
}

export interface DeviceChallengeResponse {
  challengeId: string;
  payload: string;
  expiresAt: string;
}

export interface DeviceSessionResponse {
  deviceSessionToken: string;
  expiresAt: string;
  device: RegisteredDeviceResponse;
}

export interface PagedDevicesResponse {
  items: RegisteredDeviceResponse[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface RegisteredDeviceAuditResponse {
  action: string;
  createdAt: string;
  userId: string | null;
  userEmail: string | null;
}
