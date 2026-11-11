import ExcelJS from "exceljs";
import { PDFDocument, StandardFonts } from "pdf-lib";

import {
  formatAnomalySeverity,
  formatAnomalyStatus,
  formatAnomalyType,
  formatDateTime,
  formatReadingStatus,
} from "@/lib/domain-formatters";
import {
  getReportDefinition,
  type ReportData,
} from "@/services/reports.service";

interface ReportColumn {
  key: string;
  label: string;
  width: number;
}

interface PreparedReport {
  columns: readonly ReportColumn[];
  period: string;
  rows: readonly Record<string, string | number>[];
  title: string;
}

function getPeriod(values: readonly string[]) {
  if (values.length === 0) {
    return "Sem dados";
  }

  const ordered = values.toSorted();
  return `${formatDateTime(ordered[0])} a ${formatDateTime(ordered.at(-1))}`;
}

function prepareReport(data: ReportData): PreparedReport {
  const title = getReportDefinition(data.type).title;

  if (data.type === "anomalies") {
    return {
      title,
      period: getPeriod(data.items.map((item) => item.createdAt)),
      columns: [
        { key: "date", label: "Data/hora", width: 20 },
        { key: "fleet", label: "Frota", width: 10 },
        { key: "type", label: "Tipo", width: 32 },
        { key: "severity", label: "Severidade", width: 14 },
        { key: "status", label: "Status", width: 14 },
        { key: "description", label: "Descrição", width: 54 },
        { key: "readingId", label: "ID da leitura", width: 38 },
      ],
      rows: data.items.map((item) => ({
        date: item.createdAt,
        fleet: item.machineFleetNumber,
        type: formatAnomalyType(item.type),
        severity: formatAnomalySeverity(item.severity),
        status: formatAnomalyStatus(item.status),
        description: item.description,
        readingId: item.readingId,
      })),
    };
  }

  return {
    title,
    period: getPeriod(data.items.map((item) => item.capturedAtDevice)),
    columns: [
      { key: "date", label: "Data/hora", width: 20 },
      { key: "fleet", label: "Frota", width: 10 },
      { key: "model", label: "Modelo", width: 24 },
      { key: "value", label: "Valor", width: 14 },
      { key: "unit", label: "Unidade", width: 12 },
      { key: "operator", label: "Operador", width: 30 },
      { key: "previous", label: "Valor anterior", width: 16 },
      { key: "difference", label: "Diferença", width: 14 },
      { key: "ocr", label: "OCR", width: 14 },
      { key: "device", label: "Dispositivo", width: 22 },
      { key: "type", label: "Tipo", width: 18 },
      { key: "workSite", label: "Obra", width: 28 },
      { key: "status", label: "Status", width: 14 },
      { key: "received", label: "Recebido em", width: 20 },
      { key: "synced", label: "Sincronizado em", width: 20 },
      { key: "eventId", label: "ID do evento", width: 38 },
    ],
    rows: data.items.map((item) => ({
      date: item.capturedAtDevice,
      fleet: item.machineFleetNumber,
      model: item.model,
      value: item.value,
      unit: item.meterUnit ?? "Não definida",
      operator: item.createdByEmail ?? "Não informado",
      difference: item.differenceFromPrevious ?? "",
      previous: item.differenceFromPrevious == null ? "" : item.value - item.differenceFromPrevious,
      ocr: item.ocrDetectedValue ?? "",
      device: item.deviceId ?? "",
      type: item.readingType,
      workSite: item.workSiteName ?? "Sem obra informada",
      status: formatReadingStatus(item.status),
      received: item.receivedAtServer,
      synced: item.syncedAt ?? "",
      eventId: item.clientEventId,
    })),
  };
}

