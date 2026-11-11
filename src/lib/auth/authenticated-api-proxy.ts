import { cookies } from "next/headers";

import { ACCESS_TOKEN_COOKIE, getApiBaseUrl } from "@/lib/auth/config";

export async function authenticatedApiFetch(path: string, init: RequestInit = {}) {
  const accessToken = (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
  if (!accessToken) return null;
  return fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    cache: "no-store",
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${accessToken}`,
      "X-Correlation-ID": crypto.randomUUID(),
      ...init.headers,
    },
  });
}

export function forwardApiResponse(response: Response) {
  return new Response(response.body, {
    status: response.status,
    headers: {
      "Cache-Control": "no-store",
      "Content-Type": response.headers.get("content-type") ?? "application/json",
    },
  });
}

export function apiUnavailableResponse() {
  return Response.json(
    { detail: "O servidor está temporariamente indisponível." },
    { status: 503, headers: { "Cache-Control": "no-store" } },
  );
}
