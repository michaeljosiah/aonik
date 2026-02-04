import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";

type AuthUser = {
  id: string;
  fullName: string;
  email: string;
};

type AuthContextValue = {
  isAuthenticated: boolean;
  user: AuthUser | null;
  login: (email: string, fullName?: string) => void;
  register: (fullName: string, email: string) => void;
  logout: () => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

const authStorageKey = "payabo.mockAuth";

const readAuthFromStorage = (): AuthUser | null => {
  try {
    const raw = window.localStorage.getItem(authStorageKey);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as unknown;
    if (!parsed || typeof parsed !== "object") return null;
    const user = parsed as Partial<AuthUser>;
    if (!user.id || !user.email || !user.fullName) return null;
    return { id: user.id, email: user.email, fullName: user.fullName };
  } catch {
    return null;
  }
};

const writeAuthToStorage = (user: AuthUser | null) => {
  try {
    if (!user) {
      window.localStorage.removeItem(authStorageKey);
      return;
    }

    window.localStorage.setItem(authStorageKey, JSON.stringify(user));
  } catch {
    // ignore
  }
};

export const AuthProvider = ({ children }: PropsWithChildren) => {
  const [user, setUser] = useState<AuthUser | null>(() => {
    if (typeof window === "undefined") return null;
    return readAuthFromStorage();
  });

  useEffect(() => {
    writeAuthToStorage(user);
  }, [user]);

  const value = useMemo<AuthContextValue>(() => {
    const login = (email: string, fullName?: string) => {
      setUser({
        id: crypto.randomUUID(),
        email,
        fullName: fullName?.trim() ? fullName.trim() : "John Doe"
      });
    };

    const register = (fullName: string, email: string) => {
      setUser({
        id: crypto.randomUUID(),
        email,
        fullName: fullName.trim() || "John Doe"
      });
    };

    const logout = () => setUser(null);

    return {
      isAuthenticated: Boolean(user),
      user,
      login,
      register,
      logout
    };
  }, [user]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return value;
};
