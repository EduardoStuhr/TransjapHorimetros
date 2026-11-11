import { describe, it, expect } from "vitest";
import { parseOperationalFilters, filterSearch } from "./operational-filters";

describe("operational filters", () => {
  it("preserves the same filters in report links", () => {
    const filters = parseOperationalFilters({ fleetNumber: "84", startDate:"2026-11-01", endDate:"2026-11-11", status:"PENDING" });
    const query = new URLSearchParams(filterSearch(filters));
    expect(query.get("fleetNumber")).toBe("84");
    expect(query.get("status")).toBe("PENDING");
    expect(query.has("workSiteId")).toBe(false);
  });
  it("rejects inverted dates and invalid identifiers", () => {
    expect(() => parseOperationalFilters({startDate:"2026-11-11",endDate:"2026-01-01"})).toThrow();
    expect(() => parseOperationalFilters({machineId:"invalid"})).toThrow();
  });
});
