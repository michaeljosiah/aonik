import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";

import { exchangeAuthorizationCode, getUserInfo, registerIndividual } from "../../api/auth";
import {
  PAYABO_AUTH_AUDIENCE,
  PAYABO_AUTH_CLIENT_ID,
  PAYABO_AUTH_DOMAIN,
  PAYABO_AUTH_SCOPE,
  getPayaboAuthRedirectUri
} from "../../config/auth";
import {
  clearPkceTransaction,
  clearAccessToken,
  clearStoredAuthUser,
  readPkceTransaction,
  readAccessToken,
  writePkceTransaction,
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

type LoginOptions = {
  returnTo?: string;
  loginHint?: string;
  prompt?: "login";
};

type AuthContextValue = {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
  login: (options?: LoginOptions) => Promise<void>;
  completePkceLogin: (authorizationCode: string, state: string) => Promise<string>;
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
  return status === 401;
};

const decodeBase64Url = (value: string): string | null => {
  try {
    const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
    const padded = normalized.padEnd(Math.ceil(normalized.length / 4) * 4, "=");
    return atob(padded);
  } catch {
    return null;
  }
};

const resolveUserFromAccessToken = (accessToken: string): AuthUser | null => {
  const segments = accessToken.split(".");
  if (segments.length < 2) {
    return null;
  }

  const payloadJson = decodeBase64Url(segments[1]);
  if (!payloadJson) {
    return null;
  }

  try {
    const payload = JSON.parse(payloadJson) as Record<string, unknown>;
    const subject = typeof payload.sub === "string" ? payload.sub : null;
    const emailClaim = typeof payload.email === "string" ? payload.email : null;
    const namespacedEmailClaim = typeof payload["https://aonik.app/email"] === "string"
      ? (payload["https://aonik.app/email"] as string)
      : null;
    const email = emailClaim ?? namespacedEmailClaim;

    if (!subject || !email) {
      return null;
    }

    const firstName = typeof payload.given_name === "string" ? payload.given_name : "";
    const lastName = typeof payload.family_name === "string" ? payload.family_name : "";
    const fullNameFromClaims = [firstName, lastName].join(" ").replace(/\s+/g, " ").trim();
    const fullName =
      fullNameFromClaims || (typeof payload.name === "string" && payload.name.trim() ? payload.name.trim() : "Payabo User");

    return {
      id: subject,
      email,
      fullName
    };
  } catch {
    return null;
  }
};

const toBase64Url = (value: Uint8Array): string => {
  let binary = "";
  value.forEach((item) => {
    binary += String.fromCharCode(item);
  });

  return btoa(binary)
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/g, "");
};

const generateRandomString = (length: number): string => {
  const allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
  const random = new Uint8Array(length);
  crypto.getRandomValues(random);

  return Array.from(random, (value) => allowed[value % allowed.length]).join("");
};

const createPkceCodeVerifier = () => generateRandomString(64);
const createPkceState = () => generateRandomString(48);

const createPkceCodeChallenge = async (codeVerifier: string): Promise<string> => {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(codeVerifier));
  return toBase64Url(new Uint8Array(digest));
};

const resolveReturnToPath = (candidate?: string): string => {
  if (!candidate) {
    return "/dashboard";
  }

  return candidate.startsWith("/") ? candidate : "/dashboard";
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

        const fallbackUser = resolveUserFromAccessToken(accessToken);
        if (fallbackUser) {
          setUser(fallbackUser);
          writeStoredAuthUser(fallbackUser);
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
    const hydrateSessionFromAccessToken = async (accessToken: string) => {
      writeAccessToken(accessToken);

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
          throw error;
        }

        const fallbackUser = resolveUserFromAccessToken(accessToken);
        if (fallbackUser) {
          setUser(fallbackUser);
          writeStoredAuthUser(fallbackUser);
          setIsLoading(false);
          return;
        }

        throw error;
      }
    };

    const login = async (options?: LoginOptions) => {
      const codeVerifier = createPkceCodeVerifier();
      const codeChallenge = await createPkceCodeChallenge(codeVerifier);
      const state = createPkceState();
      const returnTo = resolveReturnToPath(options?.returnTo);

      writePkceTransaction({
        codeVerifier,
        state,
        returnTo,
        createdAt: Date.now()
      });

      const authorizeUrl = new URL(`https://${PAYABO_AUTH_DOMAIN}/authorize`);
      authorizeUrl.searchParams.set("response_type", "code");
      authorizeUrl.searchParams.set("client_id", PAYABO_AUTH_CLIENT_ID);
      authorizeUrl.searchParams.set("redirect_uri", getPayaboAuthRedirectUri());
      authorizeUrl.searchParams.set("scope", PAYABO_AUTH_SCOPE);
      authorizeUrl.searchParams.set("audience", PAYABO_AUTH_AUDIENCE);
      authorizeUrl.searchParams.set("code_challenge", codeChallenge);
      authorizeUrl.searchParams.set("code_challenge_method", "S256");
      authorizeUrl.searchParams.set("state", state);

      const loginHint = options?.loginHint?.trim();
      if (loginHint) {
        authorizeUrl.searchParams.set("login_hint", loginHint);
      }

      const prompt = options?.prompt;
      if (prompt) {
        authorizeUrl.searchParams.set("prompt", prompt);
      }

      window.location.assign(authorizeUrl.toString());
    };

    const completePkceLogin = async (authorizationCode: string, state: string): Promise<string> => {
      const transaction = readPkceTransaction();
      if (!transaction) {
        throw new Error("Sign-in session expired. Please try again.");
      }

      if (transaction.state !== state) {
        clearPkceTransaction();
        throw new Error("Invalid sign-in state. Please try again.");
      }

      clearPkceTransaction();

      const token = await exchangeAuthorizationCode({
        clientId: PAYABO_AUTH_CLIENT_ID,
        redirectUri: getPayaboAuthRedirectUri(),
        codeVerifier: transaction.codeVerifier,
        authorizationCode
      });

      await hydrateSessionFromAccessToken(token.accessToken);
      return resolveReturnToPath(transaction.returnTo);
    };

    const register = async (payload: RegisterPayload) => {
      await registerIndividual(payload);
    };

    const logout = () => {
      clearPkceTransaction();
      clearAccessToken();
      clearStoredAuthUser();
      setUser(null);
    };

    return {
      isAuthenticated: Boolean(user),
      isLoading,
      user,
      login,
      completePkceLogin,
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
