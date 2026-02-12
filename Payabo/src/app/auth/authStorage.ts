const authTokenStorageKey = "payabo.accessToken";
const authUserStorageKey = "payabo.authUser";

export type StoredAuthUser = {
  id: string;
  email: string;
  fullName: string;
};

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

export const clearAccessToken = () => writeAccessToken(null);

export const writeStoredAuthUser = (user: StoredAuthUser | null) => {
  try {
    if (!user) {
      localStorage.removeItem(authUserStorageKey);
      return;
    }

    localStorage.setItem(authUserStorageKey, JSON.stringify(user));
  } catch {
    // ignore
  }
};

export const readStoredAuthUser = (): StoredAuthUser | null => {
  try {
    const raw = localStorage.getItem(authUserStorageKey);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as Partial<StoredAuthUser>;
    if (!parsed.id || !parsed.email || !parsed.fullName) {
      return null;
    }

    return {
      id: parsed.id,
      email: parsed.email,
      fullName: parsed.fullName
    };
  } catch {
    return null;
  }
};

export const clearStoredAuthUser = () => writeStoredAuthUser(null);
