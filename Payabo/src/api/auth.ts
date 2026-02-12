import { apiGet, apiPost } from "./client";

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

export const loginWithPassword = async (request: { email: string; password: string }): Promise<TokenResponse> => {
  return await apiPost<TokenResponse>("/auth/token", {
    grantType: "password",
    clientId: "payabo-web",
    username: request.email,
    password: request.password,
    scope: "openid profile email"
  });
};

export const getUserInfo = async (): Promise<AuthUserInfo> => {
  return await apiGet<AuthUserInfo>("/identity/userinfo");
};
