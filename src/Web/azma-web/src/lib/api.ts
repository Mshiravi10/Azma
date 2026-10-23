import axios, { type AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import { useAuthStore } from './auth/store';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api';

export const api: AxiosInstance = axios.create({
  baseURL,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const { accessToken } = useAuthStore.getState();
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`);
  }
  return config;
});

let refreshing: Promise<string | null> | null = null;

async function tryRefresh(): Promise<string | null> {
  if (refreshing) return refreshing;
  if (!useAuthStore.getState().refreshToken) return null;
  refreshing = (async () => {
    try {
      // Read the token fresh inside the IIFE so that if another tab already
      // rotated it (and Zustand's persist synced from localStorage), we use
      // the latest value rather than a stale capture from before the promise.
      const { refreshToken } = useAuthStore.getState();
      if (!refreshToken) return null;
      const r = await axios.post(`${baseURL}/auth/refresh`, { refreshToken });
      const data = r.data as { accessToken: string; refreshToken: string };
      const cur = useAuthStore.getState();
      if (cur.user) {
        useAuthStore.getState().setSession({
          accessToken: data.accessToken,
          refreshToken: data.refreshToken,
          user: cur.user,
        });
      }
      return data.accessToken;
    } catch (err) {
      // Only force a logout when the server explicitly rejects the refresh
      // token (400 / 401 / 403).  Transient network failures or 5xx errors
      // should NOT log the user out — they can retry on their next action.
      if (axios.isAxiosError(err) && err.response?.status != null) {
        const status = err.response.status;
        if (status === 400 || status === 401 || status === 403) {
          useAuthStore.getState().logout();
        }
      }
      return null;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

api.interceptors.response.use(
  (r) => r,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;
    if (error.response?.status === 401 && original && !original._retry) {
      original._retry = true;
      const newToken = await tryRefresh();
      if (newToken) {
        original.headers.set('Authorization', `Bearer ${newToken}`);
        return api.request(original);
      }
    }
    return Promise.reject(error);
  },
);

export interface ApiProblem {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  // backend Result.Failure ProblemDetails extension fields
  code?: string;
  message?: string;
}

export function extractError(err: unknown, fallback = 'خطای ناشناخته‌ای رخ داد'): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as ApiProblem | undefined;
    if (data) {
      if (data.message) return data.message;
      if (data.detail) return data.detail;
      if (data.title) return data.title;
      if (data.errors) return Object.values(data.errors).flat().join('، ');
    }
    if (err.message) return err.message;
  } else if (err instanceof Error) {
    return err.message;
  }
  return fallback;
}
