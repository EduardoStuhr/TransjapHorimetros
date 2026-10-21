import {
  apiUnavailableResponse,
  authenticatedApiFetch,
  forwardApiResponse,
} from "@/lib/auth/authenticated-api-proxy";

export async function PATCH(
  request: Request,
  { params }: RouteContext<"/api/machines/[machineId]/meter-unit">,
) {
  const { machineId } = await params;
  const body = await request.text();
  try {
    const response = await authenticatedApiFetch(
      `/api/v1/machines/${encodeURIComponent(machineId)}/meter-unit`,
      {
        method: "PATCH",
        body,
        headers: { "Content-Type": "application/json" },
      },
    );
    if (!response) return Response.json({ detail: "Sessão expirada." }, { status: 401 });
    return forwardApiResponse(response);
  } catch {
    return apiUnavailableResponse();
  }
}
