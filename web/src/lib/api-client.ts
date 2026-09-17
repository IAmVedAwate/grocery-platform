const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5292";

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public errors?: Record<string, string[]>,
  ) {
    super(message);
  }
}

async function parseProblemDetails(response: Response): Promise<never> {
  let detail = response.statusText;
  let errors: Record<string, string[]> | undefined;
  try {
    const body = await response.json();
    detail = body.detail ?? body.title ?? detail;
    errors = body.errors;
  } catch {
    // non-JSON error body — fall back to statusText
  }
  throw new ApiError(response.status, detail, errors);
}

/**
 * Every call sends credentials so the httpOnly refresh cookie (set by
 * /auth/login and /auth/refresh) is included automatically — see
 * docs/architecture/authentication-flow.md. Access tokens are held in
 * memory by AuthContext, never localStorage, to reduce XSS exposure.
 */
export async function apiFetch<T>(
  path: string,
  accessToken: string | null,
  options: RequestInit = {},
): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    credentials: "include",
    headers: {
      ...(options.body ? { "Content-Type": "application/json" } : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...options.headers,
    },
  });

  if (response.status === 204) return undefined as T;
  if (!response.ok) await parseProblemDetails(response);
  return (await response.json()) as T;
}

export { API_BASE_URL };
