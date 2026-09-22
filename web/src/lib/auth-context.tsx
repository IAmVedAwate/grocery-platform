"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { apiFetch, apiFetchBlob, ApiError } from "./api-client";

type AuthState = {
  accessToken: string | null;
  isLoading: boolean;
  login: (storeSlug: string, email: string, password: string) => Promise<void>;
  registerStore: (input: {
    storeName: string;
    slug: string;
    adminEmail: string;
    adminPassword: string;
    adminDisplayName: string;
  }) => Promise<void>;
  logout: () => void;
  authFetch: <T>(path: string, options?: RequestInit) => Promise<T>;
  authFetchBlob: (path: string) => Promise<Blob>;
};

const AuthContext = createContext<AuthState | null>(null);

type AccessTokenResponse = { accessToken: string };

export function AuthProvider({ children }: { children: ReactNode }) {
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  // Access token is held in memory only (never localStorage) per
  // docs/architecture/authentication-flow.md. A ref mirrors state so
  // authFetch's retry-after-refresh path always reads the latest token
  // without depending on a stale closure.
  const tokenRef = useRef<string | null>(null);

  const setToken = useCallback((token: string | null) => {
    tokenRef.current = token;
    setAccessToken(token);
  }, []);

  // On first load (e.g. after a page refresh), try to silently restore a
  // session from the httpOnly refresh cookie, if one is still valid.
  useEffect(() => {
    apiFetch<AccessTokenResponse>("/api/v1/auth/refresh", null, { method: "POST" })
      .then((res) => setToken(res.accessToken))
      .catch(() => setToken(null))
      .finally(() => setIsLoading(false));
  }, [setToken]);

  const login = useCallback(
    async (storeSlug: string, email: string, password: string) => {
      const res = await apiFetch<AccessTokenResponse>("/api/v1/auth/login", null, {
        method: "POST",
        body: JSON.stringify({ storeSlug, email, password }),
      });
      setToken(res.accessToken);
    },
    [setToken],
  );

  const registerStore = useCallback(
    async (input: {
      storeName: string;
      slug: string;
      adminEmail: string;
      adminPassword: string;
      adminDisplayName: string;
    }) => {
      await apiFetch("/api/v1/auth/register-store", null, {
        method: "POST",
        body: JSON.stringify(input),
      });
      await login(input.slug, input.adminEmail, input.adminPassword);
    },
    [login],
  );

  const logout = useCallback(() => setToken(null), [setToken]);

  const authFetch = useCallback(
    async <T,>(path: string, options: RequestInit = {}): Promise<T> => {
      try {
        return await apiFetch<T>(path, tokenRef.current, options);
      } catch (error) {
        // A single silent-refresh-and-retry on 401 — covers the common
        // case of an access token expiring mid-session.
        if (error instanceof ApiError && error.status === 401) {
          const refreshed = await apiFetch<AccessTokenResponse>(
            "/api/v1/auth/refresh",
            null,
            { method: "POST" },
          ).catch(() => null);
          if (refreshed) {
            setToken(refreshed.accessToken);
            return apiFetch<T>(path, refreshed.accessToken, options);
          }
          setToken(null);
        }
        throw error;
      }
    },
    [setToken],
  );

  const authFetchBlob = useCallback(
    async (path: string): Promise<Blob> => {
      try {
        return await apiFetchBlob(path, tokenRef.current);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          const refreshed = await apiFetch<AccessTokenResponse>(
            "/api/v1/auth/refresh",
            null,
            { method: "POST" },
          ).catch(() => null);
          if (refreshed) {
            setToken(refreshed.accessToken);
            return apiFetchBlob(path, refreshed.accessToken);
          }
          setToken(null);
        }
        throw error;
      }
    },
    [setToken],
  );

  const value = useMemo(
    () => ({ accessToken, isLoading, login, registerStore, logout, authFetch, authFetchBlob }),
    [accessToken, isLoading, login, registerStore, logout, authFetch, authFetchBlob],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (!context) throw new Error("useAuth must be used within an AuthProvider");
  return context;
}
