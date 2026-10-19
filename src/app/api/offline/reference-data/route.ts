import {
  apiUnavailableResponse,
  authenticatedApiFetch,
  forwardApiResponse,
} from "@/lib/auth/authenticated-api-proxy";

export async function GET() {
  try {
    const response = await authenticatedApiFetch("/api/v1/offline/reference-data");
    if (!response) return Response.json({ detail: "Sessão expirada." }, { status: 401 });
    return forwardApiResponse(response);
  } catch {
    return apiUnavailableResponse();
  }
}
