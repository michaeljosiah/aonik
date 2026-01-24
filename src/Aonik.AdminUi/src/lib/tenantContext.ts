export interface SelectedTenant {
  tenantId: string;
  name?: string;
  subdomain?: string | null;
  environment?: string;
}

const SelectedTenantKey = 'selected_tenant';
const SelectedTenantIdKey = 'selected_tenant_id';

function getFromStorage(storage: Storage, key: string): string | null {
  try {
    return storage.getItem(key);
  } catch {
    return null;
  }
}

function setInStorage(storage: Storage, key: string, value: string): void {
  try {
    storage.setItem(key, value);
  } catch {
    // ignore
  }
}

function removeFromStorage(storage: Storage, key: string): void {
  try {
    storage.removeItem(key);
  } catch {
    // ignore
  }
}

export function getSelectedTenant(): SelectedTenant | null {
  try {
    const raw =
      getFromStorage(localStorage, SelectedTenantKey) ??
      getFromStorage(sessionStorage, SelectedTenantKey);
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<SelectedTenant> | null;
      if (parsed && typeof parsed.tenantId === 'string' && parsed.tenantId.trim().length > 0) {
        // Migrate older session-only selection into localStorage for persistence across reloads/new tabs.
        if (!getFromStorage(localStorage, SelectedTenantKey)) {
          setInStorage(localStorage, SelectedTenantKey, raw);
        }
        return { tenantId: parsed.tenantId, name: parsed.name, subdomain: parsed.subdomain, environment: parsed.environment };
      }
    }
  } catch {
    // ignore
  }

  const tenantId =
    getFromStorage(localStorage, SelectedTenantIdKey) ??
    getFromStorage(sessionStorage, SelectedTenantIdKey);
  if (tenantId && tenantId.trim().length > 0) {
    if (!getFromStorage(localStorage, SelectedTenantIdKey)) {
      setInStorage(localStorage, SelectedTenantIdKey, tenantId);
    }
    return { tenantId };
  }

  return null;
}

export function setSelectedTenant(tenant: SelectedTenant): void {
  const raw = JSON.stringify(tenant);
  setInStorage(localStorage, SelectedTenantIdKey, tenant.tenantId);
  setInStorage(localStorage, SelectedTenantKey, raw);

  // Keep sessionStorage in sync for same-tab flows.
  setInStorage(sessionStorage, SelectedTenantIdKey, tenant.tenantId);
  setInStorage(sessionStorage, SelectedTenantKey, raw);
}

export function clearSelectedTenant(): void {
  removeFromStorage(localStorage, SelectedTenantIdKey);
  removeFromStorage(localStorage, SelectedTenantKey);
  removeFromStorage(sessionStorage, SelectedTenantIdKey);
  removeFromStorage(sessionStorage, SelectedTenantKey);
}
