import { useCallback, useEffect, useState } from 'react';
import { BarChart2, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { personalFinanceService } from '@/services/personalFinanceService';
import type { AdminBudgetResponse } from '@/types';

/* -------------------------------------------------------------------------- */
/*  Helpers                                                                    */
/* -------------------------------------------------------------------------- */

function formatCurrency(amount: number, currency: string): string {
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

function formatPeriod(isoDate: string, periodType: string): string {
  const date = new Date(isoDate);
  if (periodType === 'Monthly') {
    return date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
  }
  return date.toLocaleDateString('en-US', { dateStyle: 'medium' });
}

/* -------------------------------------------------------------------------- */
/*  Budget Card                                                                */
/* -------------------------------------------------------------------------- */

function BudgetCard({ budget }: { budget: AdminBudgetResponse }) {
  const isActive = budget.status === 'Active';

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-center justify-between mb-3">
          <div>
            <p className="text-sm font-semibold text-[var(--color-text-primary)]">
              {formatPeriod(budget.periodStart, budget.periodType)}
            </p>
            <p className="text-xs text-[var(--color-text-tertiary)]">{budget.periodType} budget</p>
          </div>
          <span
            className={`text-xs px-2 py-0.5 rounded-full font-medium ${
              isActive
                ? 'bg-[var(--color-success-light)] text-[var(--color-success)]'
                : 'bg-[var(--color-surface-inset)] text-[var(--color-text-tertiary)]'
            }`}
          >
            {budget.status}
          </span>
        </div>

        {budget.lines.length === 0 ? (
          <p className="text-xs text-[var(--color-text-tertiary)]">No budget lines</p>
        ) : (
          <div className="space-y-2">
            {budget.lines.map((line, idx) => (
              <div
                key={idx}
                className="flex items-center justify-between py-1 border-b border-[var(--color-border-light)] last:border-0"
              >
                <span className="text-xs text-[var(--color-text-secondary)]">{line.category}</span>
                <span className="text-xs font-medium text-[var(--color-text-primary)]">
                  {formatCurrency(line.limitAmount, line.currency)}
                </span>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/* -------------------------------------------------------------------------- */
/*  Main Component                                                             */
/* -------------------------------------------------------------------------- */

export function BudgetsSubTab({ userId }: { userId: string }) {
  const [budgets, setBudgets] = useState<AdminBudgetResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await personalFinanceService.admin.listBudgets(userId);
      setBudgets(data);
    } catch (err: unknown) {
      const message =
        err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
      setError(message || 'Failed to load budgets.');
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <p className="text-sm font-medium text-[var(--color-text-primary)]">
          {budgets.length} budget period{budgets.length !== 1 ? 's' : ''}
        </p>
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
      ) : budgets.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--color-surface-inset)]">
            <BarChart2 className="h-7 w-7 text-[var(--color-text-tertiary)]" />
          </div>
          <p className="text-sm font-medium text-[var(--color-text-secondary)]">No budgets yet</p>
          <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
            This customer has not set up any budgets.
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {budgets.map((b) => (
            <BudgetCard key={b.budgetId} budget={b} />
          ))}
        </div>
      )}
    </div>
  );
}
