import { useMemo } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import type { WorkspacePanelRenderProps } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

interface InvoiceSummary {
  id: string;
  customer: string;
  amount: string;
  status: 'Paid' | 'Pending' | 'Overdue';
}

export function InvoiceManagerPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { emit } = useWorkspaceEvents(panelId);
  const invoices = useMemo<InvoiceSummary[]>(
    () => [
      { id: 'INV-3921', customer: 'Sable Labs', amount: '$24,200', status: 'Pending' },
      { id: 'INV-3915', customer: 'Mango Grove', amount: '$8,450', status: 'Paid' },
      { id: 'INV-3909', customer: 'Nova Freight', amount: '$12,120', status: 'Overdue' },
    ],
    []
  );

  const handleSelect = (invoice: InvoiceSummary) => {
    emit({
      type: 'invoice:selected',
      payload: { invoiceId: invoice.id, customer: invoice.customer },
    });
  };

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-start justify-between">
        <div>
          <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
          <p className="text-sm text-[var(--color-text-secondary)]">
            Monitor invoice health and share context with other workspace panels.
          </p>
        </div>
        <Button variant="secondary" size="sm">
          New Invoice
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Outstanding</p>
          <p className="text-xl font-semibold text-[var(--color-text-primary)]">$148,920</p>
          <p className="text-xs text-[var(--color-text-secondary)]">Across 18 invoices</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">At Risk</p>
          <p className="text-xl font-semibold text-[var(--color-text-primary)]">$32,480</p>
          <p className="text-xs text-[var(--color-text-secondary)]">3 invoices overdue</p>
        </Card>
        <Card className="p-4">
          <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Next Action</p>
          <p className="text-sm font-medium text-[var(--color-text-primary)]">Dunning run</p>
          <p className="text-xs text-[var(--color-text-secondary)]">Scheduled for 2:00 PM</p>
        </Card>
      </div>

      <div className="space-y-2">
        <p className="text-xs text-[var(--color-text-tertiary)] uppercase tracking-wide">Recent invoices</p>
        <div className="space-y-2">
          {invoices.map((invoice) => (
            <button
              key={invoice.id}
              type="button"
              onClick={() => handleSelect(invoice)}
              className="w-full text-left"
            >
              <Card className="p-3 flex items-center justify-between hover:shadow-sm transition-shadow">
                <div>
                  <p className="text-sm font-semibold text-[var(--color-text-primary)]">{invoice.id}</p>
                  <p className="text-xs text-[var(--color-text-secondary)]">{invoice.customer}</p>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-[var(--color-text-primary)]">{invoice.amount}</p>
                  <Badge variant={invoice.status === 'Paid' ? 'success' : invoice.status === 'Overdue' ? 'error' : 'pending'}>
                    {invoice.status}
                  </Badge>
                </div>
              </Card>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
