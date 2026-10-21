import { describe, expect, it, vi } from "vitest";

import { resolveMachineQr } from "@/features/mobile-reading/qr-resolution";
import type { CachedMachine } from "@/offline/types";

const machine: CachedMachine = {
  id: "machine-68",
  fleetNumber: 68,
  model: "Escavadeira",
  meterUnit: "HOURS",
  status: "ACTIVE",
  activeQrToken: "TOKEN-68",
  updatedAtServer: "2026-10-20T10:00:00.000Z",
  fetchedAt: "2026-10-20T10:00:00.000Z",
  cacheVersion: 1,
};

function dependencies(overrides: Partial<Parameters<typeof resolveMachineQr>[1]> = {}) {
  return {
    online: true,
    resolveCached: vi.fn(async () => undefined),
    requestServer: vi.fn(async () => new Response(JSON.stringify({ machineId: machine.id }), { status: 200 })),
    refreshReferenceData: vi.fn(async () => undefined),
    getCachedMachines: vi.fn(async () => [machine]),
    ...overrides,
  };
}

describe("resolução segura de QR", () => {
  it("resolve primeiro pelo cache e não consulta a rede", async () => {
    const deps = dependencies({ resolveCached: vi.fn(async () => machine) });
    const result = await resolveMachineQr("0068", deps);
    expect(result).toEqual({ status: "RESOLVED", machine, source: "CACHE" });
    expect(deps.requestServer).not.toHaveBeenCalled();
  });

  it("mantém fallback manual funcional e informa ausência no cache offline", async () => {
    const deps = dependencies({ online: false });
    expect(await resolveMachineQr("FROTA 9999", deps)).toEqual({ status: "NOT_FOUND_OFFLINE" });
    expect(deps.requestServer).not.toHaveBeenCalled();
  });

  it("não confia apenas no MachineId retornado e exige a máquina no cache oficial atualizado", async () => {
    const deps = dependencies({ getCachedMachines: vi.fn(async () => []) });
    expect(await resolveMachineQr("TOKEN-68", deps)).toEqual({ status: "NOT_FOUND" });
    expect(deps.refreshReferenceData).toHaveBeenCalledOnce();
  });

  it("resolve pela API somente após reconciliar os dados oficiais", async () => {
    const deps = dependencies();
    expect(await resolveMachineQr("TOKEN-68", deps)).toEqual({ status: "RESOLVED", machine, source: "SERVER" });
  });

  it("trata código vazio e falha de rede sem lançar erro", async () => {
    expect(await resolveMachineQr("  ", dependencies())).toEqual({ status: "EMPTY" });
    const deps = dependencies({ requestServer: vi.fn(async () => { throw new Error("offline"); }) });
    expect(await resolveMachineQr("TOKEN-X", deps)).toEqual({ status: "NETWORK_ERROR" });
  });
});
