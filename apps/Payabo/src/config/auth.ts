const normalizeDomain = (rawDomain: string): string => {
  const trimmed = rawDomain.trim();
  if (!trimmed) {
    return "aonik.uk.auth0.com";
  }

  const withoutScheme = trimmed.replace(/^https?:\/\//i, "");
  return withoutScheme.replace(/\/+$/, "");
};

const authDomain = normalizeDomain(import.meta.env.VITE_PAYABO_AUTH_DOMAIN ?? "aonik.uk.auth0.com");
const authClientId = (import.meta.env.VITE_PAYABO_AUTH_CLIENT_ID ?? "payabo-web").trim();
const authAudience = (import.meta.env.VITE_PAYABO_AUTH_AUDIENCE ?? "https://api.aonik.com").trim();
const authScope = (import.meta.env.VITE_PAYABO_AUTH_SCOPE ?? "openid profile email").trim();

export const PAYABO_AUTH_DOMAIN = authDomain;
export const PAYABO_AUTH_CLIENT_ID = authClientId || "payabo-web";
export const PAYABO_AUTH_AUDIENCE = authAudience || "https://api.aonik.com";
export const PAYABO_AUTH_SCOPE = authScope || "openid profile email";

export const getPayaboAuthRedirectUri = () => `${window.location.origin}/auth/callback`;
