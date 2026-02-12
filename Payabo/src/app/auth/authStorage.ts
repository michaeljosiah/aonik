const authTokenStorageKey = "payabo.accessToken";

export const writeAccessToken = (token: string | null) => {
  try {
    if (!token) {
      localStorage.removeItem(authTokenStorageKey);
      return;
    }

    localStorage.setItem(authTokenStorageKey, token);
  } catch {
    // ignore
  }
};

export const readAccessToken = (): string | null => {
  try {
    return localStorage.getItem(authTokenStorageKey);
  } catch {
    return null;
  }
};
