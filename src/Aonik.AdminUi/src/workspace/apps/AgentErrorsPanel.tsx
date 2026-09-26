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
        <p className="text-sm text-muted-foreground py-4 text-center">
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
            <h2 className="text-lg font-semibold text-foreground">{title}</h2>
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
              panelKind="errors"
              getMetrics={() => ({
                totalErrors: errorMetrics?.total ?? 0,
                errorRatePercent: errorMetrics?.errorRatePercent ?? 0,
                totalRequests: overview?.requests?.total ?? null,
                topErrors: errorGroups.slice(0, 5).map((e) => ({
                  type: e.type,
                  message: e.innermostMessage || e.outerMessage,
                  count: e.count,
                  lastSeen: e.lastSeen,
                })),
              })}
            />
          </div>
          <p className="text-xs text-muted-foreground">
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
          color="var(--destructive)"
        />
      )}

      {/* Error groups */}
      {errorGroups.length === 0 ? (
        <Card className="border-success/30">
          <CardContent className="p-4 text-center">
            <p className="text-sm text-success font-medium">
              No errors in this period
            </p>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader className="pb-2 pt-3 px-4">
            <CardTitle className="text-sm font-medium flex items-center gap-1.5">
              <AlertTriangle className="w-3.5 h-3.5 text-warning" />
              Top Errors ({errorGroups.length})
            </CardTitle>
          </CardHeader>
          <CardContent className="px-4 pb-3">
            <div className="space-y-2">
              {errorGroups.map((error, i) => (
                <div
                  key={`${error.type}-${i}`}
                  className="rounded-md border border-border px-3 py-2 space-y-1"
                >
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-1.5 min-w-0">
                      <XCircle className="w-3 h-3 text-destructive shrink-0" />
                      <span className="text-xs font-medium text-foreground truncate">
                        {error.type}
                      </span>
                    </div>
                    <div className="flex items-center gap-1.5 shrink-0">
                      <Badge variant="outline" className="border-transparent bg-destructive/10 text-destructive font-mono tabular-nums text-[10px] px-1.5 py-0">
                        {error.count}x
                      </Badge>
                      <span className="text-[10px] text-muted-foreground">
                        {formatTimestamp(error.lastSeen)}
                      </span>
                    </div>
                  </div>
                  <p className="text-[11px] text-muted-foreground line-clamp-2">
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
