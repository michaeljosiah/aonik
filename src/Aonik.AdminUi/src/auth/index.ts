export { AuthProvider } from './AuthProvider';
export { useAuth, type AuthUser, type AuthContextType } from './useAuth';
export {
  getAuthProvider,
  validateAuthConfig,
  getProviderDisplayName,
  msalConfig,
  msalLoginRequest,
  auth0Config,
  keycloakConfig,
  apiConfig,
  type AuthProvider as AuthProviderType,
  type ConfigValidationResult,
} from './authConfig';
