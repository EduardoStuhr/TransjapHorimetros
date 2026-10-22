import { cookies } from "next/headers";

import { ACCESS_TOKEN_COOKIE, getApiBaseUrl } from "@/lib/auth/config";

export const runtime = "nodejs";

const maximumMultipartBodyLength = 11 * 1024 * 1024;

export async function POST(
  request: Request,
  { params }: RouteContext<"/api/readings/[readingId]/photo">,
) {
  const { readingId } = await params;
  const accessToken = (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
  if (!accessToken) {
    return Response.json({ detail: "Sessão expirada." }, { status: 401 });
  }

  const contentType = request.headers.get("content-type");
  const contentLength = Number(request.headers.get("content-length") ?? "0");
  if (!contentType?.startsWith("multipart/form-data")) {
    return Response.json({ detail: "Envie a foto como multipart/form-data." }, { status: 400 });
  }
  if (contentLength > maximumMultipartBodyLength) {
    return Response.json({ detail: "A foto excede o limite permitido." }, { status: 413 });
  }

  let apiResponse: Response;
  try {
    apiResponse = await fetch(
      `${getApiBaseUrl()}/api/v1/readings/${encodeURIComponent(readingId)}/photo`,
      {
        method: "POST",
        body: request.body,
        cache: "no-store",
        headers: {
          Accept: "application/json",
          Authorization: `Bearer ${accessToken}`,
          "Content-Type": contentType,
          "X-Correlation-ID": crypto.randomUUID(),
          "X-Device-Session": request.headers.get("X-Device-Session") ?? "",
        },
        duplex: "half",
      } as RequestInit & { duplex: "half" },
    );
  } catch {
    return Response.json(
      { detail: "O servidor está temporariamente indisponível." },
      { status: 503 },
    );
  }

  return new Response(apiResponse.body, {
    status: apiResponse.status,
    headers: {
      "Cache-Control": "no-store",
      "Content-Type": apiResponse.headers.get("content-type") ?? "application/json",
    },
  });
}
