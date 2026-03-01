import { apiGet, apiPost } from "./client";
import { PAYABO_TENANT_ID } from "../config/tenant";

type TokenResponse = {
  accessToken: string;
  refreshToken: string | null;
  expiresIn: number;
  tokenType: string;
  idToken: string | null;
};

export type AuthUserInfo = {
  userId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
};

type RegistrationResponse = {
  userId: string;
  partyId: string;
};

export const loginWithPassword = async (request: { email: string; password: string }): Promise<TokenResponse> => {
  return await apiPost<TokenResponse>("/auth/token", {
    grantType: "password",
    clientId: "payabo-web",
    username: request.email,
    password: request.password,
    scope: "openid profile email"
  });
};

export const exchangeAuthorizationCode = async (request: {
  clientId: string;
  redirectUri: string;
  codeVerifier: string;
  authorizationCode: string;
}): Promise<TokenResponse> => {
  return await apiPost<TokenResponse>("/auth/token", {
    grantType: "authorization_code",
    clientId: request.clientId,
    redirectUri: request.redirectUri,
    codeVerifier: request.codeVerifier,
    authorizationCode: request.authorizationCode,
    scope: "openid profile email"
  });
};

export const registerIndividual = async (request: {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  password: string;
  registrationCountry?: string;
}): Promise<RegistrationResponse> => {
  return await apiPost<RegistrationResponse>("/v1/registrations/individual", {
    tenantId: PAYABO_TENANT_ID,
    registrationCountry: request.registrationCountry ?? null,
    title: null,
    firstName: request.firstName,
    lastName: request.lastName,
    email: request.email,
    phone: request.phone ?? null,
    password: request.password
  });
};

export const getUserInfo = async (): Promise<AuthUserInfo> => {
  return await apiGet<AuthUserInfo>("/identity/userinfo");
};
