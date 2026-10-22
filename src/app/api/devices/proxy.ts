import {
  apiUnavailableResponse,
  authenticatedApiFetch,
  forwardApiResponse,
} from "@/lib/auth/authenticated-api-proxy";

export async function proxyDeviceRequest(path: string, request: Request, method = request.method) {
  try {
    const body = method === "GET" ? undefined : await request.text();
    const response = await authenticatedApiFetch(path, {
      method,
      body,
      headers: body ? { "Content-Type": "application/json" } : undefined,
    });
    if (!response) return Response.json({ detail: "Sessao expirada." }, { status: 401 });
    return forwardApiResponse(response);
  } catch {
    return apiUnavailableResponse();
  }
}
