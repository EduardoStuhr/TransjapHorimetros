import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { getUtilization } from "./operations.service";
import { UtilizationPanel } from "@/features/dashboard/components/utilization-panel";

vi.mock("./operations.service", () => ({ getUtilization: vi.fn() }));
describe("utilization panel", () => {
  it("renders empty state without presenting missing hours as zero", async () => {
    vi.mocked(getUtilization).mockResolvedValue({
      hours: null, calculatedIntervals: 0, unavailableIntervals: 0,
      totalItems: 0, page: 1, pageSize: 20, items: [],
    });
    const html = renderToStaticMarkup(await UtilizationPanel({ filters: {} }));
    expect(html).toContain("Horas indisponíveis");
    expect(html).toContain("Sem leituras");
    expect(html).not.toContain("Próxima");
  });
  it("links the authorized detail and preserves filters across pagination and exports", async () => {
    vi.mocked(getUtilization).mockResolvedValue({
      hours: 10.5, calculatedIntervals: 1, unavailableIntervals: 0,
      totalItems: 21, page: 1, pageSize: 20, items: [{
        machineId: "m", fleetNumber: 128, model: "D6", readingId: "r",
        at: "2026-11-13T00:00:00Z", initial: 100, final: 110.5, hours: 10.5,
        reason: "CALCULATED", workSiteId: null, workSite: null,
      }],
    });
    const html = renderToStaticMarkup(await UtilizationPanel({ filters: { fleetNumber: "128" } }));
    expect(html).toContain("10,5 HOURS");
    expect(html).toContain("/horimetros/r");
    expect(html).toContain("fleetNumber=128");
    expect(html).toContain("Próxima");
    expect(html).toContain("Não atribuída");
  });
});
