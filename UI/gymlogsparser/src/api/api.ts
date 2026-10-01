export class ApiError extends Error {
  public status: number;

  constructor(status: number, message = `HTTP ${status}`) {
    super(message);
    this.status = status;
  }
}

let onUnauthorized: (() => void) | null = null;
export const setUnauthorizedHandler = (fn: () => void) => (onUnauthorized = fn);

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const res = await fetch(`${import.meta.env.VITE_API_URL ?? ""}${path}`, {
    credentials: "include",
    ...init,
    headers: { "X-Requested-With": "fetch", ...(init.headers ?? {}) },
  });
  if (res.status === 401) {
    if (!path.startsWith("/auth/me")) onUnauthorized?.();
    throw new ApiError(401);
  }
  if (!res.ok) {
    let message: string | undefined;
    try {
      message = (await res.json())?.error;
    } catch {
      // keep the default message
    }
    throw new ApiError(res.status, message);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

const json = (method: string, body?: unknown): RequestInit => ({
  method,
  headers: { "Content-Type": "application/json" },
  body: body === undefined ? undefined : JSON.stringify(body),
});

export const api = {
  get: <T>(p: string) => request<T>(p),
  post: <T>(p: string, body?: unknown) => request<T>(p, json("POST", body)),
  put: <T = void>(p: string, body?: unknown) =>
    request<T>(p, json("PUT", body)),
  delete: <T = void>(p: string) => request<T>(p, { method: "DELETE" }),
};
