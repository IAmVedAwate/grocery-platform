// Port 5292 is the HTTP Kestrel endpoint (backend/src/Api/Properties/launchSettings.json).
// 7223 is HTTPS-only — plain http:// to that port gets ERR_EMPTY_RESPONSE,
// not a normal error, because Kestrel just drops a non-TLS connection.
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
  const response = await rawFetch(path, accessToken, options);
  if (response.status === 204) return undefined as T;
  if (!response.ok) await parseProblemDetails(response);
  return (await response.json()) as T;
}

/** For binary responses (product images) — apiFetch always parses JSON,
 * which would fail on an image body. */
export async function apiFetchBlob(path: string, accessToken: string | null): Promise<Blob> {
  const response = await rawFetch(path, accessToken, {});
  if (!response.ok) await parseProblemDetails(response);
  return response.blob();
}

async function rawFetch(path: string, accessToken: string | null, options: RequestInit): Promise<Response> {
  // FormData sets its own multipart boundary in the Content-Type header —
  // forcing application/json here would silently break every file upload.
  const isFormData = options.body instanceof FormData;
  return fetch(`${API_BASE_URL}${path}`, {
    ...options,
    credentials: "include",
    headers: {
      ...(options.body && !isFormData ? { "Content-Type": "application/json" } : {}),
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...options.headers,
    },
  });
}

export { API_BASE_URL };
