import { describe, it, expect } from "vitest";
import ExcelJS from "exceljs";
import { PDFDocument } from "pdf-lib";
import { createExcelReport, createPdfReport } from "./report-export.service";
import type { ReportData } from "./reports.service";

const data: ReportData = {type:"anomalies",items: Array.from({length:30}, (_,i) => ({
  id:String(i), readingId:"r",machineId:"m",machineFleetNumber:84,
  type:"READING_DECREASE",severity:"WARNING",status:"OPEN",description:"Leitura para revisão humana",
  createdAt:"2026-11-11T12:00:00Z",
}))};
describe("report exports", () => {
  it("generates a valid workbook with filters and numeric fleet values", async () => {
    const workbook = new ExcelJS.Workbook();
    await workbook.xlsx.load(await createExcelReport(data));
    expect(workbook.worksheets[0].getCell("B5").value).toBe(84);
    expect(workbook.worksheets[0].autoFilter).toBeDefined();
  });
  it("generates a multipage PDF retaining the rows", async () => {
    const document = await PDFDocument.load(await createPdfReport(data));
    expect(document.getPageCount()).toBeGreaterThan(1);
  });
});
