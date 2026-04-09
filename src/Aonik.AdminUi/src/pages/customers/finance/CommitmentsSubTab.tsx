import { useCallback, useEffect, useState } from 'react';
import { CalendarClock, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { personalFinanceService } from '@/services/personalFinanceService';
import type { CommitmentItem, CommitmentListResponse } from '@/types';

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

function formatCurrency(amount: number | null | undefined, currency: string): string {
  if (amount == null) return `— ${currency}`;
  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency,
      maximumFractionDigits: 2,
    }).format(amount);
  } catch {
    return `${amount.toLocaleString()} ${currency}`;
  }
}

function formatDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

const TYPE_LABELS: Record<string, string> = {
  Bill: 'Bill',
  PersonalRecurringBill: 'Recurring Bill',
  Subscription: 'Subscription',
  DebtRepayment: 'Debt Repayment',
};

const STATUS_CONFIG: Record<string, { bg: string; text: string }> = {
  Active: { bg: 'bg-[var(--color-success-light)]', text: 'text-[var(--color-success)]' },
  Paused: { bg: 'bg-[var(--color-warning-light)]', text: 'text-[var(--color-warning)]' },
  Cancelled: { bg: 'bg-[var(--color-surface-inset)]', text: 'text-[var(--color-text-tertiary)]' },
};

/* -------------------------------------------------------------------------- */
/*  Commitment Row                                                             */
/* -------------------------------------------------------------------------- */

function CommitmentRow({ item }: { item: CommitmentItem }) {
  const status = STATUS_CONFIG[item.status] ?? {
    bg: 'bg-[var(--color-surface-inset)]',
    text: 'text-[var(--color-text-secondary)]',
  };
  const isDueSoon =
    item.status === 'Active' && new Date(item.dueDate) <= new Date(Date.now() + 7 * 86400_000);

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-[var(--color-text-primary)] truncate">
              {item.displayName}
            </p>
            <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
              {TYPE_LABELS[item.commitmentType] ?? item.commitmentType}
              {item.frequency ? ` · ${item.frequency}` : ''}
              {item.category ? ` · ${item.category}` : ''}
            </p>
          </div>

          <div className="flex flex-col items-end gap-1.5 shrink-0">
            <p className="text-sm font-bold text-[var(--color-text-primary)]">
              {formatCurrency(item.amount, item.currency)}
            </p>
            <Badge className={`rounded-full text-xs ${status.bg} ${status.text}`}>
              {item.status}
            </Badge>
          </div>
        </div>

        <div className="mt-3 flex items-center gap-3 text-xs text-[var(--color-text-tertiary)]">
          <span
            className={isDueSoon ? 'font-medium text-[var(--color-warning)]' : ''}
          >
            Due {formatDate(item.dueDate)}
            {isDueSoon ? ' — soon' : ''}
          </span>
          {item.autopay && (
            <span className="text-[var(--color-brand-primary)]">Autopay</span>
          )}
          {item.lastPaidAt && (
            <span>Last paid {formatDate(item.lastPaidAt)}</span>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/*  Main Component                                                             */
/* -------------------------------------------------------------------------- */

export function CommitmentsSubTab({ userId }: { userId: string }) {
  const [data, setData] = useState<CommitmentListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await personalFinanceService.admin.listCommitments(userId, { pageSize: 100 });
      setData(result);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load commitments.');
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    load();
  }, [load]);

  const items = data?.items ?? [];
  const totals = data?.totals;

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-medium text-[var(--color-text-primary)]">
            {items.length} commitment{items.length !== 1 ? 's' : ''}
          </p>
          {totals && totals.totalUpcomingAmount > 0 && (
            <p className="text-xs text-[var(--color-text-tertiary)]">
              {totals.dueSoonCount > 0 ? `${totals.dueSoonCount} due within 7 days` : 'No upcoming payments'}
            </p>
          )}
        </div>
        <Button variant="ghost" size="icon-sm" onClick={load} disabled={loading} title="Refresh">
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Error */}
      {error && (
        <div className="rounded-md border border-[var(--color-error)] bg-[var(--color-error-light)] px-4 py-3 text-sm text-[var(--color-error)]">
          {error}
        </div>
      )}

      {/* Loading */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-[var(--color-brand-primary)] border-t-transparent" />
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--color-surface-inset)]">
            <CalendarClock className="h-7 w-7 text-[var(--color-text-tertiary)]" />
          </div>
          <p className="text-sm font-medium text-[var(--color-text-secondary)]">No commitments</p>
          <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
            No bills, subscriptions, or recurring commitments found.
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {items.map((item) => (
            <CommitmentRow key={item.commitmentId} item={item} />
          ))}
        </div>
      )}
    </div>
  );
}
