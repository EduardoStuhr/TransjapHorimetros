import { cookies } from "next/headers";

import { ACCESS_TOKEN_COOKIE, getApiBaseUrl } from "@/lib/auth/config";

export const runtime = "nodejs";

export async function GET(
  _request: Request,
  { params }: RouteContext<"/api/readings/[readingId]/photo/content">,
) {
  const { readingId } = await params;
  const accessToken = (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
  if (!accessToken) {
    return Response.json({ detail: "Sessão expirada." }, { status: 401 });
  }

  const apiResponse = await fetch(
    `${getApiBaseUrl()}/api/v1/readings/${encodeURIComponent(readingId)}/photo/content`,
    {
      cache: "no-store",
      headers: {
        Accept: "image/jpeg, image/png, image/webp",
        Authorization: `Bearer ${accessToken}`,
        "X-Correlation-ID": crypto.randomUUID(),
      },
    },
  );

  const headers = new Headers({
    "Cache-Control": "private, no-store",
    "Content-Type": apiResponse.headers.get("content-type") ?? "application/octet-stream",
    "X-Content-Type-Options": "nosniff",
  });
  const contentLength = apiResponse.headers.get("content-length");
  if (contentLength) {
    headers.set("Content-Length", contentLength);
  }

  return new Response(apiResponse.body, {
    status: apiResponse.status,
    headers,
  });
}
