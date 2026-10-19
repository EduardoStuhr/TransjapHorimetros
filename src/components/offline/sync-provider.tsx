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

import { useAuth } from "@/components/auth/auth-provider";
import { getOfflineStore } from "@/offline/indexed-db-store";
import { OfflineSyncService, type ConnectivityState, type SyncRunResult } from "@/offline/sync-engine";
import { BrowserSyncTransport } from "@/offline/sync-transport";
import type {
  CachedMachine,
  CachedWorkSite,
  OfflineReferenceDataResponse,
  QueueSnapshot,
} from "@/offline/types";

interface SyncContextValue {
  connectivity: ConnectivityState;
  isSyncing: boolean;
  authenticationRequired: boolean;
  snapshot: QueueSnapshot;
  machines: CachedMachine[];
  workSites: CachedWorkSite[];
  referenceDataFetchedAt: string | null;
  lastSyncAt: string | null;
  refresh: () => Promise<void>;
  syncNow: (manual?: boolean) => Promise<SyncRunResult>;
  refreshReferenceData: () => Promise<void>;
}

const emptySnapshot: QueueSnapshot = {
  events: [],
  pending: 0,
  syncing: 0,
  synced: 0,
  retryableFailures: 0,
  permanentFailures: 0,
  blockedByDependency: 0,
  otherUserPending: 0,
};

const SyncContext = createContext<SyncContextValue | null>(null);

export function SyncProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const store = useMemo(() => getOfflineStore(), []);
  const [snapshot, setSnapshot] = useState<QueueSnapshot>(emptySnapshot);
  const [machines, setMachines] = useState<CachedMachine[]>([]);
  const [workSites, setWorkSites] = useState<CachedWorkSite[]>([]);
  const [referenceDataFetchedAt, setReferenceDataFetchedAt] = useState<string | null>(null);
  const [connectivity, setConnectivity] = useState<ConnectivityState>("UNKNOWN");
  const [isSyncing, setIsSyncing] = useState(false);
  const [authenticationRequired, setAuthenticationRequired] = useState(false);
  const [lastSyncAt, setLastSyncAt] = useState<string | null>(null);
  const channelRef = useRef<BroadcastChannel | null>(null);

  const refresh = useCallback(async () => {
    const [nextSnapshot, nextMachines, nextWorkSites, fetchedAt] = await Promise.all([
      store.getQueueSnapshot(user.id),
      store.getCachedMachines(),
      store.getCachedWorkSites(),
      store.getReferenceDataFetchedAt(),
    ]);
    setSnapshot(nextSnapshot);
    setMachines(nextMachines);
    setWorkSites(nextWorkSites);
    setReferenceDataFetchedAt(fetchedAt);
  }, [store, user.id]);

  const notify = useCallback((type: "sync_started" | "sync_finished" | "queue_changed") => {
    channelRef.current?.postMessage({ type, userId: user.id, at: new Date().toISOString() });
  }, [user.id]);

  const syncNow = useCallback(async (manual = true) => {
    setIsSyncing(true);
    notify("sync_started");
    const engine = new OfflineSyncService(store, new BrowserSyncTransport(), {
      onConnectivityChange: setConnectivity,
      onAuthenticationRequired: () => {
        setAuthenticationRequired(true);
        window.dispatchEvent(new Event("transjap:auth-required"));
      },
    });
    try {
      const result = await engine.sync(user.id, manual);
      if (!result.skippedBecauseLocked) setLastSyncAt(new Date().toISOString());
      return result;
    } finally {
      setIsSyncing(false);
      await refresh();
      notify("sync_finished");
      notify("queue_changed");
    }
  }, [notify, refresh, store, user.id]);

  const refreshReferenceData = useCallback(async () => {
    try {
      const response = await fetch("/api/offline/reference-data", { cache: "no-store" });
      if (response.status === 401) {
        setAuthenticationRequired(true);
        window.dispatchEvent(new Event("transjap:auth-required"));
        return;
      }
      if (!response.ok) {
        if (response.status >= 500) setConnectivity("OFFLINE");
        return;
      }
      const data = await response.json() as OfflineReferenceDataResponse;
      await store.replaceReferenceData(data);
      setConnectivity("ONLINE");
      setAuthenticationRequired(false);
      await refresh();
      notify("queue_changed");
    } catch {
      setConnectivity("OFFLINE");
    }
  }, [notify, refresh, store]);

  useEffect(() => {
    if (typeof BroadcastChannel === "undefined") return;
    const channel = new BroadcastChannel("transjap-offline-sync");
    channelRef.current = channel;
    channel.onmessage = (event: MessageEvent<{ type?: string }>) => {
      if (event.data?.type === "sync_started") setIsSyncing(true);
      if (event.data?.type === "sync_finished") setIsSyncing(false);
      void refresh();
    };
    return () => {
      channel.close();
      channelRef.current = null;
    };
  }, [refresh]);

  useEffect(() => {
    void store.cacheAuthenticatedUser(user);
  }, [store, user]);

  useEffect(() => {
    const initialSync = window.setTimeout(() => {
      if (!navigator.onLine) setConnectivity("OFFLINE");
      void refresh();
      void refreshReferenceData().then(() => syncNow(false));
    }, 0);

    const handleOnline = () => {
      setConnectivity("UNKNOWN");
      void refreshReferenceData().then(() => syncNow(false));
    };
    const handleOffline = () => setConnectivity("OFFLINE");
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    return () => {
      window.clearTimeout(initialSync);
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [refresh, refreshReferenceData, syncNow]);

  useEffect(() => {
    if (isSyncing || authenticationRequired) return;
    const nextAttempt = snapshot.events
      .filter((event) => event.status === "FAILED_RETRYABLE" && event.nextAttemptAt)
      .map((event) => new Date(event.nextAttemptAt!).getTime())
      .filter(Number.isFinite)
      .sort((a, b) => a - b)[0];
    if (!nextAttempt) return;
    const delay = Math.min(Math.max(nextAttempt - Date.now(), 1_000), 30 * 60_000);
    const timeout = window.setTimeout(() => void syncNow(false), delay);
    return () => window.clearTimeout(timeout);
  }, [authenticationRequired, isSyncing, snapshot.events, syncNow]);

  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;
    void navigator.serviceWorker
      .register("/sw.js", { scope: "/", updateViaCache: "none" })
      .then(() => navigator.serviceWorker.ready)
      .then((registration) => registration.active?.postMessage({ type: "CACHE_FIELD_SHELL" }));
  }, []);

  const value = useMemo<SyncContextValue>(() => ({
    connectivity,
    isSyncing,
    authenticationRequired,
    snapshot,
    machines,
    workSites,
    referenceDataFetchedAt,
    lastSyncAt,
    refresh,
    syncNow,
    refreshReferenceData,
  }), [
    authenticationRequired,
    connectivity,
    isSyncing,
    lastSyncAt,
    machines,
    referenceDataFetchedAt,
    refresh,
    refreshReferenceData,
    snapshot,
    syncNow,
    workSites,
  ]);

  return <SyncContext.Provider value={value}>{children}</SyncContext.Provider>;
}

export function useSync() {
  const context = useContext(SyncContext);
  if (!context) throw new Error("useSync deve ser usado dentro de SyncProvider.");
  return context;
}
