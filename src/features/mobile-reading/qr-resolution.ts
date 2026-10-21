import type { CachedMachine } from "@/offline/types";

export type QrResolution =
  | { status: "RESOLVED"; machine: CachedMachine; source: "CACHE" | "SERVER" }
  | { status: "EMPTY" | "NOT_FOUND" | "NOT_FOUND_OFFLINE" | "NETWORK_ERROR" };

interface ResolveQrDependencies {
  online: boolean;
  resolveCached: (value: string) => Promise<CachedMachine | undefined>;
  requestServer: (value: string) => Promise<Response>;
  refreshReferenceData: () => Promise<void>;
  getCachedMachines: () => Promise<CachedMachine[]>;
}
export async function resolveMachineQr(
  rawValue: string,
  dependencies: ResolveQrDependencies,
): Promise<QrResolution> {
  const value = rawValue.trim();
  if (!value) return { status: "EMPTY" };

  const cached = await dependencies.resolveCached(value);
  if (cached) return { status: "RESOLVED", machine: cached, source: "CACHE" };
  if (!dependencies.online) return { status: "NOT_FOUND_OFFLINE" };

  try {
    const response = await dependencies.requestServer(value);
    if (!response.ok) return { status: "NOT_FOUND" };
    const payload = await response.json() as { machineId?: unknown };
    if (typeof payload.machineId !== "string") return { status: "NOT_FOUND" };

    await dependencies.refreshReferenceData();
    const officialMachine = (await dependencies.getCachedMachines())
      .find((machine) => machine.id === payload.machineId);
    return officialMachine
      ? { status: "RESOLVED", machine: officialMachine, source: "SERVER" }
      : { status: "NOT_FOUND" };
  } catch {
    return { status: "NETWORK_ERROR" };
  }
}
