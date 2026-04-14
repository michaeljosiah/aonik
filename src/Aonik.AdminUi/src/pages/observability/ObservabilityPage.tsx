import { useState, useEffect, useCallback } from 'react';
import {
  Activity,
  RotateCcw,
  Loader2,
  AlertTriangle,
  ChevronDown,
  ChevronRight,
} from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { MetricCard, TimeSeriesChart, MultiLineChart } from '@/components/charts';
import {
  observabilityService,
  type ObservabilityOverviewResponse,
  type ErrorsResponse,
  type ErrorGroup,
  type DependencyMetricsResponse,
  type AiPerformanceResponse,
  type JobMetricsResponse,
} from '@/services/observabilityService';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatMs(v: number): string {
  if (v >= 1000) return `${(v / 1000).toFixed(1)}s`;
  return `${Math.round(v)}ms`;
}

function formatPercent(v: number): string {
  return `${v.toFixed(1)}%`;
}

function formatNumber(v: number): string {
  if (v >= 1_000_000) return `${(v / 1_000_000).toFixed(1)}M`;
  if (v >= 1_000) return `${(v / 1_000).toFixed(1)}K`;
  return v.toLocaleString();
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

// ---------------------------------------------------------------------------
// Tab-level loading / error wrapper
// ---------------------------------------------------------------------------

function TabLoading() {
  return (
    <div className="flex items-center justify-center py-20">
      <Loader2 className="mr-2 h-5 w-5 animate-spin text-[var(--color-text-tertiary)]" />
      <span className="text-[var(--color-text-secondary)]">Loading...</span>
    </div>
  );
}

function TabError({ message }: { message: string }) {
  return (
    <Card className="border-l-4 border-l-red-500">
      <CardContent className="flex items-center gap-3 p-5">
        <AlertTriangle className="h-5 w-5 text-red-500 shrink-0" />
        <div>
          <p className="text-sm font-medium text-[var(--color-text-primary)]">
            Failed to load data
          </p>
          <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
            {message}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}

function NotConfiguredBanner() {
  return (
    <Card className="border-l-4 border-l-amber-500 mb-6">
      <CardContent className="flex items-center gap-3 p-5">
        <AlertTriangle className="h-5 w-5 text-amber-500 shrink-0" />
        <p className="text-sm text-[var(--color-text-secondary)]">
          Application Insights is not configured. Go to{' '}
          <a
            href="/settings/global"
            className="font-medium text-[var(--color-brand-primary)] hover:underline"
          >
            Settings &gt; Observability
          </a>{' '}
          to set up your App Insights connection.
        </p>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Expandable error row
// ---------------------------------------------------------------------------

function ErrorRow({
  error,
  expanded,
  onToggle,
}: {
  error: ErrorGroup;
  expanded: boolean;
  onToggle: () => void;
}) {
  return (
    <>
      <tr
        className="border-b border-[var(--color-border-light)] hover:bg-[var(--color-surface-inset)] cursor-pointer"
        onClick={onToggle}
      >
        <td className="px-4 py-3 text-sm">
          <span className="inline-flex items-center gap-1">
            {expanded ? (
              <ChevronDown className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
            ) : (
              <ChevronRight className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
            )}
            <span className="font-mono text-xs text-[var(--color-text-secondary)]">
              {error.type}
            </span>
          </span>
        </td>
        <td className="px-4 py-3 text-sm text-[var(--color-text-primary)] max-w-xs truncate">
          {error.outerMessage}
        </td>
        <td className="px-4 py-3 text-sm text-[var(--color-text-primary)] text-right font-medium">
          {error.count}
        </td>
        <td className="px-4 py-3 text-sm text-[var(--color-text-tertiary)] text-right whitespace-nowrap">
          {relativeTime(error.lastSeen)}
        </td>
      </tr>
      {expanded && error.innermostMessage && (
        <tr>
          <td colSpan={4} className="px-4 py-2">
            <div className="text-xs bg-[var(--color-surface-inset)] p-3 rounded-md">
              <span className="text-[var(--color-text-tertiary)]">Innermost: </span>
              <span className="text-[var(--color-text-primary)]">{error.innermostMessage}</span>
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

// ---------------------------------------------------------------------------
// Main page
// ---------------------------------------------------------------------------

const TIME_RANGE_OPTIONS = [
  { value: '1h', label: 'Last Hour' },
  { value: '24h', label: 'Last 24 Hours' },
  { value: '7d', label: 'Last 7 Days' },
  { value: '30d', label: 'Last 30 Days' },
];

export function ObservabilityPage() {
  const [timeRange, setTimeRange] = useState('24h');
  const [activeTab, setActiveTab] = useState('overview');

  // --- Overview state ---
  const [overview, setOverview] = useState<ObservabilityOverviewResponse | null>(null);
  const [overviewLoading, setOverviewLoading] = useState(false);
  const [overviewError, setOverviewError] = useState<string | null>(null);

  // --- Errors state ---
  const [errorsData, setErrorsData] = useState<ErrorsResponse | null>(null);
  const [errorsLoading, setErrorsLoading] = useState(false);
  const [errorsError, setErrorsError] = useState<string | null>(null);
  const [expandedErrors, setExpandedErrors] = useState<Set<number>>(new Set());

  // --- AI state ---
  const [aiData, setAiData] = useState<AiPerformanceResponse | null>(null);
  const [aiLoading, setAiLoading] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);

  // --- Dependencies state ---
  const [depsData, setDepsData] = useState<DependencyMetricsResponse | null>(null);
  const [depsLoading, setDepsLoading] = useState(false);
  const [depsError, setDepsError] = useState<string | null>(null);

  // --- Jobs state ---
  const [jobsData, setJobsData] = useState<JobMetricsResponse | null>(null);
  const [jobsLoading, setJobsLoading] = useState(false);
  const [jobsError, setJobsError] = useState<string | null>(null);

  // --- Fetch helpers ---

  const fetchOverview = useCallback(async (tr: string) => {
    setOverviewLoading(true);
    setOverviewError(null);
    try {
      const data = await observabilityService.getOverview(tr);
      setOverview(data);
    } catch (err) {
      setOverviewError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setOverviewLoading(false);
    }
  }, []);

  const fetchErrors = useCallback(async (tr: string) => {
    setErrorsLoading(true);
    setErrorsError(null);
    try {
      const data = await observabilityService.getErrors(tr);
      setErrorsData(data);
    } catch (err) {
      setErrorsError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setErrorsLoading(false);
    }
  }, []);

  const fetchAi = useCallback(async (tr: string) => {
    setAiLoading(true);
    setAiError(null);
    try {
      const data = await observabilityService.getAiPerformance(tr);
      setAiData(data);
    } catch (err) {
      setAiError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setAiLoading(false);
    }
  }, []);

  const fetchDeps = useCallback(async (tr: string) => {
    setDepsLoading(true);
    setDepsError(null);
    try {
      const data = await observabilityService.getDependencies(tr);
      setDepsData(data);
    } catch (err) {
      setDepsError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setDepsLoading(false);
    }
  }, []);

  const fetchJobs = useCallback(async (tr: string) => {
    setJobsLoading(true);
    setJobsError(null);
    try {
      const data = await observabilityService.getJobs(tr);
      setJobsData(data);
    } catch (err) {
      setJobsError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setJobsLoading(false);
    }
  }, []);

  // Fetch on tab activation + time range change
  useEffect(() => {
    if (activeTab === 'overview') fetchOverview(timeRange);
    if (activeTab === 'errors') fetchErrors(timeRange);
    if (activeTab === 'ai') fetchAi(timeRange);
    if (activeTab === 'dependencies') fetchDeps(timeRange);
    if (activeTab === 'jobs') fetchJobs(timeRange);
  }, [activeTab, timeRange, fetchOverview, fetchErrors, fetchAi, fetchDeps, fetchJobs]);

  // Refresh handler
  const handleRefresh = () => {
    if (activeTab === 'overview') fetchOverview(timeRange);
    if (activeTab === 'errors') fetchErrors(timeRange);
    if (activeTab === 'ai') fetchAi(timeRange);
    if (activeTab === 'dependencies') fetchDeps(timeRange);
    if (activeTab === 'jobs') fetchJobs(timeRange);
  };

  const toggleErrorExpanded = (idx: number) => {
    setExpandedErrors((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) next.delete(idx);
      else next.add(idx);
      return next;
    });
  };

  // -----------------------------------------------------------------------
  // Render helpers
  // -----------------------------------------------------------------------

  const renderOverviewTab = () => {
    if (overviewLoading) return <TabLoading />;
    if (overviewError) return <TabError message={overviewError} />;
    if (!overview) return null;

    return (
      <div className="space-y-6">
        {!overview.configured && <NotConfiguredBanner />}

        {overview.configured && overview.requests && overview.errors && overview.latency && (
          <>
            {/* Metric cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <MetricCard
                label="Total Requests"
                value={formatNumber(overview.requests.total)}
              />
              <MetricCard
                label="Requests/min"
                value={overview.requests.ratePerMinute.toFixed(1)}
              />
              <MetricCard
                label="Error Rate"
                value={formatPercent(overview.errors.errorRatePercent)}
                status={errorRateStatus(overview.errors.errorRatePercent)}
              />
              <MetricCard
                label="P95 Latency"
                value={formatMs(overview.latency.p95Ms)}
                status={latencyStatus(overview.latency.p95Ms)}
              />
            </div>

            {/* Charts */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <TimeSeriesChart
                data={overview.requests.timeSeries}
                label="Request Rate"
                formatValue={(v) => formatNumber(v)}
              />
              <TimeSeriesChart
                data={overview.errors.timeSeries}
                label="Error Rate"
                color="#ef4444"
                formatValue={(v) => formatNumber(v)}
              />
            </div>

            <MultiLineChart
              label="Latency (ms)"
              series={[
                {
                  key: 'p50',
                  label: 'P50',
                  color: '#22c55e',
                  data: overview.latency.timeSeries.map((p) => ({
                    timestamp: p.timestamp,
                    value: p.value,
                  })),
                },
                {
                  key: 'p95',
                  label: 'P95',
                  color: '#f59e0b',
                  data: overview.latency.timeSeries.map((p) => ({
                    timestamp: p.timestamp,
                    value: p.value,
                  })),
                },
                {
                  key: 'p99',
                  label: 'P99',
                  color: '#ef4444',
                  data: overview.latency.timeSeries.map((p) => ({
                    timestamp: p.timestamp,
                    value: p.value,
                  })),
                },
              ]}
              formatValue={(v) => formatMs(v)}
            />
          </>
        )}
      </div>
    );
  };

  const renderAiTab = () => {
    if (aiLoading) return <TabLoading />;
    if (aiError) return <TabError message={aiError} />;
    if (!aiData) return null;

    const totalEstimatedCostUsd = (aiData.byUseCase ?? []).reduce(
      (sum, uc) => sum + (uc.estimatedCostUsd ?? 0),
      0,
    );
    const totalCallsAcrossUseCases = (aiData.byUseCase ?? []).reduce(
      (sum, uc) => sum + uc.calls,
      0,
    );
    const formatUsd = (v: number) =>
      v >= 1 ? `$${v.toFixed(2)}` : `$${v.toFixed(4)}`;

    return (
      <div className="space-y-6">
        {!aiData.configured && <NotConfiguredBanner />}

        {/* Top row — 6 MetricCards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
          <MetricCard
            label="P50 Latency"
            value={aiData.latency ? formatMs(aiData.latency.p50Ms) : '—'}
          />
          <MetricCard
            label="P95 Latency"
            value={aiData.latency ? formatMs(aiData.latency.p95Ms) : '—'}
            status={aiData.latency ? latencyStatus(aiData.latency.p95Ms) : undefined}
          />
          <MetricCard
            label="P50 TTFT"
            value={aiData.ttft ? formatMs(aiData.ttft.p50Ms) : '—'}
          />
          <MetricCard
            label="P95 TTFT"
            value={aiData.ttft ? formatMs(aiData.ttft.p95Ms) : '—'}
          />
          <MetricCard
            label="Total Tokens"
            value={aiData.tokenUsage ? formatNumber(aiData.tokenUsage.totalTokens) : '—'}
          />
          <MetricCard
            label="Avg Tokens/Run"
            value={
              aiData.tokenUsage
                ? formatNumber(
                    Math.round(
                      aiData.tokenUsage.avgInputTokensPerRun +
                        aiData.tokenUsage.avgOutputTokensPerRun,
                    ),
                  )
                : '—'
            }
          />
        </div>

        {/* AI Spend row — surfaced from TelemetryChatClient cost catalog */}
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-medium text-[var(--color-text-secondary)]">
                AI Spend (estimated)
              </h3>
              <span className="text-xs text-[var(--color-text-tertiary)]">
                {formatNumber(totalCallsAcrossUseCases)} calls across{' '}
                {(aiData.byUseCase ?? []).length} use cases
              </span>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              <MetricCard label="Total" value={formatUsd(totalEstimatedCostUsd)} />
              <MetricCard
                label="Top Use Case"
                value={
                  (aiData.byUseCase ?? [])[0]?.useCase ?? '—'
                }
              />
              <MetricCard
                label="Top Use Case Cost"
                value={
                  (aiData.byUseCase ?? [])[0]
                    ? formatUsd((aiData.byUseCase ?? [])[0].estimatedCostUsd)
                    : '—'
                }
              />
              <MetricCard
                label="Avg $/Call"
                value={
                  totalCallsAcrossUseCases > 0
                    ? formatUsd(totalEstimatedCostUsd / totalCallsAcrossUseCases)
                    : '—'
                }
              />
            </div>
          </CardContent>
        </Card>

        {/* Charts row — latency + TTFT side by side */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <TimeSeriesChart
            data={aiData.latencyTimeSeries}
            label="Avg Latency"
            formatValue={(v) => formatMs(v)}
          />
          <TimeSeriesChart
            data={aiData.ttftTimeSeries}
            label="Avg TTFT"
            color="#8b5cf6"
            formatValue={(v) => formatMs(v)}
          />
        </div>

        {/* Token usage chart */}
        <TimeSeriesChart
          data={aiData.tokenTimeSeries}
          label="Token Usage"
          color="#f59e0b"
          formatValue={(v) => formatNumber(v)}
        />

        {/* Client vs Server comparison */}
        {aiData.clientServerComparison && (
          <Card>
            <CardContent className="p-4">
              <h3 className="text-sm font-medium text-[var(--color-text-secondary)] mb-4">
                Client vs Server
              </h3>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <MetricCard
                  label="Client Round-Trip"
                  value={formatMs(aiData.clientServerComparison.avgClientRoundTripMs)}
                />
                <MetricCard
                  label="Server Latency"
                  value={formatMs(aiData.clientServerComparison.avgServerLatencyMs)}
                />
                <MetricCard
                  label="Network Overhead"
                  value={formatMs(aiData.clientServerComparison.avgNetworkOverheadMs)}
                />
                <MetricCard
                  label="Client TTFT"
                  value={formatMs(aiData.clientServerComparison.avgClientTtftMs)}
                />
              </div>
            </CardContent>
          </Card>
        )}

        {/* Per-agent breakdown table */}
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)]">
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Agent
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Runs
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Avg Latency
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    P95 Latency
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Avg TTFT
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    P95 TTFT
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Input Tokens
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Output Tokens
                  </th>
                </tr>
              </thead>
              <tbody>
                {aiData.byAgent.map((agent, idx) => (
                  <tr
                    key={agent.agentName}
                    className={`border-b border-[var(--color-border-light)] ${
                      idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''
                    }`}
                  >
                    <td className="px-4 py-3 text-[var(--color-text-primary)] font-medium">
                      {agent.agentName}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(agent.runs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(agent.avgLatencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(agent.p95LatencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(agent.avgTtftMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(agent.p95TtftMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(agent.totalInputTokens)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(agent.totalOutputTokens)}
                    </td>
                  </tr>
                ))}
                {aiData.byAgent.length === 0 && (
                  <tr>
                    <td
                      colSpan={8}
                      className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                    >
                      No agent data available
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </CardContent>
        </Card>

        {/* Per-use-case breakdown — sourced from AiCallCompleted (covers
            background summariser/projector/tool calls, not just AG-UI). */}
        <Card>
          <CardContent className="p-0">
            <div className="px-4 py-3 border-b border-[var(--color-border-light)]">
              <h3 className="text-sm font-medium text-[var(--color-text-primary)]">
                By Use Case
              </h3>
              <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
                Every LLM call observed by TelemetryChatClient — chat,
                summariser, projector, agent tools.
              </p>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)]">
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Use Case
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Calls
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Avg Latency
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    P95 Latency
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Input Tokens
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Output Tokens
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Est. Cost
                  </th>
                </tr>
              </thead>
              <tbody>
                {(aiData.byUseCase ?? []).map((uc, idx) => (
                  <tr
                    key={uc.useCase}
                    className={`border-b border-[var(--color-border-light)] ${
                      idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''
                    }`}
                  >
                    <td className="px-4 py-3 text-[var(--color-text-primary)] font-medium">
                      {uc.useCase || '(unset)'}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(uc.calls)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(uc.avgLatencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(uc.p95LatencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(uc.totalInputTokens)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(uc.totalOutputTokens)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatUsd(uc.estimatedCostUsd)}
                    </td>
                  </tr>
                ))}
                {(aiData.byUseCase ?? []).length === 0 && (
                  <tr>
                    <td
                      colSpan={7}
                      className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                    >
                      No use-case data yet — TelemetryChatClient emits its
                      first AiCallCompleted log on the next LLM call.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </CardContent>
        </Card>
      </div>
    );
  };

  const renderErrorsTab = () => {
    if (errorsLoading) return <TabLoading />;
    if (errorsError) return <TabError message={errorsError} />;
    if (!errorsData) return null;

    return (
      <div className="space-y-6">
        {!errorsData.configured && <NotConfiguredBanner />}

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <MetricCard
            label="Total Error Groups"
            value={formatNumber(errorsData.errors.length)}
            status={errorsData.errors.length > 0 ? 'critical' : 'good'}
          />
          <MetricCard
            label="Total Occurrences"
            value={formatNumber(errorsData.errors.reduce((sum, e) => sum + e.count, 0))}
          />
        </div>

        {/* Error groups table */}
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)]">
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Type
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Message
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Count
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Last Seen
                  </th>
                </tr>
              </thead>
              <tbody>
                {errorsData.errors.map((error, idx) => (
                  <ErrorRow
                    key={`${error.type}-${idx}`}
                    error={error}
                    expanded={expandedErrors.has(idx)}
                    onToggle={() => toggleErrorExpanded(idx)}
                  />
                ))}
                {errorsData.errors.length === 0 && (
                  <tr>
                    <td
                      colSpan={4}
                      className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                    >
                      No errors recorded
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </CardContent>
        </Card>
      </div>
    );
  };

  const renderDependenciesTab = () => {
    if (depsLoading) return <TabLoading />;
    if (depsError) return <TabError message={depsError} />;
    if (!depsData) return null;

    const sorted = [...depsData.dependencies].sort((a, b) => b.totalCalls - a.totalCalls);

    return (
      <div className="space-y-6">
        {!depsData.configured && <NotConfiguredBanner />}

        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)]">
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Name
                  </th>
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Type
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Success Rate
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Avg Duration
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Total Calls
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Failed
                  </th>
                </tr>
              </thead>
              <tbody>
                {sorted.map((dep, idx) => {
                  let badgeClass = 'bg-emerald-50 text-emerald-700';
                  if (dep.successRatePercent < 95) badgeClass = 'bg-red-50 text-red-600';
                  else if (dep.successRatePercent < 99.5) badgeClass = 'bg-amber-50 text-amber-700';

                  return (
                    <tr
                      key={dep.name}
                      className={`border-b border-[var(--color-border-light)] ${
                        idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''
                      }`}
                    >
                      <td className="px-4 py-3 text-[var(--color-text-primary)] font-medium">
                        {dep.name}
                      </td>
                      <td className="px-4 py-3 text-[var(--color-text-secondary)]">{dep.type}</td>
                      <td className="px-4 py-3 text-right">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${badgeClass}`}
                        >
                          {dep.successRatePercent.toFixed(1)}%
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                        {formatMs(dep.avgDurationMs)}
                      </td>
                      <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                        {formatNumber(dep.totalCalls)}
                      </td>
                      <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                        {formatNumber(dep.failedCalls)}
                      </td>
                    </tr>
                  );
                })}
                {sorted.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                    >
                      No dependency data available
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </CardContent>
        </Card>
      </div>
    );
  };

  const renderJobsTab = () => {
    if (jobsLoading) return <TabLoading />;
    if (jobsError) return <TabError message={jobsError} />;
    if (!jobsData) return null;

    const totalExecutions = jobsData.jobs.reduce((sum, j) => sum + j.total, 0);
    const totalSuccesses = jobsData.jobs.reduce((sum, j) => sum + j.successes, 0);
    const successRate = totalExecutions > 0 ? (totalSuccesses / totalExecutions) * 100 : 0;
    const avgDuration =
      jobsData.jobs.length > 0
        ? jobsData.jobs.reduce((sum, j) => sum + j.avgDurationMs, 0) / jobsData.jobs.length
        : 0;

    return (
      <div className="space-y-6">
        {!jobsData.configured && <NotConfiguredBanner />}

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <MetricCard
            label="Total Executions"
            value={formatNumber(totalExecutions)}
          />
          <MetricCard
            label="Success Rate"
            value={formatPercent(successRate)}
            status={successRate >= 99 ? 'good' : successRate >= 95 ? 'warning' : 'critical'}
          />
          <MetricCard label="Avg Duration" value={formatMs(avgDuration)} />
        </div>

        {/* Jobs table */}
        <Card>
          <CardContent className="p-0">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)]">
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Job Name
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Total
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Successes
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Failures
                  </th>
                  <th className="px-4 py-3 text-right font-medium text-[var(--color-text-secondary)]">
                    Avg Duration
                  </th>
                </tr>
              </thead>
              <tbody>
                {jobsData.jobs.map((job, idx) => {
                  return (
                    <tr
                      key={job.jobName}
                      className={`border-b border-[var(--color-border-light)] ${
                        idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''
                      }`}
                    >
                      <td className="px-4 py-3 text-[var(--color-text-primary)] font-medium">
                        {job.jobName}
                      </td>
                      <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                        {formatNumber(job.total)}
                      </td>
                      <td className="px-4 py-3 text-right text-emerald-600">
                        {formatNumber(job.successes)}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <span className={job.failures > 0 ? 'text-red-600 font-medium' : 'text-[var(--color-text-primary)]'}>
                          {formatNumber(job.failures)}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                        {formatMs(job.avgDurationMs)}
                      </td>
                    </tr>
                  );
                })}
                {jobsData.jobs.length === 0 && (
                  <tr>
                    <td
                      colSpan={5}
                      className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                    >
                      No job execution data available
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </CardContent>
        </Card>
      </div>
    );
  };

  // -----------------------------------------------------------------------
  // Main render
  // -----------------------------------------------------------------------

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
          <Breadcrumb
            items={[
              { label: 'Admin' },
              {
                label: 'Observability',
                icon: <Activity className="h-4 w-4" />,
              },
            ]}
            className="mb-3"
          />
          <div className="flex items-start justify-between">
            <div>
              <h1 className="text-xl font-semibold text-[var(--color-text-primary)]">
                Observability
              </h1>
              <p className="text-sm text-[var(--color-text-secondary)] mt-1">
                Monitor platform health, AI performance, errors, and dependencies.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <Select value={timeRange} onValueChange={setTimeRange}>
                <SelectTrigger className="w-[180px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TIME_RANGE_OPTIONS.map((opt) => (
                    <SelectItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <Button variant="outline" size="icon" onClick={handleRefresh}>
                <RotateCcw className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={setActiveTab}>
        <div className="border-b border-[var(--color-border-light)] px-6">
          <TabsList className="bg-transparent p-0 h-auto flex flex-wrap gap-0">
            <TabsTrigger
              value="overview"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              Overview
            </TabsTrigger>
            <TabsTrigger
              value="ai"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              AI
            </TabsTrigger>
            <TabsTrigger
              value="errors"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              Errors
            </TabsTrigger>
            <TabsTrigger
              value="dependencies"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              Dependencies
            </TabsTrigger>
            <TabsTrigger
              value="jobs"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              Jobs
            </TabsTrigger>
          </TabsList>
        </div>

        <div className="p-6 overflow-auto flex-1">
          <TabsContent value="overview" className="mt-0">
            {renderOverviewTab()}
          </TabsContent>
          <TabsContent value="ai" className="mt-0">
            {renderAiTab()}
          </TabsContent>
          <TabsContent value="errors" className="mt-0">
            {renderErrorsTab()}
          </TabsContent>
          <TabsContent value="dependencies" className="mt-0">
            {renderDependenciesTab()}
          </TabsContent>
          <TabsContent value="jobs" className="mt-0">
            {renderJobsTab()}
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
}
