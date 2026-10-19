import {
  apiUnavailableResponse,
  authenticatedApiFetch,
  forwardApiResponse,
} from "@/lib/auth/authenticated-api-proxy";

export async function POST(request: Request) {
  const body = await request.text();
  try {
    const response = await authenticatedApiFetch("/api/v1/readings", {
      method: "POST",
      body,
      headers: { "Content-Type": "application/json" },
    });
    if (!response) return Response.json({ detail: "Sessão expirada." }, { status: 401 });
    return forwardApiResponse(response);
  } catch {
    return apiUnavailableResponse();
  }
}
