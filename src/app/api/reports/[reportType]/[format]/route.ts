import {
  createExcelReport,
  createPdfReport,
} from "@/services/report-export.service";
import {
  getReportData,
  getReportDefinition,
  isReportType,
} from "@/services/reports.service";

export const runtime = "nodejs";

export async function GET(
  _request: Request,
  context: RouteContext<"/api/reports/[reportType]/[format]">,
) {
  const { format, reportType } = await context.params;

  if (!isReportType(reportType) || (format !== "xlsx" && format !== "pdf")) {
    return Response.json({ message: "Relatório não encontrado." }, { status: 404 });
  }

  const data = await getReportData(reportType);

  if (data.items.length === 0) {
    return Response.json(
      { message: "Não há dados reais disponíveis para exportar." },
      { status: 404 },
    );
  }

  const definition = getReportDefinition(reportType);
  const filename = `${reportType}-${new Date().toISOString().slice(0, 10)}.${format}`;
  let responseBody: ArrayBuffer;

  if (format === "xlsx") {
    responseBody = await createExcelReport(data);
  } else {
    const pdfBytes = await createPdfReport(data);
    const pdfCopy = new Uint8Array(pdfBytes.byteLength);
    pdfCopy.set(pdfBytes);
    responseBody = pdfCopy.buffer;
  }

  return new Response(responseBody, {
    headers: {
      "Content-Disposition": `attachment; filename="${filename}"`,
      "Content-Type":
        format === "xlsx"
          ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          : "application/pdf",
      "X-Report-Title": encodeURIComponent(definition.title),
    },
  });
}
