import { cookies } from "next/headers";
import { NextResponse } from "next/server";

import { clearAuthCookies, refreshSession, setAuthCookies } from "@/lib/auth/backend";
import { REFRESH_TOKEN_COOKIE } from "@/lib/auth/config";
import type { PublicAuthResponse } from "@/lib/auth/types";

export async function POST() {
  const refreshToken = (await cookies()).get(REFRESH_TOKEN_COOKIE)?.value;

  if (!refreshToken) {
    return NextResponse.json({ detail: "Sessao nao encontrada." }, { status: 401 });
  }

  const auth = await refreshSession(refreshToken);
  if (!auth) {
    const response = NextResponse.json(
      { detail: "Sessao expirada. Entre novamente." },
      { status: 401 },
    );
    clearAuthCookies(response.cookies);
    return response;
  }

  const response = NextResponse.json<PublicAuthResponse>({
    expiresAt: auth.expiresAt,
    user: auth.user,
  });
  setAuthCookies(response.cookies, auth);
  return response;
}
