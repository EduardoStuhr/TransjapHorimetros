import ExcelJS from "exceljs";
import { PDFDocument, StandardFonts, rgb } from "pdf-lib";

import {
  formatAnomalySeverity,
  formatAnomalyStatus,
  formatAnomalyType,
  formatDateTime,
  formatHourMeter,
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
        date: formatDateTime(item.createdAt),
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
      { key: "value", label: "Horímetro", width: 14 },
      { key: "type", label: "Tipo", width: 18 },
      { key: "workSite", label: "Obra", width: 28 },
      { key: "status", label: "Status", width: 14 },
      { key: "received", label: "Recebido em", width: 20 },
      { key: "synced", label: "Sincronizado em", width: 20 },
      { key: "eventId", label: "ID do evento", width: 38 },
    ],
    rows: data.items.map((item) => ({
      date: formatDateTime(item.capturedAtDevice),
      fleet: item.machineFleetNumber,
      model: item.model,
      value: formatHourMeter(item.value),
      type: item.readingType,
      workSite: item.workSiteName ?? "Sem obra informada",
      status: formatReadingStatus(item.status),
      received: formatDateTime(item.receivedAtServer),
      synced: formatDateTime(item.syncedAt),
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
    worksheet.addRow(report.columns.map((column) => row[column.key]));
  }

  report.columns.forEach((column, index) => {
    worksheet.getColumn(index + 1).width = column.width;
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
    .replaceAll("•", "-");
}

function fitPdfText(value: string | number, maxCharacters: number) {
  const normalized = normalizePdfText(value);
  return normalized.length <= maxCharacters
    ? normalized
    : `${normalized.slice(0, Math.max(0, maxCharacters - 3))}...`;
}

export async function createPdfReport(data: ReportData) {
  const report = prepareReport(data);
  const document = await PDFDocument.create();
  const regularFont = await document.embedFont(StandardFonts.Helvetica);
  const boldFont = await document.embedFont(StandardFonts.HelveticaBold);
  const pageSize: [number, number] = [841.89, 595.28];
  const margin = 28;
  const headerHeight = 82;
  const tableHeaderHeight = 24;
  const rowHeight = 22;
  const usableWidth = pageSize[0] - margin * 2;
  const totalWeight = report.columns.reduce((sum, column) => sum + column.width, 0);
  const widths = report.columns.map((column) =>
    (column.width / totalWeight) * usableWidth,
  );
  let page = document.addPage(pageSize);
  let y = pageSize[1] - margin;

  const drawPageHeader = () => {
    page.drawRectangle({
      x: 0,
      y: pageSize[1] - 64,
      width: pageSize[0],
      height: 64,
      color: rgb(0.09, 0.23, 0.4),
    });
    page.drawText(`Transjap - ${normalizePdfText(report.title)}`, {
      x: margin,
      y: pageSize[1] - 34,
      font: boldFont,
      size: 16,
      color: rgb(1, 1, 1),
    });
    page.drawText(
      `Periodo dos dados: ${normalizePdfText(report.period)} | Gerado em: ${normalizePdfText(formatDateTime(new Date().toISOString()))}`,
      {
        x: margin,
        y: pageSize[1] - 51,
        font: regularFont,
        size: 8,
        color: rgb(0.88, 0.93, 0.98),
      },
    );
    y = pageSize[1] - headerHeight;
  };

  const drawTableHeader = () => {
    page.drawRectangle({
      x: margin,
      y: y - tableHeaderHeight + 6,
      width: usableWidth,
      height: tableHeaderHeight,
      color: rgb(0.17, 0.36, 0.62),
    });
    let x = margin + 4;
    report.columns.forEach((column, index) => {
      page.drawText(fitPdfText(column.label, Math.max(5, Math.floor(widths[index] / 5))), {
        x,
        y: y - 10,
        font: boldFont,
        size: 7,
        color: rgb(1, 1, 1),
      });
      x += widths[index];
    });
    y -= tableHeaderHeight;
  };

  drawPageHeader();
  drawTableHeader();

  report.rows.forEach((row, rowIndex) => {
    if (y - rowHeight < margin) {
      page = document.addPage(pageSize);
      drawPageHeader();
      drawTableHeader();
    }

    if (rowIndex % 2 === 1) {
      page.drawRectangle({
        x: margin,
        y: y - rowHeight + 5,
        width: usableWidth,
        height: rowHeight,
        color: rgb(0.97, 0.98, 0.99),
      });
    }

    let x = margin + 4;
    report.columns.forEach((column, columnIndex) => {
      page.drawText(
        fitPdfText(
          row[column.key],
          Math.max(4, Math.floor(widths[columnIndex] / 4.6)),
        ),
        {
          x,
          y: y - 9,
          font: regularFont,
          size: 6.5,
          color: rgb(0.12, 0.16, 0.23),
        },
      );
      x += widths[columnIndex];
    });
    y -= rowHeight;
  });

  return document.save();
}
