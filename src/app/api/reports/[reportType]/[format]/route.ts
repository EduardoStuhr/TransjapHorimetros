import { apiFetch } from "@/services/api-client";
import type { AuthUser } from "@/lib/auth/types";
import { parseOperationalFilters } from "@/lib/operational-filters";
import {
  createExcelReport,
  createPdfReport,
} from "@/services/report-export.service";
import {
  getReportData,
  getReportDefinition,
  isReportType,
} from "@/services/reports.service";
import {
  apiUnavailableResponse,
  authenticatedUserHasRole,
} from "@/lib/auth/authenticated-api-proxy";

export const runtime = "nodejs";

export async function GET(
  request: Request,
  context: RouteContext<"/api/reports/[reportType]/[format]">,
) {
  let authorization: boolean | Response;
  try {
    authorization = await authenticatedUserHasRole([
      "SUPERVISOR",
      "ADMINISTRATIVE",
      "MANAGER",
      "ADMIN",
    ]);
  } catch {
    return apiUnavailableResponse();
  }

  if (authorization instanceof Response) return authorization;
  if (!authorization) {
    return Response.json({ detail: "Acesso negado." }, { status: 403 });
  }

  const { format, reportType } = await context.params;

  if (!isReportType(reportType) || (format !== "xlsx" && format !== "pdf")) {
    return Response.json(
      { message: "Relatório não encontrado." },
      { status: 404 },
    );
  }

  const user = await apiFetch<AuthUser>("/api/v1/auth/me");
  if (
    !user.roles.some((role) =>
      ["ADMIN", "MANAGER", "SUPERVISOR", "ADMINISTRATIVE"].includes(role),
    )
  ) {
    return Response.json(
      { message: "Acesso restrito à supervisão." },
      { status: 403 },
    );
  }
  const parsed = new URL(request.url).searchParams;
  let filters;
  try {
    filters = parseOperationalFilters(Object.fromEntries(parsed));
  } catch {
    return Response.json({ message: "Filtros inválidos." }, { status: 400 });
  }
  const data = await getReportData(reportType, filters);

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
      "Cache-Control": "private, no-store",
      "Content-Disposition": `attachment; filename="${filename}"`,
      "Content-Type":
        format === "xlsx"
          ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          : "application/pdf",
      "X-Report-Title": encodeURIComponent(definition.title),
    },
  });
}
