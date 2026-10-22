"use client";

import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

import { generateDeviceIdentity, signDeviceChallenge } from "@/features/device-identity/device-crypto";
import type {
  CurrentDeviceStatusResponse,
  DeviceChallengeResponse,
  DeviceSessionResponse,
  RegisteredDeviceResponse,
} from "@/features/device-identity/types";
import { getOfflineStore } from "@/offline/indexed-db-store";
import type { StoredDeviceIdentity } from "@/offline/types";

class DeviceApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
  }
}

interface DeviceIdentityContextValue {
  identity: StoredDeviceIdentity | null;
  loading: boolean;
  busy: boolean;
  error: string | null;
  register: (displayName: string) => Promise<void>;
  refreshStatus: () => Promise<void>;
  getDeviceSession: () => Promise<string>;
}

const DeviceIdentityContext = createContext<DeviceIdentityContextValue | null>(null);

async function apiRequest<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, { ...init, cache: "no-store", headers: {
    Accept: "application/json",
    ...(init?.body ? { "Content-Type": "application/json" } : {}),
    ...init?.headers,
  } });
  const body = await response.json().catch(() => null) as Record<string, unknown> | null;
  if (!response.ok) {
    const detail = typeof body?.detail === "string" ? body.detail : "Nao foi possivel validar este dispositivo.";
    throw new DeviceApiError(detail, response.status);
  }
  return body as T;
}

function mergeServerIdentity(
  local: StoredDeviceIdentity,
  server: Pick<CurrentDeviceStatusResponse | RegisteredDeviceResponse, "id" | "displayName" | "status" | "publicKeyFingerprint">,
): StoredDeviceIdentity {
  const now = new Date().toISOString();
  return {
    ...local,
    registeredDeviceId: server.id,
    displayName: server.displayName,
    status: server.status,
    publicKeyFingerprint: server.publicKeyFingerprint,
    lastValidatedAt: now,
    updatedAt: now,
  };
}

export function DeviceIdentityProvider({ children }: { children: ReactNode }) {
  const store = useMemo(() => getOfflineStore(), []);
  const [identity, setIdentity] = useState<StoredDeviceIdentity | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const identityRef = useRef<StoredDeviceIdentity | null>(null);
  const sessionRef = useRef<{ token: string; expiresAt: number } | null>(null);
  const sessionPromiseRef = useRef<Promise<string> | null>(null);

  const persist = useCallback(async (next: StoredDeviceIdentity) => {
    await store.saveDeviceIdentity(next);
    identityRef.current = next;
    setIdentity(next);
  }, [store]);

  const refreshStatus = useCallback(async () => {
    const current = identityRef.current;
    if (!current || !navigator.onLine) return;
    try {
      const server = await apiRequest<CurrentDeviceStatusResponse>(
        `/api/devices/status?localDeviceId=${encodeURIComponent(current.localDeviceId)}`,
      );
      await persist(mergeServerIdentity(current, server));
      sessionRef.current = null;
      setError(null);
    } catch (caught) {
      if (caught instanceof DeviceApiError && caught.status === 404) {
        await persist({ ...current, registeredDeviceId: null, status: "UNREGISTERED", lastValidatedAt: new Date().toISOString() });
        sessionRef.current = null;
        return;
      }
      if (caught instanceof DeviceApiError && caught.status < 500) setError(caught.message);
    }
  }, [persist]);

  useEffect(() => {
    let active = true;
    void store.getDeviceIdentity().then((stored) => {
      if (!active) return;
      identityRef.current = stored;
      setIdentity(stored);
      setLoading(false);
      if (stored && navigator.onLine) void refreshStatus();
    }).catch(() => {
      if (active) {
        setError("Nao foi possivel abrir a identidade segura deste dispositivo.");
        setLoading(false);
      }
    });
    const handleOnline = () => void refreshStatus();
    window.addEventListener("online", handleOnline);
    return () => {
      active = false;
      window.removeEventListener("online", handleOnline);
    };
  }, [refreshStatus, store]);

  const register = useCallback(async (displayName: string) => {
    if (!navigator.onLine) throw new Error("Conecte este dispositivo a internet para solicitar o cadastro.");
    if (!displayName.trim()) throw new Error("Informe um nome para identificar este dispositivo.");
    setBusy(true);
    setError(null);
    try {
      let current = identityRef.current;
      if (!current) {
        current = await generateDeviceIdentity(await store.getOrCreateDeviceId(), displayName);
        await persist(current);
      } else if (current.displayName !== displayName.trim()) {
        current = { ...current, displayName: displayName.trim(), updatedAt: new Date().toISOString() };
        await persist(current);
      }
      const server = await apiRequest<RegisteredDeviceResponse>("/api/devices/registration", {
        method: "POST",
        body: JSON.stringify({
          localDeviceId: current.localDeviceId,
          displayName: current.displayName,
          platform: current.platform,
          publicKey: current.publicKeySpki,
          keyAlgorithm: current.keyAlgorithm,
        }),
      });
      await persist(mergeServerIdentity(current, server));
    } catch (caught) {
      const message = caught instanceof Error ? caught.message : "Nao foi possivel solicitar o cadastro.";
      setError(message);
      throw caught;
    } finally {
      setBusy(false);
    }
  }, [persist, store]);

  const getDeviceSession = useCallback(async () => {
    const cached = sessionRef.current;
    if (cached && cached.expiresAt - Date.now() > 30_000) return cached.token;
    if (sessionPromiseRef.current) return sessionPromiseRef.current;
    const operation = (async () => {
      const current = identityRef.current;
      if (!navigator.onLine) throw new Error("Sem conexao para comprovar a identidade do dispositivo.");
      if (!current || current.status !== "AUTHORIZED") {
        throw new Error(current?.status === "REVOKED"
          ? "Este dispositivo foi revogado. A fila local permanece preservada para revisao."
          : "Este dispositivo ainda nao foi autorizado.");
      }
      const challenge = await apiRequest<DeviceChallengeResponse>("/api/devices/challenge", {
        method: "POST",
        body: JSON.stringify({ localDeviceId: current.localDeviceId }),
      });
      const signature = await signDeviceChallenge(current.privateKey, challenge.payload);
      const verified = await apiRequest<DeviceSessionResponse>("/api/devices/challenge/verify", {
        method: "POST",
        body: JSON.stringify({ challengeId: challenge.challengeId, signature }),
      });
      await persist(mergeServerIdentity(current, verified.device));
      sessionRef.current = { token: verified.deviceSessionToken, expiresAt: new Date(verified.expiresAt).getTime() };
      return verified.deviceSessionToken;
    })();
    sessionPromiseRef.current = operation;
    try {
      return await operation;
    } catch (caught) {
      sessionRef.current = null;
      if (caught instanceof DeviceApiError && caught.status === 403) await refreshStatus();
      throw caught;
    } finally {
      sessionPromiseRef.current = null;
    }
  }, [persist, refreshStatus]);

  const value = useMemo(() => ({ identity, loading, busy, error, register, refreshStatus, getDeviceSession }),
    [busy, error, getDeviceSession, identity, loading, refreshStatus, register]);
  return <DeviceIdentityContext.Provider value={value}>{children}</DeviceIdentityContext.Provider>;
}

export function useDeviceIdentity() {
  const context = useContext(DeviceIdentityContext);
  if (!context) throw new Error("useDeviceIdentity deve ser usado dentro de DeviceIdentityProvider.");
  return context;
}
