export const ACCESS_TOKEN_COOKIE = "transjap_access_token";
export const REFRESH_TOKEN_COOKIE = "transjap_refresh_token";

export const authCookieBaseOptions = {
  httpOnly: true,
  path: "/",
  sameSite: "strict" as const,
  secure: process.env.NODE_ENV === "production",
};

export function getApiBaseUrl() {
  const value = (process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL)?.replace(
    /\/$/,
    "",
  );

  if (!value) {
    throw new Error("API_URL ou NEXT_PUBLIC_API_URL deve ser configurada.");
  }

  return value;
}

export function getRefreshCookieExpiresAt() {
  const configuredDays = Number(process.env.AUTH_REFRESH_COOKIE_DAYS ?? "7");
  const days = Number.isFinite(configuredDays) && configuredDays > 0
    ? configuredDays
    : 7;

  return new Date(Date.now() + days * 24 * 60 * 60 * 1000);
}
