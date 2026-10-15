import { NextResponse, type NextRequest } from "next/server";

import { refreshSession, setAuthCookies } from "@/lib/auth/backend";
import {
  ACCESS_TOKEN_COOKIE,
  REFRESH_TOKEN_COOKIE,
} from "@/lib/auth/config";

function tokenIsCurrent(token: string | undefined) {
  if (!token) return false;
  try {
    const payload = JSON.parse(
      Buffer.from(token.split(".")[1], "base64url").toString("utf8"),
    ) as { exp?: number };
    return typeof payload.exp === "number" && payload.exp > Date.now() / 1000 + 15;
  } catch {
    return false;
  }
}

export async function proxy(request: NextRequest) {
  const pathname = request.nextUrl.pathname;
  const isLogin = pathname === "/login";
  let accessToken = request.cookies.get(ACCESS_TOKEN_COOKIE)?.value;
  const refreshToken = request.cookies.get(REFRESH_TOKEN_COOKIE)?.value;
  let rotatedSession = null;

  if (!tokenIsCurrent(accessToken) && refreshToken) {
    rotatedSession = await refreshSession(refreshToken);
    accessToken = rotatedSession?.accessToken;
  }

  if (!accessToken) {
    if (isLogin) return NextResponse.next();
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("returnTo", pathname);
    const response = NextResponse.redirect(loginUrl);
    response.cookies.delete(ACCESS_TOKEN_COOKIE);
    response.cookies.delete(REFRESH_TOKEN_COOKIE);
    return response;
  }

  if (isLogin) {
    const response = NextResponse.redirect(new URL("/", request.url));
    if (rotatedSession) setAuthCookies(response.cookies, rotatedSession);
    return response;
  }

  if (!rotatedSession) return NextResponse.next();

  const requestHeaders = new Headers(request.headers);
  const requestCookies = new Map(
    request.cookies.getAll().map((cookie) => [cookie.name, cookie.value]),
  );
  requestCookies.set(ACCESS_TOKEN_COOKIE, rotatedSession.accessToken);
  requestCookies.set(REFRESH_TOKEN_COOKIE, rotatedSession.refreshToken);
  requestHeaders.set(
    "cookie",
    [...requestCookies].map(([name, value]) => `${name}=${encodeURIComponent(value)}`).join("; "),
  );

  const response = NextResponse.next({ request: { headers: requestHeaders } });
  setAuthCookies(response.cookies, rotatedSession);
  return response;
}

export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico|.*\\.(?:png|jpg|jpeg|gif|svg|webp)$).*)"],
};
