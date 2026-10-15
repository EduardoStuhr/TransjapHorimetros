import {
  ACCESS_TOKEN_COOKIE,
  authCookieBaseOptions,
  getApiBaseUrl,
  getRefreshCookieExpiresAt,
  REFRESH_TOKEN_COOKIE,
} from "@/lib/auth/config";
import type { BackendAuthResponse } from "@/lib/auth/types";

interface MutableCookieStore {
  set: (
    name: string,
    value: string,
    options: typeof authCookieBaseOptions & { expires: Date },
  ) => unknown;
}

export async function callAuthApi(
  path: "/login" | "/refresh",
  body: unknown,
) {
  return fetch(`${getApiBaseUrl()}/api/v1/auth${path}`, {
    body: JSON.stringify(body),
    cache: "no-store",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-Correlation-ID": crypto.randomUUID(),
    },
    method: "POST",
  });
}

export async function refreshSession(refreshToken: string) {
  const response = await callAuthApi("/refresh", { refreshToken });
  if (!response.ok) {
    return null;
  }

  return (await response.json()) as BackendAuthResponse;
}

export function setAuthCookies(
  cookieStore: MutableCookieStore,
  auth: BackendAuthResponse,
) {
  cookieStore.set(ACCESS_TOKEN_COOKIE, auth.accessToken, {
    ...authCookieBaseOptions,
    expires: new Date(auth.expiresAt),
  });
  cookieStore.set(REFRESH_TOKEN_COOKIE, auth.refreshToken, {
    ...authCookieBaseOptions,
    expires: getRefreshCookieExpiresAt(),
  });
}

export function clearAuthCookies(cookieStore: MutableCookieStore) {
  const expires = new Date(0);
  cookieStore.set(ACCESS_TOKEN_COOKIE, "", {
    ...authCookieBaseOptions,
    expires,
  });
  cookieStore.set(REFRESH_TOKEN_COOKIE, "", {
    ...authCookieBaseOptions,
    expires,
  });
}
