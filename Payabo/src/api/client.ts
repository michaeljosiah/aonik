import { readAccessToken } from "../app/auth/authStorage";
import { PAYABO_TENANT_ID } from "../config/tenant";

const rawBaseUrl = import.meta.env.VITE_AONIK_API_BASE_URL ?? "https://localhost:5001";
const apiBaseUrl = rawBaseUrl.replace(/\/+$/, "");

const buildUrl = (path: string) => {
  if (path.startsWith("http")) {
    return path;
  }

  const normalized = path.startsWith("/") ? path : `/${path}`;
  return `${apiBaseUrl}${normalized}`;
};

const buildHeaders = (headers?: HeadersInit, contentType?: "json" | "multipart") => {
  const resolved = new Headers(headers);
  resolved.set("Accept", "application/json");
  resolved.set("X-Tenant-Id", PAYABO_TENANT_ID);

  if (contentType === "json") {
    resolved.set("Content-Type", "application/json");
  }

  const accessToken = readAccessToken();
  if (accessToken) {
    resolved.set("Authorization", `Bearer ${accessToken}`);
  }

  return resolved;
};

const apiRequest = async <T>(path: string, init: RequestInit, contentType?: "json" | "multipart"): Promise<T> => {
  const response = await fetch(buildUrl(path), {
    ...init,
    headers: buildHeaders(init.headers, contentType)
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    let details: unknown;

    try {
      details = await response.json();
      if (details && typeof details === "object" && "error" in details && typeof details.error === "string") {
        message = details.error;
      }
    } catch {
      details = undefined;
    }

    const error = new Error(message) as Error & { status?: number; details?: unknown };
    error.status = response.status;
    error.details = details;
    throw error;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
};

export const apiGet = async <T>(path: string, init?: RequestInit): Promise<T> => {
  return apiRequest<T>(path, {
    ...init,
    method: "GET"
  });
};

export const apiPost = async <T>(path: string, body: unknown, init?: RequestInit): Promise<T> => {
  return apiRequest<T>(path, {
    ...init,
    method: "POST",
    body: JSON.stringify(body)
  }, "json");
};

export const apiPut = async <T>(path: string, body: unknown, init?: RequestInit): Promise<T> => {
  return apiRequest<T>(path, {
    ...init,
    method: "PUT",
    body: JSON.stringify(body)
  }, "json");
};

export const apiPatch = async <T>(path: string, body: unknown, init?: RequestInit): Promise<T> => {
  return apiRequest<T>(path, {
    ...init,
    method: "PATCH",
    body: JSON.stringify(body)
  }, "json");
};

export const apiDelete = async <T>(path: string, init?: RequestInit): Promise<T> => {
  return apiRequest<T>(path, {
    ...init,
    method: "DELETE"
  });
};

export const apiPostForm = async <T>(path: string, formData: FormData, init?: RequestInit): Promise<T> => {
  return apiRequest<T>(path, {
    ...init,
    method: "POST",
    body: formData
  }, "multipart");
};
