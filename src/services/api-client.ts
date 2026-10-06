interface ApiProblemDetails {
  detail?: string;
  title?: string;
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
  const apiUrl = process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "");

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

export async function apiFetch<T>(path: string): Promise<T> {
  const apiUrl = getApiUrl();
  const requestUrl = `${apiUrl}${path}`;
  const correlationId = crypto.randomUUID();
  let response: Response;

  try {
    response = await fetch(requestUrl, {
      cache: "no-store",
      headers: {
        Accept: "application/json",
        "X-Correlation-ID": correlationId,
      },
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

    const userMessage =
      problem?.detail ??
      problem?.title ??
      `A solicitação não pôde ser concluída (código HTTP ${response.status}).`;

    throw new ApiError(response.status, userMessage);
  }

  return (await response.json()) as T;
}
