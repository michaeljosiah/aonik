import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";

import { getUserInfo, loginWithPassword, registerIndividual } from "../../api/auth";
import {
  clearAccessToken,
  clearStoredAuthUser,
  readAccessToken,
  writeAccessToken,
  writeStoredAuthUser
} from "./authStorage";

type AuthUser = {
  id: string;
  fullName: string;
  email: string;
};

type RegisterPayload = {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  password: string;
  registrationCountry?: string;
};

type AuthContextValue = {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  login: (email: string, password: string) => Promise<void>;
  register: (payload: RegisterPayload) => Promise<void>;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

const buildFullName = (firstName?: string | null, lastName?: string | null) => {
  const composed = [firstName ?? "", lastName ?? ""].join(" ").replace(/\s+/g, " ").trim();
  return composed || "Payabo User";
};

const isAuthFailure = (error: unknown): boolean => {
  if (!error || typeof error !== "object") {
    return false;
  }

  if (!("status" in error)) {
    return false;
  }

  const status = (error as { status?: unknown }).status;
  return status === 401 || status === 403;
};

export const AuthProvider = ({ children }: PropsWithChildren) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [bootstrapRetryCount, setBootstrapRetryCount] = useState(0);

  useEffect(() => {
    let cancelled = false;

    let retryTimer: number | null = null;

    const bootstrap = async () => {
      const accessToken = readAccessToken();
      if (!accessToken) {
        clearStoredAuthUser();
        setUser(null);
        setIsLoading(false);
        return;
      }

      try {
        const info = await getUserInfo();
        if (cancelled) {
          return;
        }

        const resolvedUser = {
          id: info.userId,
          email: info.email,
          fullName: buildFullName(info.firstName, info.lastName)
        };

        setUser(resolvedUser);
        writeStoredAuthUser(resolvedUser);
        setBootstrapRetryCount(0);
        setIsLoading(false);
      } catch (error) {
        if (cancelled) {
          return;
        }

        if (isAuthFailure(error)) {
          clearAccessToken();
          clearStoredAuthUser();
          setUser(null);
          setIsLoading(false);
          return;
        }

        if (bootstrapRetryCount < 3) {
          const delayMs = (bootstrapRetryCount + 1) * 1500;
          retryTimer = window.setTimeout(() => {
            setBootstrapRetryCount((current) => current + 1);
          }, delayMs);
          return;
        }

        setUser(null);
        setIsLoading(false);
      }
    };

    void bootstrap();

    return () => {
      cancelled = true;
      if (retryTimer) {
        window.clearTimeout(retryTimer);
      }
    };
  }, [bootstrapRetryCount]);

  const value = useMemo<AuthContextValue>(() => {
    const login = async (email: string, password: string) => {
      const token = await loginWithPassword({ email, password });
      writeAccessToken(token.accessToken);

      try {
        const info = await getUserInfo();
        const resolvedUser = {
          id: info.userId,
          email: info.email,
          fullName: buildFullName(info.firstName, info.lastName)
        };

        setUser(resolvedUser);
        writeStoredAuthUser(resolvedUser);
        setBootstrapRetryCount(0);
        setIsLoading(false);
      } catch (error) {
        if (isAuthFailure(error)) {
          clearAccessToken();
          clearStoredAuthUser();
          setUser(null);
        }

        throw error;
      }
    };

    const register = async (payload: RegisterPayload) => {
      await registerIndividual(payload);
      await login(payload.email, payload.password);
    };

    const logout = () => {
      clearAccessToken();
      clearStoredAuthUser();
      setUser(null);
    };

    return {
      isAuthenticated: Boolean(user),
      isLoading,
      user,
      login,
      register,
      logout
    };
  }, [isLoading, user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return value;
};
