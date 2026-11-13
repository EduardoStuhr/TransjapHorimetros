import { describe, it, expect } from "vitest";
import ExcelJS from "exceljs";
import { PDFDocument } from "pdf-lib";
import { createExcelReport, createPdfReport } from "./report-export.service";
import type { ReportData } from "./reports.service";
import { utilizationReason } from "./utilization-presentation";

const data: ReportData = {
  type: "utilization",
  filters: { fleetNumber: "128", startDate: "2026-11-01", endDate: "2026-11-02" },
  items: [{
    machineId: "machine", fleetNumber: 128, model: "Máquina", readingId: "reading",
    at: "2026-11-02T00:00:00Z", initial: 100.01, final: 100.22,
    hours: 0.21, reason: "CALCULATED", workSiteId: "site", workSite: "=1+1",
  }],
};
describe("utilization exports", () => {
  it("retains numeric precision, filters and text without spreadsheet formulas", async () => {
    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.load(await createExcelReport(data));
    const sheet = workbook.worksheets[0];
    expect(sheet.getCell("A3").value).toContain("fleetNumber=128");
    expect(sheet.getCell("C5").value).toBe(100.01);
    expect(sheet.getCell("D5").value).toBe(100.22);
    expect(sheet.getCell("E5").value).toBe(0.21);
    expect(sheet.getCell("F5").value).toBe("=1+1");
    expect(sheet.getCell("F5").type).toBe(ExcelJS.ValueType.String);
    expect(sheet.getCell("A6").value).toContain("0,21");
  });
  it("generates a valid PDF and explains unavailable results", async () => {
    const pdf = await PDFDocument.load(await createPdfReport(data));
    expect(pdf.getPageCount()).toBeGreaterThan(0);
    expect(utilizationReason("NEGATIVE_OR_RESET")).toContain("negativa");
  });
});
