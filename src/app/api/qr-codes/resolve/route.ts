import {
  apiUnavailableResponse,
  authenticatedApiFetch,
  forwardApiResponse,
} from "@/lib/auth/authenticated-api-proxy";

export async function GET(request: Request) {
  const value = new URL(request.url).searchParams.get("value")?.trim();
  if (!value) return Response.json({ detail: "Informe o conteúdo do QR code." }, { status: 400 });
  try {
    const response = await authenticatedApiFetch(
      `/api/v1/qr-codes/resolve?value=${encodeURIComponent(value)}`,
    );
    if (!response) return Response.json({ detail: "Sessão expirada." }, { status: 401 });
    return forwardApiResponse(response);
  } catch {
    return apiUnavailableResponse();
  }
}
