import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import { apiConfig } from '@/auth';

// Create axios instance with base configuration
const apiClient: AxiosInstance = axios.create({
  baseURL: apiConfig.baseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000,
});

// Token getter function - will be set by AuthProvider
let getAccessTokenFn: (() => Promise<string | null>) | null = null;

// Function to set the token getter (called by AuthProvider)
export function setAccessTokenGetter(getter: () => Promise<string | null>) {
  getAccessTokenFn = getter;
}

// Request interceptor to add auth token
apiClient.interceptors.request.use(
  async (config: InternalAxiosRequestConfig) => {
    if (getAccessTokenFn) {
      try {
        const token = await getAccessTokenFn();
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
      } catch (error) {
        console.error('Error getting access token:', error);
      }
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

const statusMessages: Record<number, string> = {
  400: 'The request was invalid. Check your inputs and try again.',
  401: 'You do not have permission to perform this action. PlatformAdmin role required.',
  403: 'You do not have permission to perform this action. PlatformAdmin role required.',
  404: 'We could not find what you requested.',
  409: 'This request could not be completed because of a conflict.',
  422: 'Some of the provided data is not valid. Please review and try again.',
  429: 'Too many requests. Please wait a moment and try again.',
  500: 'Something went wrong on our side. Please try again shortly.',
  502: 'The service is unavailable right now. Please try again shortly.',
  503: 'The service is unavailable right now. Please try again shortly.',
  504: 'The request timed out. Please try again.',
};

const resolveErrorMessage = (error: AxiosError): string => {
  const status = error.response?.status;
  if (!status) {
    return 'Unable to reach the service. Check your connection and try again.';
  }

  const data = error.response?.data as { message?: string } | undefined;
  const message = data?.message?.trim();
  if (message) {
    return message;
  }

  return statusMessages[status] ?? 'Something went wrong. Please try again.';
};

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    // Handle 401 Unauthorized - token might be expired
    if (error.response?.status === 401 && originalRequest && !originalRequest._retry) {
      originalRequest._retry = true;

      if (getAccessTokenFn) {
        try {
          const token = await getAccessTokenFn();
          if (token) {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return apiClient(originalRequest);
          }
        } catch (refreshError) {
          console.error('Error refreshing token:', refreshError);
          // Redirect to login if token refresh fails
          window.location.href = '/login';
        }
      }
    }

    // Handle other errors
    if (error.response?.status === 403) {
      console.error('Access forbidden:', error.response.data);
    }

    if (error.response?.status === 404) {
      console.error('Resource not found:', error.response.data);
    }

    if (error.response?.status && error.response.status >= 500) {
      console.error('Server error:', error.response.data);
    }

    const message = resolveErrorMessage(error);
    return Promise.reject({ ...error, userMessage: message });
  }
);

// API helper functions
export const api = {
  // GET request
  get: <T>(url: string, config?: object) => 
    apiClient.get<T>(url, config).then((res) => res.data),

  // POST request
  post: <T>(url: string, data?: object, config?: object) =>
    apiClient.post<T>(url, data, config).then((res) => res.data),

  // PUT request
  put: <T>(url: string, data?: object, config?: object) =>
    apiClient.put<T>(url, data, config).then((res) => res.data),

  // PATCH request
  patch: <T>(url: string, data?: object, config?: object) =>
    apiClient.patch<T>(url, data, config).then((res) => res.data),

  // DELETE request
  delete: <T>(url: string, config?: object) =>
    apiClient.delete<T>(url, config).then((res) => res.data),
};

export default apiClient;
