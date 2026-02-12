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

export const AuthProvider = ({ children }: PropsWithChildren) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    let cancelled = false;

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
      } catch {
        if (!cancelled) {
          clearAccessToken();
          clearStoredAuthUser();
          setUser(null);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void bootstrap();

    return () => {
      cancelled = true;
    };
  }, []);

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
      } catch (error) {
        clearAccessToken();
        clearStoredAuthUser();
        setUser(null);
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
