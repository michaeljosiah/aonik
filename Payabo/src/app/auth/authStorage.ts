const authTokenStorageKey = "payabo.accessToken";
const authUserStorageKey = "payabo.authUser";
const authPkceStorageKey = "payabo.pkceTransaction";
const pkceTransactionTtlMs = 15 * 60 * 1000;

export type StoredAuthUser = {
  id: string;
  email: string;
  fullName: string;
};

export type PkceTransaction = {
  codeVerifier: string;
  state: string;
  returnTo: string;
  createdAt: number;
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

export const writePkceTransaction = (transaction: PkceTransaction | null) => {
  try {
    if (!transaction) {
      sessionStorage.removeItem(authPkceStorageKey);
      return;
    }

    sessionStorage.setItem(authPkceStorageKey, JSON.stringify(transaction));
  } catch {
    // ignore
  }
};

export const readPkceTransaction = (): PkceTransaction | null => {
  try {
    const raw = sessionStorage.getItem(authPkceStorageKey);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as Partial<PkceTransaction>;
    if (
      !parsed
      || typeof parsed.codeVerifier !== "string"
      || typeof parsed.state !== "string"
      || typeof parsed.returnTo !== "string"
      || typeof parsed.createdAt !== "number"
    ) {
      sessionStorage.removeItem(authPkceStorageKey);
      return null;
    }

    if (Date.now() - parsed.createdAt > pkceTransactionTtlMs) {
      sessionStorage.removeItem(authPkceStorageKey);
      return null;
    }

    return {
      codeVerifier: parsed.codeVerifier,
      state: parsed.state,
      returnTo: parsed.returnTo,
      createdAt: parsed.createdAt
    };
  } catch {
    return null;
  }
};

export const clearPkceTransaction = () => writePkceTransaction(null);
