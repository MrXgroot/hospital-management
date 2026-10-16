import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type { AuthResponse } from '../types/api';

const SESSION_KEY = 'hms.session';
export const LOGOUT_EVENT = 'hms:logout';

/** Only auth tokens and the user's own profile are stored; never patient data. */
export const session = {
  get(): AuthResponse | null {
    try {
      const raw = localStorage.getItem(SESSION_KEY);
      return raw ? (JSON.parse(raw) as AuthResponse) : null;
    } catch {
      return null;
    }
  },
  set(value: AuthResponse) {
    try {
      localStorage.setItem(SESSION_KEY, JSON.stringify(value));
    } catch {
      // Storage unavailable (private mode): the session lasts for this page only.
    }
  },
  clear() {
    try {
      localStorage.removeItem(SESSION_KEY);
    } catch {
      // ignore
    }
  },
};

const baseURL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export const api = axios.create({ baseURL });

api.interceptors.request.use((config) => {
  const current = session.get();
  if (current) config.headers.Authorization = `Bearer ${current.accessToken}`;
  return config;
});

let refreshInFlight: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const current = session.get();
  if (!current) return null;
  try {
    const { data } = await axios.post<AuthResponse>(`${baseURL}/auth/refresh`, { refreshToken: current.refreshToken });
    session.set(data);
    return data.accessToken;
  } catch {
    return null;
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;
    if (error.response?.status === 401 && original && !original._retried && !original.url?.startsWith('/auth/')) {
      original._retried = true;
      refreshInFlight ??= refreshAccessToken().finally(() => {
        refreshInFlight = null;
      });
      const token = await refreshInFlight;
      if (token) {
        original.headers.Authorization = `Bearer ${token}`;
        return api(original);
      }
      session.clear();
      window.dispatchEvent(new Event(LOGOUT_EVENT));
    }
    return Promise.reject(error);
  },
);
