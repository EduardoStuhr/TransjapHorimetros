import { cookies } from "next/headers";
import { ACCESS_TOKEN_COOKIE, getApiBaseUrl } from "@/lib/auth/config";

export const runtime = "nodejs";

export async function POST(request: Request, { params }: RouteContext<"/api/readings/[readingId]/photo/ocr/reprocess">) {
  const { readingId } = await params;
  const token = (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
  if (!token) return Response.json({ detail: "Sessão expirada." }, { status: 401 });
  // Cookie-authenticated mutation must originate from this application.
  if (request.headers.get("origin") !== new URL(request.url).origin)
    return Response.json({ detail: "Origem inválida." }, { status: 403 });
  try {
    const response = await fetch(`${getApiBaseUrl()}/api/v1/readings/${encodeURIComponent(readingId)}/photo/ocr/reprocess`, {
      method: "POST", cache: "no-store", signal: AbortSignal.timeout(160_000),
      headers: { Accept: "application/json", Authorization: `Bearer ${token}`, "X-Correlation-ID": crypto.randomUUID() },
    });
    return new Response(response.body, { status: response.status, headers: {
      "Cache-Control": "no-store", "Content-Type": response.headers.get("content-type") ?? "application/json",
    } });
  } catch {
    return Response.json({ detail: "Não foi possível consultar o processamento. Atualize o resultado antes de tentar novamente." }, { status: 502 });
  }
}