export async function createExcelReport(data: ReportData) {
  const report = prepareReport(data);
  const workbook = new ExcelJS.Workbook();
  const worksheet = workbook.addWorksheet("Relatório");
  const lastColumn = report.columns.length;

  workbook.creator = "Transjap Horímetros";
  workbook.created = new Date();

  worksheet.mergeCells(1, 1, 1, lastColumn);
  worksheet.getCell(1, 1).value = `Transjap — ${report.title}`;
  worksheet.getCell(1, 1).font = { bold: true, color: { argb: "FFFFFFFF" }, size: 16 };
  worksheet.getCell(1, 1).fill = {
    type: "pattern",
    pattern: "solid",
    fgColor: { argb: "FF173B67" },
  };
  worksheet.getRow(1).height = 28;

  worksheet.mergeCells(2, 1, 2, lastColumn);
  worksheet.getCell(2, 1).value = `Período dos dados: ${report.period} | Gerado em: ${formatDateTime(new Date().toISOString())}`;
  worksheet.getCell(2, 1).font = { color: { argb: "FF475569" }, italic: true };

  const headerRow = worksheet.getRow(4);
  headerRow.values = report.columns.map((column) => column.label);
  headerRow.font = { bold: true, color: { argb: "FFFFFFFF" } };
  headerRow.fill = {
    type: "pattern",
    pattern: "solid",
    fgColor: { argb: "FF2C5B9E" },
  };

  for (const row of report.rows) {
    worksheet.addRow(report.columns.map((column) =>
      ["date","received","synced"].includes(column.key) && row[column.key] ? new Date(String(row[column.key])) : row[column.key]));
  }

  report.columns.forEach((column, index) => {
    worksheet.getColumn(index + 1).width = column.width;
    if (["date","received","synced"].includes(column.key)) worksheet.getColumn(index + 1).numFmt = "dd/mm/yyyy hh:mm";
    if (["value","previous","difference","ocr"].includes(column.key)) worksheet.getColumn(index + 1).numFmt = "0.00";
  });
  worksheet.autoFilter = { from: { row: 4, column: 1 }, to: { row: 4, column: lastColumn } };
  worksheet.views = [{ state: "frozen", ySplit: 4 }];
  worksheet.eachRow({ includeEmpty: false }, (row, rowNumber) => {
    if (rowNumber > 4) {
      row.alignment = { vertical: "top", wrapText: true };
      if (rowNumber % 2 === 1) {
        row.fill = {
          type: "pattern",
          pattern: "solid",
          fgColor: { argb: "FFF8FAFC" },
        };
      }
    }
  });

  return workbook.xlsx.writeBuffer();
}

function normalizePdfText(value: string | number) {
  return String(value)
    .replaceAll("—", "-")
    .replaceAll("–", "-")
    .replaceAll("•", "-")
    .replace(/[^\x20-\x7E\xA0-\xFF]/g, "?");
}

export async function createPdfReport(data: ReportData) {
  const report = prepareReport(data);
  const document = await PDFDocument.create();
  const font = await document.embedFont(StandardFonts.Helvetica);
  const bold = await document.embedFont(StandardFonts.HelveticaBold);
  let page = document.addPage([841.89, 595.28]);
  let y = 510;
  const header = () => {
    page.drawText(normalizePdfText("Transjap - " + report.title), {x:32,y:560,font:bold,size:16});
    page.drawText(normalizePdfText("Período dos dados: " + report.period), {x:32,y:541,font,size:9});
    page.drawText("Gerado em: " + formatDateTime(new Date().toISOString()), {x:32,y:526,font,size:9});
    y = 505;
  };
  header();
  for (const row of report.rows) {
    // Complete key/value rows avoid truncating evidence identifiers and operator names.
    for (const column of report.columns) {
      const value = ["date","received","synced"].includes(column.key) && row[column.key] ? formatDateTime(String(row[column.key])) : row[column.key];
      const text = normalizePdfText(column.label + ": " + value);
      const lines = text.match(/.{1,120}/g) ?? [""];
      for (const line of lines) {
        if (y < 45) { page = document.addPage([841.89,595.28]); header(); }
        page.drawText(line, {x:36,y,font,size:9});
        y -= 13;
      }
    }
    y -= 14;
  }
  document.getPages().forEach((p, index) => p.drawText(`Página ${index + 1} / ${document.getPageCount()}`, {x:32,y:20,font,size:8}));
  return document.save();
}
