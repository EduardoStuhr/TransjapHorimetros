import { cookies } from "next/headers";
import { NextResponse } from "next/server";

import { clearAuthCookies, refreshSession } from "@/lib/auth/backend";
import {
  ACCESS_TOKEN_COOKIE,
  getApiBaseUrl,
  REFRESH_TOKEN_COOKIE,
} from "@/lib/auth/config";

async function revoke(accessToken: string, refreshToken: string) {
  return fetch(`${getApiBaseUrl()}/api/v1/auth/logout`, {
    body: JSON.stringify({ refreshToken }),
    cache: "no-store",
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${accessToken}`,
      "Content-Type": "application/json",
      "X-Correlation-ID": crypto.randomUUID(),
    },
    method: "POST",
  });
}

export async function POST() {
  const cookieStore = await cookies();
  let accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;
  let refreshToken = cookieStore.get(REFRESH_TOKEN_COOKIE)?.value;

  if (refreshToken) {
    let result = accessToken ? await revoke(accessToken, refreshToken) : null;
    if (!result || result.status === 401) {
      const rotated = await refreshSession(refreshToken);
      if (rotated) {
        accessToken = rotated.accessToken;
        refreshToken = rotated.refreshToken;
        result = await revoke(accessToken, refreshToken);
      }
    }
  }

  const response = new NextResponse(null, { status: 204 });
  clearAuthCookies(response.cookies);
  return response;
}
