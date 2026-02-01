import { useEffect, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { WorkspacePanelRenderProps, WorkspaceEvent } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

export function ReconciliationHubPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { onEvent } = useWorkspaceEvents(panelId);
  const [linkedInvoice, setLinkedInvoice] = useState<string | null>(null);
  const [linkedCustomer, setLinkedCustomer] = useState<string | null>(null);

  useEffect(() => {
    return onEvent('invoice:selected', (event: WorkspaceEvent) => {
      const invoiceId = event.payload?.invoiceId as string | undefined;
      const customer = event.payload?.customer as string | undefined;
      if (invoiceId) {
        setLinkedInvoice(invoiceId);
        setLinkedCustomer(customer ?? null);
      }
    });
  }, [onEvent]);

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-sm text-[var(--color-text-secondary)]">
            Streamline matching across ledger, payment, and partner feeds.
          </p>
        </div>
        <Button variant="secondary" size="sm">
          Run Match
        </Button>
      </div>

      <Card className="p-4 flex items-start justify-between gap-4">
        <div>
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Linked from workspace</p>
          <p className="text-base font-semibold text-[var(--color-text-primary)]">
            {linkedInvoice ?? 'Select an invoice in Invoice Manager'}
          </p>
          <p className="text-xs text-[var(--color-text-secondary)]">
            {linkedCustomer ?? 'Waiting for a shared selection'}
          </p>
        </div>
        <Badge variant={linkedInvoice ? 'success' : 'outline'}>
          {linkedInvoice ? 'In focus' : 'Idle'}
        </Badge>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Matches today</p>
          <p className="text-2xl font-semibold text-[var(--color-text-primary)]">1,248</p>
          <p className="text-xs text-[var(--color-text-secondary)]">97% auto-match rate</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Open exceptions</p>
          <p className="text-2xl font-semibold text-[var(--color-text-primary)]">42</p>
          <p className="text-xs text-[var(--color-text-secondary)]">8 need manual review</p>
        </Card>
      </div>
    </div>
  );
}
