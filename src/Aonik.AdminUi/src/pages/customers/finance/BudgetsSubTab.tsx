import { useCallback, useEffect, useState } from 'react';
import { BarChart2, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
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
            <p className="text-sm font-semibold text-foreground">
              {formatPeriod(budget.periodStart, budget.periodType)}
            </p>
            <p className="text-xs text-muted-foreground">{budget.periodType} budget</p>
          </div>
          <Badge variant={isActive ? 'success' : 'secondary'}>{budget.status}</Badge>
        </div>

        {budget.lines.length === 0 ? (
          <p className="text-xs text-muted-foreground">No budget lines</p>
        ) : (
          <div className="space-y-2">
            {budget.lines.map((line, idx) => (
              <div
                key={idx}
                className="flex items-center justify-between py-1 border-b border-border last:border-0"
              >
                <span className="text-xs text-muted-foreground">{line.category}</span>
                <span className="font-mono text-xs font-medium tabular-nums text-foreground">
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
        <p className="text-sm font-medium text-foreground">
          <span className="font-mono tabular-nums">{budgets.length}</span> budget period{budgets.length !== 1 ? 's' : ''}
        </p>
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon-sm"
              onClick={load}
              disabled={loading}
              aria-label="Refresh budgets"
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
      ) : budgets.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-muted">
            <BarChart2 className="h-7 w-7 text-muted-foreground" />
          </div>
          <p className="text-sm font-medium text-muted-foreground">No budgets yet</p>
          <p className="text-xs text-muted-foreground mt-0.5">
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
