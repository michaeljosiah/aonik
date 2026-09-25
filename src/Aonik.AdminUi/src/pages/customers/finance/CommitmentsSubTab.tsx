import { useCallback, useEffect, useState } from 'react';
import { CalendarClock, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
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

const STATUS_VARIANTS: Record<string, 'success' | 'warning' | 'secondary'> = {
  Active: 'success',
  Paused: 'warning',
  Cancelled: 'secondary',
};

/* -------------------------------------------------------------------------- */
/*  Commitment Row                                                             */
/* -------------------------------------------------------------------------- */

function CommitmentRow({ item }: { item: CommitmentItem }) {
  const isDueSoon =
    item.status === 'Active' && new Date(item.dueDate) <= new Date(Date.now() + 7 * 86400_000);

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold text-foreground truncate">
              {item.displayName}
            </p>
            <p className="text-xs text-muted-foreground mt-0.5">
              {TYPE_LABELS[item.commitmentType] ?? item.commitmentType}
              {item.frequency ? ` · ${item.frequency}` : ''}
              {item.category ? ` · ${item.category}` : ''}
            </p>
          </div>

          <div className="flex flex-col items-end gap-1.5 shrink-0">
            <p className="font-mono text-sm font-bold tabular-nums text-foreground">
              {formatCurrency(item.amount, item.currency)}
            </p>
            <Badge variant={STATUS_VARIANTS[item.status] ?? 'secondary'}>{item.status}</Badge>
          </div>
        </div>

        <div className="mt-3 flex items-center gap-3 text-xs text-muted-foreground">
          <span
            className={isDueSoon ? 'font-medium text-warning' : ''}
          >
            Due {formatDate(item.dueDate)}
            {isDueSoon ? ' — soon' : ''}
          </span>
          {item.autopay && (
            <span className="text-primary">Autopay</span>
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
          <p className="text-sm font-medium text-foreground">
            <span className="font-mono tabular-nums">{items.length}</span> commitment{items.length !== 1 ? 's' : ''}
          </p>
          {totals && totals.totalUpcomingAmount > 0 && (
            <p className="text-xs text-muted-foreground">
              {totals.dueSoonCount > 0 ? `${totals.dueSoonCount} due within 7 days` : 'No upcoming payments'}
            </p>
          )}
        </div>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={load}
              disabled={loading}
              aria-label="Refresh commitments"
            >
              <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
            </Button>
          </TooltipTrigger>
          <TooltipContent>Refresh</TooltipContent>
        </Tooltip>
      </div>

      {/* Error */}
      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Loading */}
      {loading ? (
        <div className="flex items-center justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        </div>
      ) : items.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-muted">
            <CalendarClock className="h-7 w-7 text-muted-foreground" />
          </div>
          <p className="text-sm font-medium text-muted-foreground">No commitments</p>
          <p className="text-xs text-muted-foreground mt-0.5">
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
