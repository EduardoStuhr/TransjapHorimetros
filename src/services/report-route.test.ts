import { describe, expect, it, vi } from "vitest";
import { apiFetch } from "@/services/api-client";
import { GET } from "@/app/api/reports/[reportType]/[format]/route";

vi.mock("@/services/api-client", () => ({ apiFetch: vi.fn() }));
const context = {
  params: Promise.resolve({ reportType: "readings", format: "xlsx" }),
};

describe("report authorization", () => {
  it("blocks exports for operators using the verified API identity", async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce({ roles: ["OPERATOR"] });
    const response = await GET(
      new Request("http://localhost/api/reports/readings/xlsx"),
      context,
    );
    expect(response.status).toBe(403);
  });
  it("rejects invalid filters before querying report data", async () => {
    vi.mocked(apiFetch).mockResolvedValueOnce({ roles: ["SUPERVISOR"] });
    const response = await GET(
      new Request(
        "http://localhost/api/reports/readings/xlsx?machineId=invalid",
      ),
      context,
    );
    expect(response.status).toBe(400);
  });
});
