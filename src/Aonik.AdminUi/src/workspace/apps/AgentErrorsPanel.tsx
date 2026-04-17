import { useState, useEffect, useCallback } from 'react';
import { RefreshCw, AlertTriangle, XCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  PanelInfoPopover,
  type PanelCallout,
} from '@/components/ui/panel-info-popover';
import { MetricCard } from '@/components/charts/MetricCard';
import { TimeSeriesChart } from '@/components/charts/TimeSeriesChart';
import {
  observabilityService,
  type ObservabilityOverviewResponse,
  type ErrorsResponse,
} from '@/services/observabilityService';
import type { WorkspacePanelRenderProps } from '../types';
import { useWorkspaceEvents } from '../useWorkspace';

function formatTimestamp(ts: string): string {
  const date = new Date(ts);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60_000);
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr}h ago`;
  return `${Math.floor(diffHr / 24)}d ago`;
}

export function AgentErrorsPanel({ panelId, title }: WorkspacePanelRenderProps) {
  const { onEvent } = useWorkspaceEvents(panelId);
  const [overview, setOverview] = useState<ObservabilityOverviewResponse | null>(null);
  const [errors, setErrors] = useState<ErrorsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [, setSelectedAgent] = useState<string | null>(null);
  const [timeRange] = useState('24h');

  const load = useCallback(async () => {
    try {
      const [overviewRes, errorsRes] = await Promise.all([
        observabilityService.getOverview(timeRange),
        observabilityService.getErrors(timeRange),
      ]);
      setOverview(overviewRes);
      setErrors(errorsRes);
    } catch {
      /* swallow */
    } finally {
      setLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    void load();
    const interval = setInterval(() => void load(), 30_000);
    return () => clearInterval(interval);
  }, [load]);

  useEffect(() => {
    const unsub = onEvent('agent:selected', (event) => {
      setSelectedAgent((event.payload?.agentName as string) ?? null);
    });
    return unsub;
  }, [onEvent]);

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      await load();
    } finally {
      setRefreshing(false);
    }
  };

  if (loading && !overview) {
    return (
      <div className="h-full overflow-auto p-4">
        <p className="text-sm text-[var(--color-text-tertiary)] py-4 text-center">
          Loading error data...
        </p>
      </div>
    );
  }

  const errorMetrics = overview?.errors;
  const errorGroups = errors?.errors ?? [];

  const callouts: PanelCallout[] = [];
  if (errorMetrics) {
    if (errorMetrics.total === 0) {
      callouts.push({ level: 'good', message: 'No errors in this window — healthy.' });
    } else {
      if (errorMetrics.errorRatePercent > 5) {
        callouts.push({
          level: 'critical',
          message: (
            <>
              Error rate of <strong>{errorMetrics.errorRatePercent.toFixed(1)}%</strong> is high —
              users are hitting real problems.
            </>
          ),
        });
      } else if (errorMetrics.errorRatePercent > 1) {
        callouts.push({
          level: 'warning',
          message: (
            <>
              Error rate of <strong>{errorMetrics.errorRatePercent.toFixed(1)}%</strong> is above
              the healthy threshold (1%).
            </>
          ),
        });
      } else {
        callouts.push({
          level: 'good',
          message: (
            <>
              Error rate of <strong>{errorMetrics.errorRatePercent.toFixed(1)}%</strong> is within
              the healthy range.
            </>
          ),
        });
      }

      if (errorGroups.length > 1) {
        const totalErrors = errorGroups.reduce((s, e) => s + e.count, 0);
        const topError = errorGroups.reduce((a, b) => (a.count > b.count ? a : b));
        const topPct = totalErrors > 0 ? (topError.count / totalErrors) * 100 : 0;
        if (topPct > 60) {
          callouts.push({
            level: 'info',
            message: (
              <>
                <strong>{topError.type}</strong> drives {topPct.toFixed(0)}% of errors — fixing
                this will have outsized impact.
              </>
            ),
          });
        }
      }
    }
  }

  return (
    <div className="h-full overflow-auto p-4 space-y-3">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div>
          <div className="flex items-center gap-1.5">
            <h2 className="text-lg font-semibold text-[var(--color-text-primary)]">{title}</h2>
            <PanelInfoPopover
              title="Errors & Failures"
              description={
                <>
                  <p>Where your agents are failing, and how often.</p>
                  <ul>
                    <li>
                      <strong>Error rate</strong> — percentage of calls that failed. Healthy
                      systems run under 1%; above 5% means users are hitting real problems.
                    </li>
                    <li>
                      <strong>Time series</strong> — distinguishes a steady low-grade issue (flat
                      line) from an incident (spike). Correlate spikes with deploys or provider
                      outages.
                    </li>
                    <li>
                      <strong>Top errors</strong> — grouped by type so you can see whether one
                      root cause drives most failures. Fixing the top entry usually has outsized
                      impact.
                    </li>
                  </ul>
                  <p>
                    Click an error to see the stack trace, the failing agent, and recent
                    occurrences.
                  </p>
                </>
              }
              callouts={callouts}
            />
          </div>
          <p className="text-xs text-[var(--color-text-secondary)]">
            Error rates and failure analysis.
          </p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => void handleRefresh()}
          disabled={refreshing}
        >
          <RefreshCw className={`w-3.5 h-3.5 ${refreshing ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Error summary */}
      {errorMetrics && (
        <div className="grid grid-cols-3 gap-2">
          <MetricCard
            label="Total Errors"
            value={errorMetrics.total.toLocaleString()}
            status={errorMetrics.total > 0 ? 'critical' : 'good'}
          />
          <MetricCard
            label="Error Rate"
            value={`${errorMetrics.errorRatePercent.toFixed(1)}%`}
            status={
              errorMetrics.errorRatePercent > 5
                ? 'critical'
                : errorMetrics.errorRatePercent > 1
                  ? 'warning'
                  : 'good'
            }
          />
          <MetricCard
            label="Total Requests"
            value={overview?.requests?.total?.toLocaleString() ?? '--'}
          />
        </div>
      )}

      {/* Error time series */}
      {errorMetrics?.timeSeries && errorMetrics.timeSeries.length > 0 && (
        <TimeSeriesChart
          data={errorMetrics.timeSeries}
          label="Errors Over Time"
          height={140}
          color="#ef4444"
        />
      )}

      {/* Error groups */}
      {errorGroups.length === 0 ? (
        <Card className="border-emerald-200 dark:border-emerald-800">
          <CardContent className="p-4 text-center">
            <p className="text-sm text-emerald-600 dark:text-emerald-400 font-medium">
              No errors in this period
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader className="pb-2 pt-3 px-4">
            <CardTitle className="text-sm font-medium flex items-center gap-1.5">
              <AlertTriangle className="w-3.5 h-3.5 text-amber-500" />
              Top Errors ({errorGroups.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3">
            <div className="space-y-2">
              {errorGroups.map((error, i) => (
                <div
                  key={`${error.type}-${i}`}
                  className="rounded-md border border-[var(--color-border-light)] px-3 py-2 space-y-1"
                >
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-1.5 min-w-0">
                      <XCircle className="w-3 h-3 text-red-500 shrink-0" />
                      <span className="text-xs font-medium text-[var(--color-text-primary)] truncate">
                        {error.type}
                      </span>
                    </div>
                    <div className="flex items-center gap-1.5 shrink-0">
                      <Badge className="bg-red-50 text-red-600 dark:bg-red-900/20 dark:text-red-400 text-[10px] px-1.5 py-0">
                        {error.count}x
                      </Badge>
                      <span className="text-[10px] text-[var(--color-text-tertiary)]">
                        {formatTimestamp(error.lastSeen)}
                      </span>
                    </div>
                  </div>
                  <p className="text-[11px] text-[var(--color-text-secondary)] line-clamp-2">
                    {error.innermostMessage || error.outerMessage}
                  </p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
