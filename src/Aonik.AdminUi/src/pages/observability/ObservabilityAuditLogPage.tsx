import { Activity } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Card, CardContent } from '@/components/ui/card';

export function ObservabilityAuditLogPage() {
  return (
    <div className="flex h-full flex-col overflow-auto">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
          <Breadcrumb
            items={[
              { label: 'Admin' },
              { label: 'Observability', href: '/admin/observability', icon: <Activity className="h-4 w-4" /> },
              { label: 'Audit Log' },
            ]}
            className="mb-3"
          />
          <h1 className="text-xl font-semibold text-[var(--color-text-primary)]">Audit Log</h1>
          <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
            Route scaffold is in place. This screen will get its own observability-focused audit port instead of reusing the Settings audit view.
          </p>
        </div>
      </div>

      <div className="flex-1 p-6">
        <Card>
          <CardContent className="p-6 text-sm text-[var(--color-text-secondary)]">
            The existing settings audit experience remains available at <code>/settings/audit-logs</code> while the template-based observability audit page is being built.
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
