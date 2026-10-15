import { cookies } from "next/headers";
import { redirect } from "next/navigation";

import { ACCESS_TOKEN_COOKIE } from "@/lib/auth/config";

interface ApiProblemDetails {
  detail?: string;
  errors?: Record<string, string[]>;
  title?: string;
}

interface ApiFetchOptions {
  body?: unknown;
  method?: "GET" | "POST" | "PUT";
}

export interface PagedApiResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function getApiUrl(): string {
  const apiUrl = (process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL)?.replace(
    /\/$/,
    "",
  );

  if (!apiUrl) {
    console.error(
      "[apiFetch] Configuração ausente: a variável de ambiente NEXT_PUBLIC_API_URL não foi definida.",
    );
    throw new Error(
      "Configuração de sistema ausente: endereço da API não definido.",
    );
  }

  return apiUrl;
}

function getProblemMessage(
  problem: ApiProblemDetails | undefined,
  status: number,
): string {
  const validationMessages = problem?.errors
    ? Object.values(problem.errors).flat().filter(Boolean)
    : [];

  return (
    validationMessages.join(" ") ||
    problem?.detail ||
    problem?.title ||
    `A solicitação não pôde ser concluída (código HTTP ${status}).`
  );
}

export async function apiFetch<T>(
  path: string,
  options: ApiFetchOptions = {},
): Promise<T> {
  const apiUrl = getApiUrl();
  const requestUrl = `${apiUrl}${path}`;
  const correlationId = crypto.randomUUID();
  const hasBody = options.body !== undefined;
  const accessToken = (await cookies()).get(ACCESS_TOKEN_COOKIE)?.value;
  let response: Response;

  try {
    response = await fetch(requestUrl, {
      body: hasBody ? JSON.stringify(options.body) : undefined,
      cache: "no-store",
      headers: {
        Accept: "application/json",
        ...(hasBody ? { "Content-Type": "application/json" } : {}),
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
        "X-Correlation-ID": correlationId,
      },
      method: options.method ?? "GET",
    });
  } catch (error) {
    console.error("[apiFetch] Falha de rede ao conectar à API:", {
      requestUrl,
      correlationId,
      error: error instanceof Error ? error.message : String(error),
      cause: error,
    });
    throw new Error(
      "A API do Transjap Horímetros está indisponível ou fora do ar. Verifique se o serviço está em execução.",
      { cause: error },
    );
  }

  if (!response.ok) {
    if (response.status === 401) {
      redirect("/login?reason=session-expired");
    }

    if (response.status === 403) {
      redirect("/acesso-negado");
    }

    let problem: ApiProblemDetails | undefined;

    try {
      problem = (await response.json()) as ApiProblemDetails;
    } catch {
      problem = undefined;
    }

    console.error("[apiFetch] A API respondeu com status de erro:", {
      status: response.status,
      requestUrl,
      correlationId,
      problem,
    });

    throw new ApiError(
      response.status,
      getProblemMessage(problem, response.status),
    );
  }

  return (await response.json()) as T;
}
