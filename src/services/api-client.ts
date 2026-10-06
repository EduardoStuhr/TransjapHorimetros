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

function getApiUrl() {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "");

  if (!apiUrl) {
    throw new Error("Não foi possível conectar à API.");
  }

  return apiUrl;
}

export async function apiFetch<T>(path: string): Promise<T> {
  let response: Response;

  try {
    response = await fetch(`${getApiUrl()}${path}`, {
      cache: "no-store",
      headers: {
        Accept: "application/json",
        "X-Correlation-ID": crypto.randomUUID(),
      },
    });
  } catch (error) {
    throw new Error("Não foi possível conectar à API.", { cause: error });
  }

  if (!response.ok) {
    let problem: ApiProblemDetails | undefined;

    try {
      problem = (await response.json()) as ApiProblemDetails;
    } catch {
      problem = undefined;
    }

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? "A API rejeitou a solicitação.",
    );
  }

  return (await response.json()) as T;
}
