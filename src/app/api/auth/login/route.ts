import { NextResponse } from "next/server";

import { callAuthApi, setAuthCookies } from "@/lib/auth/backend";
import type { BackendAuthResponse, PublicAuthResponse } from "@/lib/auth/types";

export async function POST(request: Request) {
  let credentials: unknown;

  try {
    credentials = await request.json();
  } catch {
    return NextResponse.json(
      { detail: "Informe e-mail e senha validos." },
      { status: 400 },
    );
  }

  const apiResponse = await callAuthApi("/login", credentials);
  if (!apiResponse.ok) {
    const body = await apiResponse.text();
    return new NextResponse(body || null, {
      headers: { "Content-Type": apiResponse.headers.get("content-type") ?? "application/problem+json" },
      status: apiResponse.status,
    });
  }

  const auth = (await apiResponse.json()) as BackendAuthResponse;
  const response = NextResponse.json<PublicAuthResponse>({
    expiresAt: auth.expiresAt,
    user: auth.user,
  });
  setAuthCookies(response.cookies, auth);
  return response;
}
