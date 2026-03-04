const tenantId = (import.meta.env.VITE_PAYABO_TENANT_ID ?? "").trim();
const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

if (!guidPattern.test(tenantId)) {
  throw new Error(
    "Invalid or missing VITE_PAYABO_TENANT_ID. Set a valid tenant GUID in apps/Payabo/.env and restart the Vite server."
  );
}

export const PAYABO_TENANT_ID = tenantId;
