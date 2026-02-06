// Read tenant id from Vite env variable so it can be configured per-environment.
// Falls back to the previous development GUID if the env var is not provided.
export const PAYABO_TENANT_ID = import.meta.env.VITE_PAYABO_TENANT_ID ?? "550e8400-e29b-41d4-a716-446655440000";
