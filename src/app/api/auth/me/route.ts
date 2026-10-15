import { cookies } from "next/headers";
import { NextResponse } from "next/server";

import { clearAuthCookies, refreshSession, setAuthCookies } from "@/lib/auth/backend";
import {
  ACCESS_TOKEN_COOKIE,
  getApiBaseUrl,
  REFRESH_TOKEN_COOKIE,
} from "@/lib/auth/config";
import type { AuthUser } from "@/lib/auth/types";

async function getMe(accessToken: string) {
  return fetch(`${getApiBaseUrl()}/api/v1/auth/me`, {
    cache: "no-store",
    headers: {
      Accept: "application/json",
      Authorization: `Bearer ${accessToken}`,
      "X-Correlation-ID": crypto.randomUUID(),
    },
  });
}

export async function GET() {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(ACCESS_TOKEN_COOKIE)?.value;
  const refreshToken = cookieStore.get(REFRESH_TOKEN_COOKIE)?.value;
  let apiResponse = accessToken ? await getMe(accessToken) : null;
  let rotatedSession = null;

  if ((!apiResponse || apiResponse.status === 401) && refreshToken) {
    rotatedSession = await refreshSession(refreshToken);
    if (rotatedSession) {
      apiResponse = await getMe(rotatedSession.accessToken);
    }
  }

  if (!apiResponse?.ok) {
    const response = NextResponse.json(
      { detail: apiResponse?.status === 403 ? "Acesso negado." : "Sessao expirada." },
      { status: apiResponse?.status === 403 ? 403 : 401 },
    );
    clearAuthCookies(response.cookies);
    return response;
  }

  const user = (await apiResponse.json()) as AuthUser;
  const response = NextResponse.json(user);
  if (rotatedSession) {
    setAuthCookies(response.cookies, rotatedSession);
  }
  return response;
}
