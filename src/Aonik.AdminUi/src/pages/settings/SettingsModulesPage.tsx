import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { TenantModulesPanel } from '@/components/modules/TenantModulesPanel';
import { getSelectedTenant } from '@/lib/tenantContext';

/**
 * Tenant-facing, read-only view of module enablement (Spec 097 §10).
 * Only host administrators change module state, from the tenant's detail
 * page under Host.
 */
export function SettingsModulesPage() {
  const tenant = getSelectedTenant();

  return (
    <div className="h-full overflow-auto p-6">
      <div className="mb-6">
        <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.1em] text-[var(--color-text-tertiary)]">Admin</p>
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)]">Modules</h1>
        <p className="max-w-3xl text-[var(--color-text-secondary)]">
          The platform modules available to this organisation. Module state is managed by the host administrator.
        </p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Module enablement</CardTitle>
          <CardDescription>
            Disabled modules are hidden from navigation and their features are unavailable until a host administrator enables them.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {tenant?.tenantId ? (
            <TenantModulesPanel tenantId={tenant.tenantId} readOnly />
          ) : (
            <p className="text-sm text-[var(--color-text-secondary)]">
              No organisation is selected. Choose an organisation to see its modules.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
