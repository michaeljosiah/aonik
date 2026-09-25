import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  AlertTriangle,
  Bell,
  Calendar,
  Download,
  Loader2,
  RotateCcw,
} from 'lucide-react';

import { MetricCard, TimeSeriesChart } from '@/components/charts';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import {
  PanelInfoPopover,
  type PanelCallout,
} from '@/components/ui/panel-info-popover';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  observabilityService,
  type DependencyMetricsResponse,
  type ObservabilityOverviewResponse,
} from '@/services/observabilityService';

const TIME_RANGE_OPTIONS = [
  { value: '1h', label: 'Last Hour' },
  { value: '24h', label: 'Last 24 Hours' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
];

function normalizeTimeRange(value: string | null): string {
  return TIME_RANGE_OPTIONS.some((option) => option.value === value) ? value ?? '24h' : '24h';
}

function formatMs(value: number): string {
  if (value >= 1000) return `${(value / 1000).toFixed(1)}s`;
  return `${Math.round(value)}ms`;
}

function formatPercent(value: number): string {
  return `${value.toFixed(1)}%`;
}

function formatNumber(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return value.toLocaleString();
}

function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function errorRateStatus(rate: number): 'good' | 'warning' | 'critical' {
  if (rate < 1) return 'good';
  if (rate < 5) return 'warning';
  return 'critical';
}

function latencyStatus(ms: number): 'good' | 'warning' | 'critical' {
  if (ms < 500) return 'good';
  if (ms < 2000) return 'warning';
  return 'critical';
}

function metricTone(value: number, warning: number, critical: number): 'good' | 'warning' | 'critical' {
  if (value >= critical) return 'critical';
  if (value >= warning) return 'warning';
  return 'good';
}

function formatObservabilityDateLabel(): string {
  return new Intl.DateTimeFormat(undefined, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(new Date()).toUpperCase();
}

function getErrorMessage(error: unknown, fallback = 'Unknown error'): string {
  if (error && typeof error === 'object' && 'userMessage' in error) {
    const message = String((error as { userMessage?: string }).userMessage ?? '').trim();
    if (message) return message;
  }

  if (error instanceof Error) {
    const message = error.message.trim();
    if (message) return message;
  }

  return fallback;
}

function LoadingState() {
  return (
    <div className="flex items-center justify-center py-20">
      <Loader2 className="mr-2 h-5 w-5 animate-spin text-muted-foreground" />
      <span className="text-muted-foreground">Loading overview...</span>
    </div>
  );
}

function ErrorState({ message }: { message: string }) {
  return (
    <Card className="border-l-4 border-l-destructive">
      <CardContent className="flex items-center gap-3 p-5">
        <AlertTriangle className="h-5 w-5 shrink-0 text-destructive" />
        <div>
          <p className="text-sm font-medium text-foreground">Failed to load overview</p>
          <p className="mt-0.5 text-xs text-muted-foreground">{message}</p>
        </div>
      </CardContent>
    </Card>
  );
}

function NotConfiguredBanner() {
  return (
    <Card className="mb-6 border-l-4 border-l-warning">
      <CardContent className="flex items-center gap-3 p-5">
        <AlertTriangle className="h-5 w-5 shrink-0 text-warning" />
        <p className="text-sm text-muted-foreground">
          Application Insights is not configured. Go to{' '}
          <a
            href="/settings/global"
            className="font-medium text-primary hover:underline"
          >
            Settings &gt; Observability
          </a>{' '}
          to set up your App Insights connection.
        </p>
      </CardContent>
    </Card>
  );
}

export function ObservabilityPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [timeRange, setTimeRange] = useState(() => normalizeTimeRange(searchParams.get('timeRange')));
  const [overview, setOverview] = useState<ObservabilityOverviewResponse | null>(null);
  const [overviewLoading, setOverviewLoading] = useState(false);
  const [initialLoad, setInitialLoad] = useState(true);
  const [overviewError, setOverviewError] = useState<string | null>(null);
  const [depsData, setDepsData] = useState<DependencyMetricsResponse | null>(null);
  const [depsLoading, setDepsLoading] = useState(false);

  const updateQuery = useCallback(
    (updates: Record<string, string | null>) => {
      const next = new URLSearchParams(searchParams);
      for (const [key, value] of Object.entries(updates)) {
        if (value) next.set(key, value);
        else next.delete(key);
      }
      setSearchParams(next, { replace: true });
    },
    [searchParams, setSearchParams],
  );

  useEffect(() => {
    const nextRange = normalizeTimeRange(searchParams.get('timeRange'));
    setTimeRange((current) => (current === nextRange ? current : nextRange));
  }, [searchParams]);

  const fetchOverview = useCallback(async (range: string) => {
    setOverviewLoading(true);
    setOverviewError(null);
    try {
      const [overviewResult, depsResult] = await Promise.all([
        observabilityService.getOverview(range),
        observabilityService.getDependencies(range),
      ]);
      setOverview(overviewResult);
      setDepsData(depsResult);
    } catch (error) {
      setOverviewError(getErrorMessage(error));
    } finally {
      setOverviewLoading(false);
      setDepsLoading(false);
      setInitialLoad(false);
    }
  }, []);

  useEffect(() => {
    setDepsLoading(true);
    void fetchOverview(timeRange);
  }, [timeRange, fetchOverview]);

  const handleRefresh = () => {
    setDepsLoading(true);
    void fetchOverview(timeRange);
  };

  const overviewCallouts: PanelCallout[] = [];
  if (overview?.errors) {
    const rate = overview.errors.errorRatePercent;
    overviewCallouts.push({
      level: errorRateStatus(rate),
      message: (
        <>
          <strong>Error rate</strong> is {rate.toFixed(1)}%
          {rate < 1 ? ' and healthy.' : rate < 5 ? ' and elevated.' : ' and critical.'}
        </>
      ),
    });
  }
  if (overview?.latency) {
    const p95 = overview.latency.p95Ms;
    overviewCallouts.push({
      level: latencyStatus(p95),
      message: (
        <>
          <strong>P95 latency</strong> is {formatMs(p95)}.
        </>
      ),
    });
  }
  if (depsData?.dependencies?.length) {
    const critical = depsData.dependencies.filter((service) => service.successRatePercent < 95).length;
    const degraded = depsData.dependencies.filter(
      (service) => service.successRatePercent < 99.5 && service.successRatePercent >= 95,
    ).length;
    overviewCallouts.push({
      level: critical > 0 ? 'critical' : degraded > 0 ? 'warning' : 'good',
      message: (
        <>
          <strong>{depsData.dependencies.length}</strong> dependencies tracked.
          {critical > 0 ? ` ${critical} critical.` : degraded > 0 ? ` ${degraded} degraded.` : ' All healthy.'}
        </>
      ),
    });
  }

  const services = depsData?.configured ? depsData.dependencies.slice(0, 12) : [];
  const rankedServices = depsData?.configured
    ? [...depsData.dependencies].sort((a, b) => b.totalCalls - a.totalCalls)
    : [];
  const topErrors = overview?.errors?.topErrors ?? [];
  const totalRequests = overview?.requests?.total ?? 0;
  const requestRate = overview?.requests?.ratePerMinute ?? 0;
  const errorRate = overview?.errors?.errorRatePercent ?? 0;
  const errorTotal = overview?.errors?.total ?? 0;
  const p50Latency = overview?.latency?.p50Ms ?? 0;
  const p95Latency = overview?.latency?.p95Ms ?? 0;
  const p99Latency = overview?.latency?.p99Ms ?? 0;
  const successRate = Math.max(0, 100 - errorRate);
  const healthyServices = services.filter((service) => service.successRatePercent >= 99.5).length;
  const degradedServices = services.filter(
    (service) => service.successRatePercent < 99.5 && service.successRatePercent >= 95,
  ).length;
  const criticalServices = services.filter((service) => service.successRatePercent < 95).length;
  const avgDependencyLatency = services.length > 0
    ? services.reduce((sum, service) => sum + service.avgDurationMs, 0) / services.length
    : 0;
  const totalDependencyCalls = services.reduce((sum, service) => sum + service.totalCalls, 0);
  const failedDependencyCalls = services.reduce((sum, service) => sum + service.failedCalls, 0);
  const topTrafficServices = rankedServices.slice(0, 5);
  const slowestServices = [...rankedServices].sort((a, b) => b.avgDurationMs - a.avgDurationMs).slice(0, 5);
  const riskServices = [...rankedServices]
    .filter((service) => service.successRatePercent < 99.5 || service.avgDurationMs >= 500)
    .sort((a, b) => {
      const aRisk = (100 - a.successRatePercent) * 100 + a.avgDurationMs;
      const bRisk = (100 - b.successRatePercent) * 100 + b.avgDurationMs;
      return bRisk - aRisk;
    })
    .slice(0, 5);
  const headline = criticalServices > 0
    ? 'Critical service degradation detected'
    : degradedServices > 0 || errorRate >= 1 || p95Latency >= 2000
    ? 'Performance requires attention'
    : 'All systems operational';
  const headlineDetail = services.length > 0
    ? `${healthyServices} healthy services · ${criticalServices} critical · ${degradedServices} degraded`
    : 'Overview is live, but service health data has not been loaded yet.';
  const dateLabel = formatObservabilityDateLabel();

  if (initialLoad) {
    return <PageLoadingScreen message="Loading observability" />;
  }

  return (
    <div className="flex h-full flex-col">
      <div className="border-b border-border bg-card">
        <div className="px-6 pt-5 pb-4">
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-xl font-semibold text-foreground">Observability</h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Route-based observability shell. Overview is now a standalone page and the remaining surfaces will be ported individually.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <Select
                value={timeRange}
                onValueChange={(value) => {
                  setTimeRange(value);
                  updateQuery({ timeRange: value });
                }}
              >
                <SelectTrigger className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TIME_RANGE_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button variant="outline" size="icon" onClick={handleRefresh} aria-label="Refresh">
                <RotateCcw className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
        <div className="mb-4 flex items-center gap-1.5">
          <span className="text-sm font-medium text-muted-foreground">Platform Overview</span>
          <PanelInfoPopover
            title="Platform Overview"
            description={
              <>
                <p>
                  Tracks inbound request volume, application failures, response latency, and dependency health.
                </p>
                <p>
                  This page is the overview route for the new observability IA. Traces, logs, and audit are now separate destinations in the sidebar.
                </p>
              </>
            }
            callouts={overviewCallouts.length > 0 ? overviewCallouts : undefined}
            panelKind="overview"
            getMetrics={
              overview
                ? () => ({
                    requests: overview.requests,
                    errors: overview.errors,
                    latency: overview.latency,
                    dependencies: depsData?.dependencies ?? [],
                  })
                : undefined
            }
          />
        </div>

        {overviewLoading ? <LoadingState /> : overviewError ? <ErrorState message={overviewError} /> : null}

        {!overviewLoading && !overviewError && overview && (
          <div className="space-y-6">
            {!overview.configured && <NotConfiguredBanner />}

            {overview.configured && overview.requests && overview.errors && overview.latency && (
              <>
                <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                  <div>
                    <h2 className="text-4xl font-bold tracking-tight text-foreground">Overview</h2>
                    <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
                      Live pulse across requests, dependencies, and application failures for the selected time range.
                    </p>
                    {depsLoading && (
                      <p className="mt-3 text-xs text-muted-foreground">
                        Refreshing dependency health for the overview...
                      </p>
                    )}
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    <Button variant="outline" size="sm">
                      <Calendar className="mr-2 h-3.5 w-3.5" />
                      {TIME_RANGE_OPTIONS.find((option) => option.value === timeRange)?.label ?? timeRange}
                    </Button>
                    <Button variant="outline" size="sm" disabled>
                      <Download className="mr-2 h-3.5 w-3.5" />
                      Snapshot
                    </Button>
                    <Button size="sm" disabled>
                      <Bell className="mr-2 h-3.5 w-3.5" />
                      Alert rules
                    </Button>
                  </div>
                </div>

                <Card>
                  <CardContent className="grid gap-4 p-5 lg:grid-cols-[auto_1fr_repeat(5,minmax(0,1fr))] lg:items-center">
                    <div className="flex items-center justify-center lg:justify-start">
                      <span
                        className={cn(
                          'inline-flex h-3 w-3 rounded-full',
                          criticalServices > 0
                            ? 'bg-destructive ring-[6px] ring-destructive/15'
                            : degradedServices > 0 || errorRate >= 1 || p95Latency >= 2000
                            ? 'bg-warning ring-[6px] ring-warning/15'
                            : 'bg-success ring-[6px] ring-success/15',
                        )}
                      />
                    </div>
                    <div>
                      <div className="text-sm font-semibold text-foreground">{headline}</div>
                      <div className="mt-1 text-xs text-muted-foreground">{headlineDetail}</div>
                    </div>
                    {[
                      { label: 'Success', value: formatPercent(successRate) },
                      { label: 'P50', value: formatMs(p50Latency) },
                      { label: 'P99', value: formatMs(p99Latency) },
                      { label: 'Ops · window', value: formatNumber(totalRequests) },
                      { label: 'Errors · window', value: formatNumber(errorTotal) },
                    ].map((item) => (
                      <div key={item.label} className="text-left lg:text-right">
                        <div className="font-mono text-sm font-semibold text-foreground">{item.value}</div>
                        <div className="mt-1 text-[11px] text-muted-foreground">{item.label}</div>
                      </div>
                    ))}
                  </CardContent>
                </Card>

                <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-4">
                  <MetricCard
                    label="Error rate"
                    value={formatPercent(errorRate)}
                    subtitle={`${formatNumber(errorTotal)} failed requests in range`}
                    status={errorRateStatus(errorRate)}
                  />
                  <MetricCard
                    label="P95 latency"
                    value={formatMs(p95Latency)}
                    subtitle={`P50 ${formatMs(p50Latency)} · P99 ${formatMs(p99Latency)}`}
                    status={latencyStatus(p95Latency)}
                  />
                  <MetricCard
                    label="Traffic"
                    value={formatNumber(totalRequests)}
                    subtitle={`${requestRate.toFixed(1)} requests/minute`}
                  />
                  <MetricCard
                    label="Service health"
                    value={services.length > 0 ? `${healthyServices}/${services.length}` : '—'}
                    subtitle={services.length > 0 ? `${criticalServices} critical · ${degradedServices} degraded` : 'Dependency data unavailable'}
                    status={criticalServices > 0 ? 'critical' : degradedServices > 0 ? 'warning' : 'good'}
                  />
                </div>

                <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1.45fr_1fr]">
                  <Card>
                    <CardContent className="p-5">
                      <div className="mb-4 flex items-start justify-between gap-4">
                        <div>
                          <h3 className="text-lg font-semibold text-foreground">Traffic & latency</h3>
                          <p className="mt-1 text-sm text-muted-foreground">
                            Request volume, error rate, and latency trend across the selected window.
                          </p>
                        </div>
                        <div className="text-right text-xs font-medium text-muted-foreground">
                          {dateLabel}
                        </div>
                      </div>

                      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
                        <TimeSeriesChart
                          data={overview.requests.timeSeries}
                          label="Request volume"
                          formatValue={(value) => formatNumber(value)}
                        />
                        <TimeSeriesChart
                          data={overview.errors.timeSeries}
                          label="Error rate"
                          color="var(--destructive)"
                          formatValue={(value) => formatPercent(value)}
                        />
                      </div>

                      <div className="mt-4">
                        <TimeSeriesChart
                          data={overview.latency.timeSeries}
                          label="Average latency"
                          color="var(--warning)"
                          formatValue={(value) => formatMs(value)}
                        />
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-5">
                      <div className="mb-4 flex items-start justify-between gap-3">
                        <div>
                          <h3 className="text-lg font-semibold text-foreground">Incidents</h3>
                          <p className="mt-1 text-sm text-muted-foreground">
                            Derived from top error fingerprints until a dedicated incident stream exists.
                          </p>
                        </div>
                        <span className="text-xs text-muted-foreground">{topErrors.length} surfaced</span>
                      </div>

                      <div className="space-y-3">
                        {topErrors.slice(0, 3).map((error) => {
                          const severity = metricTone(error.count, 5, 20);
                          const severityClass = severity === 'critical'
                            ? 'border-l-destructive text-destructive bg-destructive/10'
                            : severity === 'warning'
                            ? 'border-l-warning text-warning bg-warning-subtle'
                            : 'border-l-border text-muted-foreground bg-card';
                          return (
                            <div
                              key={`${error.type}-${error.lastSeen}`}
                              className={cn(
                                'rounded-xl border border-border border-l-4 bg-muted p-4',
                                severityClass,
                              )}
                            >
                              <div className="mb-2 flex items-start justify-between gap-3">
                                <div className="min-w-0">
                                  <div className="text-sm font-semibold text-foreground">
                                    {error.outerMessage || error.type}
                                  </div>
                                  <div className="mt-1 truncate font-mono text-[11px] text-muted-foreground">
                                    {error.type}
                                  </div>
                                </div>
                                <Badge
                                  variant={severity === 'critical' ? 'destructive' : severity === 'warning' ? 'warning' : 'secondary'}
                                  className="font-mono"
                                >
                                  {severity === 'critical' ? 'critical' : severity === 'warning' ? 'warning' : 'info'}
                                </Badge>
                              </div>
                              <div className="text-sm text-muted-foreground">
                                {error.innermostMessage || 'No inner exception message captured.'}
                              </div>
                              <div className="mt-3 flex items-center justify-between gap-3 text-[11px] text-muted-foreground">
                                <span>{formatNumber(error.count)} occurrences</span>
                                <span>{relativeTime(error.lastSeen)}</span>
                              </div>
                            </div>
                          );
                        })}
                        {topErrors.length === 0 && (
                          <div className="rounded-xl border border-dashed border-border bg-muted px-4 py-8 text-center text-sm text-muted-foreground">
                            No incident-like error spikes captured in this window.
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                </div>

                <div className="grid grid-cols-1 gap-4 xl:grid-cols-4">
                  <MetricCard
                    label="Service calls"
                    value={formatNumber(totalDependencyCalls)}
                    subtitle="Observed dependency calls in the current service slice"
                  />
                  <MetricCard
                    label="Failed service calls"
                    value={formatNumber(failedDependencyCalls)}
                    subtitle="Dependency failures in the surfaced slice"
                    status={failedDependencyCalls > 0 ? 'warning' : 'good'}
                  />
                  <MetricCard
                    label="Avg dependency latency"
                    value={services.length > 0 ? formatMs(avgDependencyLatency) : '—'}
                    subtitle="Mean response time across surfaced services"
                    status={services.length > 0 ? latencyStatus(avgDependencyLatency) : undefined}
                  />
                  <MetricCard
                    label="Dependency warnings"
                    value={formatNumber(degradedServices)}
                    subtitle="Services below 99.5% success rate"
                    status={degradedServices > 0 ? 'warning' : 'good'}
                  />
                </div>

                <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
                  <Card>
                    <CardContent className="p-5">
                      <div className="mb-4 flex items-start justify-between gap-3">
                        <div>
                          <h3 className="text-lg font-semibold text-foreground">Highest traffic services</h3>
                          <p className="mt-1 text-sm text-muted-foreground">
                            The busiest dependencies in the current window.
                          </p>
                        </div>
                        <span className="text-xs text-muted-foreground">Top 5</span>
                      </div>
                      <div className="space-y-3">
                        {topTrafficServices.map((service, index) => (
                          <div key={`${service.type}-${service.name}`} className="flex items-start gap-3 rounded-lg border border-border bg-muted p-3">
                            <span className="font-mono text-xs text-muted-foreground">#{index + 1}</span>
                            <div className="min-w-0 flex-1">
                              <div className="truncate font-mono text-xs text-foreground">{service.name}</div>
                              <div className="mt-1 text-[11px] text-muted-foreground">{service.type}</div>
                            </div>
                            <div className="text-right">
                              <div className="font-mono text-xs font-semibold text-foreground">{formatNumber(service.totalCalls)}</div>
                              <div className="mt-1 text-[11px] text-muted-foreground">calls</div>
                            </div>
                          </div>
                        ))}
                        {topTrafficServices.length === 0 && (
                          <div className="rounded-lg border border-dashed border-border bg-muted px-4 py-8 text-center text-sm text-muted-foreground">
                            No dependency volume data yet.
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-5">
                      <div className="mb-4 flex items-start justify-between gap-3">
                        <div>
                          <h3 className="text-lg font-semibold text-foreground">Slowest services</h3>
                          <p className="mt-1 text-sm text-muted-foreground">
                            Dependencies with the highest average duration.
                          </p>
                        </div>
                        <span className="text-xs text-muted-foreground">Top 5</span>
                      </div>
                      <div className="space-y-3">
                        {slowestServices.map((service, index) => (
                          <div key={`${service.type}-${service.name}`} className="flex items-start gap-3 rounded-lg border border-border bg-muted p-3">
                            <span className="font-mono text-xs text-muted-foreground">#{index + 1}</span>
                            <div className="min-w-0 flex-1">
                              <div className="truncate font-mono text-xs text-foreground">{service.name}</div>
                              <div className="mt-1 text-[11px] text-muted-foreground">{service.type} · {formatPercent(service.successRatePercent)} success</div>
                            </div>
                            <div className="text-right">
                              <div className="font-mono text-xs font-semibold text-foreground">{formatMs(service.avgDurationMs)}</div>
                              <div className="mt-1 text-[11px] text-muted-foreground">avg</div>
                            </div>
                          </div>
                        ))}
                        {slowestServices.length === 0 && (
                          <div className="rounded-lg border border-dashed border-border bg-muted px-4 py-8 text-center text-sm text-muted-foreground">
                            No dependency latency data yet.
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>

                  <Card>
                    <CardContent className="p-5">
                      <div className="mb-4 flex items-start justify-between gap-3">
                        <div>
                          <h3 className="text-lg font-semibold text-foreground">Needs attention</h3>
                          <p className="mt-1 text-sm text-muted-foreground">
                            Services with elevated latency or below-target success rates.
                          </p>
                        </div>
                        <span className="text-xs text-muted-foreground">Top 5</span>
                      </div>
                      <div className="space-y-3">
                        {riskServices.map((service) => {
                          const status = service.successRatePercent < 95
                            ? 'critical'
                            : service.successRatePercent < 99.5 || service.avgDurationMs >= 2000
                            ? 'warning'
                            : 'good';
                          return (
                            <div key={`${service.type}-${service.name}`} className="rounded-lg border border-border bg-muted p-3">
                              <div className="flex items-start justify-between gap-3">
                                <div className="min-w-0">
                                  <div className="truncate font-mono text-xs text-foreground">{service.name}</div>
                                  <div className="mt-1 text-[11px] text-muted-foreground">{service.type}</div>
                                </div>
                                <Badge
                                  variant={status === 'critical' ? 'destructive' : status === 'warning' ? 'warning' : 'success'}
                                  className="font-mono"
                                >
                                  {status}
                                </Badge>
                              </div>
                              <div className="mt-3 grid grid-cols-2 gap-3 text-[11px]">
                                <div>
                                  <div className="text-muted-foreground">Success</div>
                                  <div className="mt-1 font-mono text-foreground">{formatPercent(service.successRatePercent)}</div>
                                </div>
                                <div>
                                  <div className="text-muted-foreground">Avg latency</div>
                                  <div className="mt-1 font-mono text-foreground">{formatMs(service.avgDurationMs)}</div>
                                </div>
                              </div>
                            </div>
                          );
                        })}
                        {riskServices.length === 0 && (
                          <div className="rounded-lg border border-dashed border-border bg-muted px-4 py-8 text-center text-sm text-muted-foreground">
                            No elevated dependency risk signals in this window.
                          </div>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                </div>

                <Card>
                  <CardContent className="p-5">
                    <div className="mb-4 flex items-start justify-between gap-4">
                      <div>
                        <h3 className="text-lg font-semibold text-foreground">Services</h3>
                        <p className="mt-1 text-sm text-muted-foreground">
                          Backing services and external dependencies currently visible from Application Insights.
                        </p>
                      </div>
                      <span className="text-xs text-muted-foreground">{services.length} surfaced</span>
                    </div>

                    <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                      {services.map((service) => {
                        const status = service.successRatePercent < 95
                          ? 'critical'
                          : service.successRatePercent < 99.5
                          ? 'warning'
                          : 'good';
                        const statusColor = status === 'critical'
                          ? 'bg-destructive'
                          : status === 'warning'
                          ? 'bg-warning'
                          : 'bg-success';
                        return (
                          <div
                            key={`${service.type}-${service.name}`}
                            className="rounded-lg border border-border bg-muted p-3"
                          >
                            <div className="flex items-start gap-3">
                              <span className={cn('mt-1 h-2.5 w-2.5 shrink-0 rounded-full', statusColor)} />
                              <div className="min-w-0 flex-1">
                                <div className="truncate font-mono text-xs text-foreground">{service.name}</div>
                                <div className="mt-1 text-[11px] text-muted-foreground">
                                  {service.type} · {formatPercent(service.successRatePercent)} success
                                </div>
                                <div className="mt-2 text-[11px] text-muted-foreground">
                                  {formatMs(service.avgDurationMs)} avg · {formatNumber(service.totalCalls)} calls
                                </div>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                      {services.length === 0 && (
                        <div className="rounded-lg border border-dashed border-border bg-muted px-4 py-10 text-center text-sm text-muted-foreground md:col-span-2 xl:col-span-4">
                          Service health is not available until dependency data is loaded.
                        </div>
                      )}
                    </div>
                  </CardContent>
                </Card>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
