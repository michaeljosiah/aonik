import { Activity } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Card, CardContent } from '@/components/ui/card';

export function ObservabilityLogsPage() {
  return (
    <div className="flex h-full flex-col overflow-auto">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
          <Breadcrumb
            items={[
              { label: 'Admin' },
              { label: 'Observability', href: '/admin/observability', icon: <Activity className="h-4 w-4" /> },
              { label: 'Logs' },
            ]}
            className="mb-3"
          />
          <h1 className="text-xl font-semibold text-[var(--color-text-primary)]">Logs</h1>
          <p className="mt-1 text-sm text-[var(--color-text-secondary)]">
            Route scaffold is in place. This screen will be ported from the structured logs template after traces.
          </p>
        </div>
      </div>

      <div className="flex-1 p-6">
        <Card>
          <CardContent className="p-6 text-sm text-[var(--color-text-secondary)]">
            The tabbed observability shell has been removed. Logs will live here as a first-class observability page.
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
