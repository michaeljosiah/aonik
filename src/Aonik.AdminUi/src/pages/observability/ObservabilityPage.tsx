import { useState, useEffect, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
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
  type ErrorDetailResponse,
  type DependencyMetricsResponse,
  type AiPerformanceResponse,
  type JobMetricsResponse,
  type RetrievalResponse,
  type TopologyResponse,
} from '@/services/observabilityService';
import { RetrievalTab } from './RetrievalTab';
import { TopologyTab } from './TopologyTab';
import {
  PanelInfoPopover,
  type PanelCallout,
} from '@/components/ui/panel-info-popover';

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

function formatPhaseLabel(phaseName: string): string {
  switch (phaseName) {
    case 'request_to_first_token':
      return 'Request → First Token';
    case 'user_brief':
      return 'User Brief';
    case 'history_load':
      return 'History Load';
    case 'run_started_sse':
      return 'RUN_STARTED SSE';
    case 'first_token_sse':
      return 'First Token SSE';
    default:
      return phaseName;
  }
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

/**
 * Trigger a CSV download of `rows` (array of objects) named `filename`.
 * Quotes every field defensively — handles strings with commas/newlines/quotes.
 */
function downloadCsv(filename: string, rows: Array<Record<string, unknown>>) {
  if (rows.length === 0) return;
  const headers = Object.keys(rows[0]);
  const escape = (value: unknown) => {
    if (value === null || value === undefined) return '';
    const s = String(value);
    return /[",\n\r]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const csv = [
    headers.join(','),
    ...rows.map((r) => headers.map((h) => escape(r[h])).join(',')),
  ].join('\n');
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
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

/// State for a single error-detail fetch. Kept narrow so the page can
/// share one cache across every expanded row and we don't refetch when
/// a user collapses and re-expands the same row.
type ErrorDetailState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'loaded'; detail: ErrorDetailResponse }
  | { status: 'error'; message: string };

function ErrorRow({
  error,
  expanded,
  onToggle,
  detailState,
}: {
  error: ErrorGroup;
  expanded: boolean;
  onToggle: () => void;
  detailState: ErrorDetailState;
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
          {error.method && (
            <div className="text-[11px] text-[var(--color-text-tertiary)] font-mono truncate mt-0.5">
              at {error.method}
            </div>
          )}
        </td>
        <td className="px-4 py-3 text-sm text-[var(--color-text-primary)] text-right font-medium">
          {error.count}
        </td>
        <td className="px-4 py-3 text-sm text-[var(--color-text-tertiary)] text-right whitespace-nowrap">
          {relativeTime(error.lastSeen)}
        </td>
      </tr>
      {expanded && (
        <tr>
          <td colSpan={4} className="px-4 py-2">
            <ErrorDetailPanel error={error} detailState={detailState} />
          </td>
        </tr>
      )}
    </>
  );
}

/// Expanded panel showing the row-level summary plus the lazily-fetched
/// detail (parsed stack, operation id, custom dimensions). Keeps each
/// section conditional so the panel degrades gracefully when older
/// exceptions lack a problemId or App Insights returns a partial payload.
function ErrorDetailPanel({
  error,
  detailState,
}: {
  error: ErrorGroup;
  detailState: ErrorDetailState;
}) {
  const detail = detailState.status === 'loaded' ? detailState.detail : null;

  return (
    <div className="text-xs bg-[var(--color-surface-inset)] p-3 rounded-md space-y-3">
      {/* Header summary always available from the list row itself */}
      {error.innermostMessage && (
        <div>
          <span className="text-[var(--color-text-tertiary)]">Innermost: </span>
          <span className="text-[var(--color-text-primary)]">
            {error.innermostMessage}
          </span>
        </div>
      )}

      <div className="flex flex-wrap gap-x-6 gap-y-1 text-[var(--color-text-secondary)]">
        {error.roles && error.roles.length > 0 && (
          <span>
            <span className="text-[var(--color-text-tertiary)]">Services: </span>
            <span className="font-mono text-[var(--color-text-primary)]">
              {error.roles.join(', ')}
            </span>
          </span>
        )}
        {error.operations && error.operations.length > 0 && (
          <span className="max-w-full">
            <span className="text-[var(--color-text-tertiary)]">
              Operations:{' '}
            </span>
            <span className="font-mono text-[var(--color-text-primary)]">
              {error.operations.join(', ')}
            </span>
          </span>
        )}
        {error.sampleOperationId && (
          <span>
            <span className="text-[var(--color-text-tertiary)]">
              Sample operation:{' '}
            </span>
            <span className="font-mono text-[var(--color-text-primary)]">
              {error.sampleOperationId}
            </span>
          </span>
        )}
      </div>

      {/* Detail payload — lazily loaded on expand */}
      {detailState.status === 'loading' && (
        <div className="flex items-center gap-2 text-[var(--color-text-tertiary)]">
          <Loader2 className="h-3.5 w-3.5 animate-spin" />
          Loading exception details…
        </div>
      )}

      {detailState.status === 'error' && (
        <div className="text-[var(--color-error)]">
          Couldn't load details: {detailState.message}
        </div>
      )}

      {detail && !detail.found && (
        <div className="text-[var(--color-text-tertiary)]">
          No sample exception is retained in the active time range. Widen the
          time range to pull a fresh sample.
        </div>
      )}

      {detail?.found && (
        <div className="space-y-3">
          {detail.parsedStack.length > 0 ? (
            <div>
              <div className="text-[var(--color-text-tertiary)] mb-1">
                Stack trace
              </div>
              <pre className="whitespace-pre-wrap break-words font-mono text-[11px] leading-relaxed text-[var(--color-text-primary)] bg-[var(--color-surface)] p-2 rounded border border-[var(--color-border-light)] overflow-x-auto">
                {detail.parsedStack
                  .map((frame) => {
                    const method = frame.method ?? '<unknown>';
                    const location =
                      frame.fileName && frame.line != null
                        ? ` (${frame.fileName}:${frame.line})`
                        : frame.fileName
                          ? ` (${frame.fileName})`
                          : '';
                    const asm = frame.assembly ? ` — ${frame.assembly}` : '';
                    return `  at ${method}${location}${asm}`;
                  })
                  .join('\n')}
              </pre>
            </div>
          ) : (
            <div className="text-[var(--color-text-tertiary)]">
              No parsed stack trace was captured for this exception.
            </div>
          )}

          {Object.keys(detail.customDimensions).length > 0 && (
            <div>
              <div className="text-[var(--color-text-tertiary)] mb-1">
                Custom dimensions
              </div>
              <div className="grid grid-cols-[max-content_minmax(0,1fr)] gap-x-4 gap-y-0.5 font-mono text-[11px]">
                {Object.entries(detail.customDimensions).map(([k, v]) => (
                  <div key={k} className="contents">
                    <div className="text-[var(--color-text-tertiary)]">{k}</div>
                    <div className="text-[var(--color-text-primary)] break-all">
                      {v}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
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

const OBSERVABILITY_TABS = new Set(['overview', 'ai', 'errors', 'dependencies', 'jobs', 'retrieval', 'topology']);

function normalizeTab(value: string | null): string {
  return value && OBSERVABILITY_TABS.has(value) ? value : 'overview';
}

function normalizeTimeRange(value: string | null): string {
  return TIME_RANGE_OPTIONS.some((option) => option.value === value) ? value! : '24h';
}

export function ObservabilityPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [timeRange, setTimeRange] = useState(() => normalizeTimeRange(searchParams.get('timeRange')));
  const [activeTab, setActiveTab] = useState(() => normalizeTab(searchParams.get('tab')));
  const operationIdFilter = searchParams.get('operationId');

  // --- Overview state ---
  const [overview, setOverview] = useState<ObservabilityOverviewResponse | null>(null);
  const [overviewLoading, setOverviewLoading] = useState(false);
  const [overviewError, setOverviewError] = useState<string | null>(null);

  // --- Errors state ---
  const [errorsData, setErrorsData] = useState<ErrorsResponse | null>(null);
  const [errorsLoading, setErrorsLoading] = useState(false);
  const [errorsError, setErrorsError] = useState<string | null>(null);
  const [expandedErrors, setExpandedErrors] = useState<Set<number>>(new Set());
  // Drill-down detail cache, keyed by `problemId`. Populated lazily when
  // a row is expanded so we never pay the App Insights detail query for
  // rows the user isn't looking at. Rows without a problemId stay on the
  // summary view only.
  const [errorDetails, setErrorDetails] = useState<
    Map<string, ErrorDetailState>
  >(new Map());

  // Errors tab facet + mute state. Mutes persist in localStorage so a
  // noisy-but-known error can stay hidden across reloads without touching
  // the backend; we still count them in the rail so they're never invisible.
  const [errorTypeFilter, setErrorTypeFilter] = useState<string | null>(null);
  const [mutedErrorTypes, setMutedErrorTypes] = useState<Set<string>>(() => {
    if (typeof window === 'undefined') return new Set();
    try {
      const stored = window.localStorage.getItem('observability:mutedErrorTypes');
      return stored ? new Set(JSON.parse(stored) as string[]) : new Set();
    } catch {
      return new Set();
    }
  });
  const persistMutes = (next: Set<string>) => {
    setMutedErrorTypes(next);
    try {
      window.localStorage.setItem(
        'observability:mutedErrorTypes',
        JSON.stringify(Array.from(next)),
      );
    } catch {
      /* localStorage disabled — non-fatal, mutes won't survive reload */
    }
  };
  const toggleMute = (type: string) => {
    const next = new Set(mutedErrorTypes);
    if (next.has(type)) next.delete(type);
    else next.add(type);
    persistMutes(next);
  };

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
    const nextTab = normalizeTab(searchParams.get('tab'));
    const nextRange = normalizeTimeRange(searchParams.get('timeRange'));
    setActiveTab((current) => (current === nextTab ? current : nextTab));
    setTimeRange((current) => (current === nextRange ? current : nextRange));
  }, [searchParams]);

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

  // --- Retrieval state ---
  const [retrievalData, setRetrievalData] = useState<RetrievalResponse | null>(null);
  const [retrievalLoading, setRetrievalLoading] = useState(false);
  const [retrievalError, setRetrievalError] = useState<string | null>(null);

  // --- Topology state ---
  const [topologyData, setTopologyData] = useState<TopologyResponse | null>(null);
  const [topologyLoading, setTopologyLoading] = useState(false);
  const [topologyError, setTopologyError] = useState<string | null>(null);

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

  const fetchErrors = useCallback(async (tr: string, operationId?: string | null) => {
    setErrorsLoading(true);
    setErrorsError(null);
    try {
      const data = await observabilityService.getErrors(tr, operationId);
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

  const fetchRetrieval = useCallback(async (tr: string) => {
    setRetrievalLoading(true);
    setRetrievalError(null);
    try {
      const data = await observabilityService.getRetrieval(tr);
      setRetrievalData(data);
    } catch (err) {
      setRetrievalError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setRetrievalLoading(false);
    }
  }, []);

  const fetchTopology = useCallback(async (tr: string) => {
    setTopologyLoading(true);
    setTopologyError(null);
    try {
      const data = await observabilityService.getTopology(tr);
      setTopologyData(data);
    } catch (err) {
      setTopologyError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setTopologyLoading(false);
    }
  }, []);

  // Fetch on tab activation + time range change
  useEffect(() => {
    if (activeTab === 'overview') fetchOverview(timeRange);
    if (activeTab === 'errors') fetchErrors(timeRange, operationIdFilter);
    if (activeTab === 'ai') fetchAi(timeRange);
    if (activeTab === 'dependencies') fetchDeps(timeRange);
    if (activeTab === 'jobs') fetchJobs(timeRange);
    if (activeTab === 'retrieval') fetchRetrieval(timeRange);
    if (activeTab === 'topology') fetchTopology(timeRange);
  }, [activeTab, timeRange, operationIdFilter, fetchOverview, fetchErrors, fetchAi, fetchDeps, fetchJobs, fetchRetrieval, fetchTopology]);

  // Refresh handler
  const handleRefresh = () => {
    if (activeTab === 'overview') fetchOverview(timeRange);
    if (activeTab === 'errors') fetchErrors(timeRange, operationIdFilter);
    if (activeTab === 'ai') fetchAi(timeRange);
    if (activeTab === 'dependencies') fetchDeps(timeRange);
    if (activeTab === 'jobs') fetchJobs(timeRange);
    if (activeTab === 'retrieval') fetchRetrieval(timeRange);
    if (activeTab === 'topology') fetchTopology(timeRange);
  };

  /// Lazily fetches detail for a single error group. Idempotent: multiple
  /// calls for the same problemId while loading collapse to one HTTP
  /// request, and completed results stay cached for the session so
  /// collapsing/re-expanding the row is free.
  const ensureErrorDetail = useCallback(
    (problemId: string | null | undefined, tr: string) => {
      if (!problemId) return;
      setErrorDetails((prev) => {
        const existing = prev.get(problemId);
        if (existing && existing.status !== 'idle') return prev;
        const next = new Map(prev);
        next.set(problemId, { status: 'loading' });
        return next;
      });

      observabilityService
        .getErrorDetail(problemId, tr)
        .then((detail) => {
          setErrorDetails((prev) => {
            const next = new Map(prev);
            next.set(problemId, { status: 'loaded', detail });
            return next;
          });
        })
        .catch((err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          setErrorDetails((prev) => {
            const next = new Map(prev);
            next.set(problemId, { status: 'error', message });
            return next;
          });
        });
    },
    [],
  );

  const toggleErrorExpanded = (idx: number, error: ErrorGroup) => {
    setExpandedErrors((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) {
        next.delete(idx);
      } else {
        next.add(idx);
        // Kick off the detail fetch on first expand only; cached on subsequent.
        ensureErrorDetail(error.problemId ?? null, timeRange);
      }
      return next;
    });
  };

  useEffect(() => {
    if (activeTab !== 'errors' || !operationIdFilter || !errorsData || errorsData.errors.length === 0) return;

    setExpandedErrors(new Set([0]));
    ensureErrorDetail(errorsData.errors[0].problemId ?? null, timeRange);
  }, [activeTab, errorsData, ensureErrorDetail, operationIdFilter, timeRange]);

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
    const pfStreaming = aiData.personalFinanceStreaming;
    const pfPhase = (name: string) =>
      pfStreaming?.phases.find((phase) => phase.phaseName === name) ?? null;
    const pfCache = (name: string) =>
      pfStreaming?.caches.find((cache) => cache.cacheName === name) ?? null;
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

        {/* Watchdog status — mirrors AiCostGuardJob's threshold so we can
            see "are we close to tripping the alarm?" without leaving the
            tab. Threshold default ($5/hr) lives in worker appsettings. */}
        {(() => {
          const watchdogThreshold = 5.0;
          const ratio = watchdogThreshold > 0
            ? totalEstimatedCostUsd / watchdogThreshold
            : 0;
          const status: 'good' | 'warning' | 'critical' =
            ratio >= 1 ? 'critical' : ratio >= 0.6 ? 'warning' : 'good';
          return (
            <Card className={
              status === 'critical'
                ? 'border-l-4 border-l-red-500'
                : status === 'warning'
                ? 'border-l-4 border-l-amber-500'
                : 'border-l-4 border-l-green-500'
            }>
              <CardContent className="p-4">
                <div className="flex items-start justify-between gap-4">
                  <div>
                    <h3 className="text-sm font-medium text-[var(--color-text-primary)]">
                      AI Cost Watchdog
                    </h3>
                    <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
                      Quartz job <code>AiCostGuardJob</code> trips when
                      estimated spend exceeds ${watchdogThreshold.toFixed(2)}
                      {' '}per hour. Tripped events surface as{' '}
                      <code>AiCostGuardTripped</code> errors.
                    </p>
                  </div>
                  <span className={`text-xs font-semibold px-2 py-1 rounded ${
                    status === 'critical'
                      ? 'bg-red-500/10 text-red-500'
                      : status === 'warning'
                      ? 'bg-amber-500/10 text-amber-500'
                      : 'bg-green-500/10 text-green-500'
                  }`}>
                    {status === 'critical'
                      ? 'TRIPPED'
                      : status === 'warning'
                      ? 'NEAR LIMIT'
                      : 'OK'}
                  </span>
                </div>
                <div className="mt-3 h-2 w-full rounded-full bg-[var(--color-surface-inset)] overflow-hidden">
                  <div
                    className={
                      status === 'critical'
                        ? 'h-full bg-red-500'
                        : status === 'warning'
                        ? 'h-full bg-amber-500'
                        : 'h-full bg-green-500'
                    }
                    style={{ width: `${Math.min(100, ratio * 100).toFixed(1)}%` }}
                  />
                </div>
                <p className="mt-2 text-xs text-[var(--color-text-tertiary)]">
                  {formatUsd(totalEstimatedCostUsd)} of{' '}
                  {formatUsd(watchdogThreshold)} ({(ratio * 100).toFixed(0)}%)
                </p>
              </CardContent>
            </Card>
          );
        })()}

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

        {/* Per-model breakdown — cost + volume by underlying model. */}
        <Card>
          <CardContent className="p-0">
            <div className="px-4 py-3 border-b border-[var(--color-border-light)] flex items-start justify-between gap-3">
              <div>
                <h3 className="text-sm font-medium text-[var(--color-text-primary)]">
                  By Model
                </h3>
                <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
                  Resolved from <code>customDimensions.ActualModel</code>,
                  falling back to the requested model when the provider does
                  not echo one back.
                </p>
              </div>
              <button
                type="button"
                onClick={() =>
                  downloadCsv('ai-by-model.csv', (aiData.byModel ?? []) as unknown as Array<Record<string, unknown>>)
                }
                disabled={(aiData.byModel ?? []).length === 0}
                className="text-xs px-2 py-1 rounded border border-[var(--color-border-light)] text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)] disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Export CSV
              </button>
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[var(--color-border-light)]">
                  <th className="px-4 py-3 text-left font-medium text-[var(--color-text-secondary)]">
                    Model
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
                {(aiData.byModel ?? []).map((m, idx) => (
                  <tr
                    key={m.model}
                    className={`border-b border-[var(--color-border-light)] ${
                      idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''
                    }`}
                  >
                    <td className="px-4 py-3 text-[var(--color-text-primary)] font-medium">
                      {m.model || '(unknown)'}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(m.calls)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(m.avgLatencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatMs(m.p95LatencyMs)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(m.totalInputTokens)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatNumber(m.totalOutputTokens)}
                    </td>
                    <td className="px-4 py-3 text-right text-[var(--color-text-primary)]">
                      {formatUsd(m.estimatedCostUsd)}
                    </td>
                  </tr>
                ))}
                {(aiData.byModel ?? []).length === 0 && (
                  <tr>
                    <td
                      colSpan={7}
                      className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                    >
                      No model data yet.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
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

        {pfStreaming && (
          <Card>
            <CardContent className="p-4 space-y-4">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <h3 className="text-sm font-medium text-[var(--color-text-primary)]">
                    Personal Finance Streaming Diagnostics
                  </h3>
                  <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
                    AG-UI request phases for <code>{pfStreaming.agentName}</code>.
                    This breaks first-token latency into pre-stream work instead of
                    treating the provider call as a black box.
                  </p>
                </div>
                <span className="text-xs text-[var(--color-text-tertiary)]">
                  {pfStreaming.threadModes.reduce((sum, mode) => sum + mode.runs, 0)} runs
                </span>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
                <MetricCard
                  label="P50 First Token"
                  value={pfPhase('request_to_first_token') ? formatMs(pfPhase('request_to_first_token')!.p50Ms) : '—'}
                />
                <MetricCard
                  label="P95 First Token"
                  value={pfPhase('request_to_first_token') ? formatMs(pfPhase('request_to_first_token')!.p95Ms) : '—'}
                  status={pfPhase('request_to_first_token') ? latencyStatus(pfPhase('request_to_first_token')!.p95Ms) : undefined}
                />
                <MetricCard
                  label="P95 User Brief"
                  value={pfPhase('user_brief') ? formatMs(pfPhase('user_brief')!.p95Ms) : '—'}
                />
                <MetricCard
                  label="P95 History Load"
                  value={pfPhase('history_load') ? formatMs(pfPhase('history_load')!.p95Ms) : '—'}
                />
                <MetricCard
                  label="User Brief Hit Rate"
                  value={pfCache('user_brief') ? formatPercent(pfCache('user_brief')!.hitRatePercent) : '—'}
                />
                <MetricCard
                  label="History Cache Hit Rate"
                  value={pfCache('history') ? formatPercent(pfCache('history')!.hitRatePercent) : '—'}
                />
              </div>

              {pfStreaming.phaseTimeSeries.some((series) => series.points.length > 0) && (
                <MultiLineChart
                  series={pfStreaming.phaseTimeSeries
                    .filter((series) => series.points.length > 0)
                    .map((series, index) => ({
                      key: series.phaseName,
                      label: formatPhaseLabel(series.phaseName),
                      color: ['#8b5cf6', '#0ea5e9', '#f59e0b', '#10b981'][index % 4],
                      data: series.points,
                    }))}
                  label="PF Streaming Phases"
                  height={180}
                  formatValue={(value) => formatMs(value)}
                />
              )}

              <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)] mb-2">
                    Thread Modes
                  </h4>
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-[var(--color-border-light)]">
                        <th className="px-3 py-2 text-left font-medium text-[var(--color-text-secondary)]">Mode</th>
                        <th className="px-3 py-2 text-right font-medium text-[var(--color-text-secondary)]">Runs</th>
                        <th className="px-3 py-2 text-right font-medium text-[var(--color-text-secondary)]">Avg First Token</th>
                        <th className="px-3 py-2 text-right font-medium text-[var(--color-text-secondary)]">P95 First Token</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pfStreaming.threadModes.map((mode, idx) => (
                        <tr
                          key={mode.mode}
                          className={`border-b border-[var(--color-border-light)] ${idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''}`}
                        >
                          <td className="px-3 py-2 text-[var(--color-text-primary)] font-medium">{mode.mode}</td>
                          <td className="px-3 py-2 text-right text-[var(--color-text-primary)]">{formatNumber(mode.runs)}</td>
                          <td className="px-3 py-2 text-right text-[var(--color-text-primary)]">{formatMs(mode.avgRequestToFirstTokenMs)}</td>
                          <td className="px-3 py-2 text-right text-[var(--color-text-primary)]">{formatMs(mode.p95RequestToFirstTokenMs)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <div>
                  <h4 className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-tertiary)] mb-2">
                    History Sources
                  </h4>
                  <table className="w-full text-sm">
                    <thead>
                      <tr className="border-b border-[var(--color-border-light)]">
                        <th className="px-3 py-2 text-left font-medium text-[var(--color-text-secondary)]">Source</th>
                        <th className="px-3 py-2 text-right font-medium text-[var(--color-text-secondary)]">Runs</th>
                        <th className="px-3 py-2 text-right font-medium text-[var(--color-text-secondary)]">Avg First Token</th>
                        <th className="px-3 py-2 text-right font-medium text-[var(--color-text-secondary)]">P95 First Token</th>
                      </tr>
                    </thead>
                    <tbody>
                      {pfStreaming.historySources.map((source, idx) => (
                        <tr
                          key={source.mode}
                          className={`border-b border-[var(--color-border-light)] ${idx % 2 === 1 ? 'bg-[var(--color-surface-inset)]' : ''}`}
                        >
                          <td className="px-3 py-2 text-[var(--color-text-primary)] font-medium">{source.mode}</td>
                          <td className="px-3 py-2 text-right text-[var(--color-text-primary)]">{formatNumber(source.runs)}</td>
                          <td className="px-3 py-2 text-right text-[var(--color-text-primary)]">{formatMs(source.avgRequestToFirstTokenMs)}</td>
                          <td className="px-3 py-2 text-right text-[var(--color-text-primary)]">{formatMs(source.p95RequestToFirstTokenMs)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
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
            <div className="px-4 py-3 border-b border-[var(--color-border-light)] flex items-start justify-between gap-3">
              <div>
                <h3 className="text-sm font-medium text-[var(--color-text-primary)]">
                  By Use Case
                </h3>
                <p className="text-xs text-[var(--color-text-tertiary)] mt-0.5">
                  Every LLM call observed by TelemetryChatClient — chat,
                  summariser, projector, agent tools.
                </p>
              </div>
              <button
                type="button"
                onClick={() =>
                  downloadCsv('ai-by-use-case.csv', (aiData.byUseCase ?? []) as unknown as Array<Record<string, unknown>>)
                }
                disabled={(aiData.byUseCase ?? []).length === 0}
                className="text-xs px-2 py-1 rounded border border-[var(--color-border-light)] text-[var(--color-text-secondary)] hover:bg-[var(--color-surface-inset)] disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Export CSV
              </button>
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

    // Aggregate counts per error type for the faceted rail.
    const facetCounts = new Map<string, number>();
    for (const e of errorsData.errors) {
      facetCounts.set(e.type, (facetCounts.get(e.type) ?? 0) + e.count);
    }
    const facets = Array.from(facetCounts.entries())
      .sort((a, b) => b[1] - a[1]);

    // Apply filter + mute set to the visible rows.
    const visibleErrors = errorsData.errors.filter((e) => {
      if (mutedErrorTypes.has(e.type)) return false;
      if (errorTypeFilter && e.type !== errorTypeFilter) return false;
      return true;
    });

    const totalUnmutedOccurrences = errorsData.errors
      .filter((e) => !mutedErrorTypes.has(e.type))
      .reduce((sum, e) => sum + e.count, 0);
    const totalMutedOccurrences = errorsData.errors
      .filter((e) => mutedErrorTypes.has(e.type))
      .reduce((sum, e) => sum + e.count, 0);

    return (
      <div className="space-y-6">
        {!errorsData.configured && <NotConfiguredBanner />}

        {operationIdFilter && (
          <Card className="border-l-4 border-l-blue-500">
            <CardContent className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <div className="text-sm font-medium text-[var(--color-text-primary)]">
                  Filtered to operation
                </div>
                <div className="mt-1 font-mono text-xs break-all text-[var(--color-text-secondary)]">
                  {operationIdFilter}
                </div>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={() => updateQuery({ operationId: null })}
              >
                Clear operation
              </Button>
            </CardContent>
          </Card>
        )}

        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          <MetricCard
            label="Active Error Groups"
            value={formatNumber(
              errorsData.errors.filter((e) => !mutedErrorTypes.has(e.type)).length,
            )}
            status={
              errorsData.errors.filter((e) => !mutedErrorTypes.has(e.type)).length > 0
                ? 'critical'
                : 'good'
            }
          />
          <MetricCard
            label="Active Occurrences"
            value={formatNumber(totalUnmutedOccurrences)}
          />
          <MetricCard
            label="Muted Occurrences"
            value={formatNumber(totalMutedOccurrences)}
          />
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-[220px_minmax(0,1fr)] gap-4">
          {/* Faceted rail — click to filter, double-click muted to unmute. */}
          <Card>
            <CardContent className="p-3 space-y-1">
              <div className="flex items-center justify-between mb-2">
                <h4 className="text-xs font-semibold uppercase tracking-wide text-[var(--color-text-secondary)]">
                  Type
                </h4>
                {errorTypeFilter && (
                  <button
                    type="button"
                    className="text-xs text-[var(--color-link)] hover:underline"
                    onClick={() => setErrorTypeFilter(null)}
                  >
                    Clear
                  </button>
                )}
              </div>
              {facets.map(([type, count]) => {
                const muted = mutedErrorTypes.has(type);
                const active = errorTypeFilter === type;
                return (
                  <div
                    key={type}
                    className={`group flex items-center justify-between gap-2 px-2 py-1.5 rounded-md text-xs cursor-pointer ${
                      active
                        ? 'bg-[var(--color-surface-hover)]'
                        : 'hover:bg-[var(--color-surface-inset)]'
                    } ${muted ? 'opacity-50' : ''}`}
                    onClick={() =>
                      setErrorTypeFilter((prev) => (prev === type ? null : type))
                    }
                  >
                    <span className="font-mono truncate text-[var(--color-text-primary)]">
                      {type}
                    </span>
                    <span className="flex items-center gap-2">
                      <span className="text-[var(--color-text-tertiary)] tabular-nums">
                        {formatNumber(count)}
                      </span>
                      <button
                        type="button"
                        title={muted ? 'Unmute' : 'Mute'}
                        className="opacity-0 group-hover:opacity-100 text-[10px] text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
                        onClick={(ev) => {
                          ev.stopPropagation();
                          toggleMute(type);
                        }}
                      >
                        {muted ? '🔔' : '🔕'}
                      </button>
                    </span>
                  </div>
                );
              })}
              {facets.length === 0 && (
                <div className="text-xs text-[var(--color-text-tertiary)] py-3 text-center">
                  No error types
                </div>
              )}
            </CardContent>
          </Card>

          {/* Error groups table — filtered + muted view */}
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
                  {visibleErrors.map((error, idx) => (
                    <ErrorRow
                      key={`${error.problemId ?? error.type}-${idx}`}
                      error={error}
                      expanded={expandedErrors.has(idx)}
                      onToggle={() => toggleErrorExpanded(idx, error)}
                      detailState={
                        error.problemId
                          ? errorDetails.get(error.problemId) ?? {
                              status: 'idle',
                            }
                          : { status: 'idle' }
                      }
                    />
                  ))}
                  {visibleErrors.length === 0 && (
                    <tr>
                      <td
                        colSpan={4}
                        className="px-4 py-8 text-center text-[var(--color-text-tertiary)]"
                      >
                        {errorTypeFilter || mutedErrorTypes.size > 0
                          ? 'No errors match the active filter / mute set.'
                          : 'No errors recorded'}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </CardContent>
          </Card>
        </div>
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

  const renderRetrievalTab = () => {
    if (retrievalLoading) return <TabLoading />;
    if (retrievalError) return <TabError message={retrievalError} />;
    if (!retrievalData) return null;
    return (
      <div className="space-y-6">
        {!retrievalData.configured && <NotConfiguredBanner />}
        <RetrievalTab data={retrievalData} />
      </div>
    );
  };

  const renderTopologyTab = () => {
    if (topologyLoading) return <TabLoading />;
    if (topologyError) return <TabError message={topologyError} />;
    if (!topologyData) return null;
    return (
      <div className="space-y-6">
        {!topologyData.configured && <NotConfiguredBanner />}
        <TopologyTab data={topologyData} />
      </div>
    );
  };

  // -----------------------------------------------------------------------
  // Callouts for tab info popovers (derived from loaded data)
  // -----------------------------------------------------------------------

  const overviewCallouts: PanelCallout[] = [];
  if (overview?.errors) {
    const rate = overview.errors.errorRatePercent;
    overviewCallouts.push({
      level: rate < 1 ? 'good' : rate < 5 ? 'warning' : 'critical',
      message: (
        <>
          <strong>Error rate</strong> is {rate.toFixed(1)}%
          {rate < 1
            ? ' — healthy.'
            : rate < 5
            ? ' — elevated, investigate.'
            : ' — critical, immediate attention needed.'}
        </>
      ),
    });
  }
  if (overview?.latency) {
    const p95 = overview.latency.p95Ms;
    overviewCallouts.push({
      level: p95 < 500 ? 'good' : p95 < 2000 ? 'warning' : 'critical',
      message: (
        <>
          <strong>P95 latency</strong> is {formatMs(p95)}
          {p95 < 500
            ? ' — responsive.'
            : p95 < 2000
            ? ' — slow, check dependencies.'
            : ' — critically slow.'}
        </>
      ),
    });
  }
  if (overview?.requests) {
    overviewCallouts.push({
      level: 'info',
      message: (
        <>
          {formatNumber(overview.requests.total)} total requests at{' '}
          {overview.requests.ratePerMinute.toFixed(1)} req/min.
        </>
      ),
    });
  }

  const aiCallouts: PanelCallout[] = [];
  if (aiData) {
    const totalCostAi = (aiData.byUseCase ?? []).reduce(
      (s, uc) => s + (uc.estimatedCostUsd ?? 0),
      0,
    );
    const watchdogThresholdAi = 5.0;
    const ratioAi = watchdogThresholdAi > 0 ? totalCostAi / watchdogThresholdAi : 0;
    const fmtUsd = (v: number) => (v >= 1 ? `$${v.toFixed(2)}` : `$${v.toFixed(4)}`);
    aiCallouts.push({
      level: ratioAi >= 1 ? 'critical' : ratioAi >= 0.6 ? 'warning' : 'good',
      message: (
        <>
          Estimated cost is <strong>{fmtUsd(totalCostAi)}</strong> against a{' '}
          {fmtUsd(watchdogThresholdAi)} hourly watchdog threshold (
          {(ratioAi * 100).toFixed(0)}%).
        </>
      ),
    });
    if (aiData.latency) {
      const p95ai = aiData.latency.p95Ms;
      aiCallouts.push({
        level: latencyStatus(p95ai),
        message: (
          <>
            <strong>P95 LLM latency</strong> is {formatMs(p95ai)}.
          </>
        ),
      });
    }
  }

  const errorsCallouts: PanelCallout[] = [];
  if (errorsData) {
    const activeGroups = errorsData.errors.filter(
      (e) => !mutedErrorTypes.has(e.type),
    );
    const activeOccurrences = activeGroups.reduce((s, e) => s + e.count, 0);
    errorsCallouts.push({
      level: activeGroups.length === 0 ? 'good' : 'critical',
      message:
        activeGroups.length === 0 ? (
          <>No active error groups in the selected window.</>
        ) : (
          <>
            <strong>{activeGroups.length}</strong> active error{' '}
            {activeGroups.length === 1 ? 'group' : 'groups'} with{' '}
            {formatNumber(activeOccurrences)} occurrences.
          </>
        ),
    });
    if (mutedErrorTypes.size > 0) {
      errorsCallouts.push({
        level: 'info',
        message: (
          <>
            {mutedErrorTypes.size} error{' '}
            {mutedErrorTypes.size === 1 ? 'type' : 'types'} muted.
          </>
        ),
      });
    }
  }

  const depsCallouts: PanelCallout[] = [];
  if (depsData) {
    const criticalDeps = depsData.dependencies.filter(
      (d) => d.successRatePercent < 95,
    );
    const warnDeps = depsData.dependencies.filter(
      (d) => d.successRatePercent >= 95 && d.successRatePercent < 99.5,
    );
    if (criticalDeps.length > 0) {
      depsCallouts.push({
        level: 'critical',
        message: (
          <>
            <strong>{criticalDeps.length}</strong>{' '}
            {criticalDeps.length === 1 ? 'dependency' : 'dependencies'} below
            95% success rate: {criticalDeps.map((d) => d.name).join(', ')}.
          </>
        ),
      });
    } else if (warnDeps.length > 0) {
      depsCallouts.push({
        level: 'warning',
        message: (
          <>
            <strong>{warnDeps.length}</strong>{' '}
            {warnDeps.length === 1 ? 'dependency' : 'dependencies'} below 99.5%
            success rate.
          </>
        ),
      });
    } else if (depsData.dependencies.length > 0) {
      depsCallouts.push({
        level: 'good',
        message: (
          <>
            All {formatNumber(depsData.dependencies.length)}{' '}
            {depsData.dependencies.length === 1 ? 'dependency' : 'dependencies'}{' '}
            above 99.5% success rate.
          </>
        ),
      });
    }
  }

  const jobsCallouts: PanelCallout[] = [];
  if (jobsData) {
    const totalExJobs = jobsData.jobs.reduce((s, j) => s + j.total, 0);
    const totalSucJobs = jobsData.jobs.reduce((s, j) => s + j.successes, 0);
    const srJobs =
      totalExJobs > 0 ? (totalSucJobs / totalExJobs) * 100 : 100;
    jobsCallouts.push({
      level: srJobs >= 99 ? 'good' : srJobs >= 95 ? 'warning' : 'critical',
      message: (
        <>
          Overall success rate is <strong>{srJobs.toFixed(1)}%</strong> across{' '}
          {formatNumber(totalExJobs)} executions.
        </>
      ),
    });
    const failingJobs = jobsData.jobs.filter((j) => j.failures > 0);
    if (failingJobs.length > 0) {
      jobsCallouts.push({
        level: 'warning',
        message: (
          <>
            {failingJobs.length}{' '}
            {failingJobs.length === 1 ? 'job has' : 'jobs have'} recorded
            failures: {failingJobs.map((j) => j.jobName).join(', ')}.
          </>
        ),
      });
    }
  }

  const retrievalCallouts: PanelCallout[] = [];
  if (retrievalData) {
    retrievalCallouts.push({
      level:
        retrievalData.embeddingErrorCount === 0
          ? 'good'
          : retrievalData.embeddingErrorCount < 5
          ? 'warning'
          : 'critical',
      message:
        retrievalData.embeddingErrorCount === 0 ? (
          <>No embedding errors — retrieval is operating cleanly.</>
        ) : (
          <>
            <strong>{formatNumber(retrievalData.embeddingErrorCount)}</strong>{' '}
            embedding{' '}
            {retrievalData.embeddingErrorCount === 1 ? 'error' : 'errors'} —
            may be causing silent retrieval degradation.
          </>
        ),
    });
    retrievalCallouts.push({
      level: 'info',
      message: (
        <>
          {formatNumber(retrievalData.totalSearches)} searches across{' '}
          {retrievalData.collections.length} collection
          {retrievalData.collections.length !== 1 ? 's' : ''}.
        </>
      ),
    });
  }

  const topologyCallouts: PanelCallout[] = [];
  if (topologyData) {
    const criticalNodes = topologyData.nodes.filter(
      (n) => n.status === 'critical',
    );
    const degradedNodes = topologyData.nodes.filter(
      (n) => n.status === 'degraded',
    );
    if (criticalNodes.length > 0) {
      topologyCallouts.push({
        level: 'critical',
        message: (
          <>
            <strong>{criticalNodes.length}</strong> service
            {criticalNodes.length !== 1 ? 's' : ''} in critical state.
          </>
        ),
      });
    } else if (degradedNodes.length > 0) {
      topologyCallouts.push({
        level: 'warning',
        message: (
          <>
            <strong>{degradedNodes.length}</strong> service
            {degradedNodes.length !== 1 ? 's' : ''} degraded.
          </>
        ),
      });
    } else if (topologyData.nodes.length > 0) {
      topologyCallouts.push({
        level: 'good',
        message: <>All {topologyData.nodes.length} services healthy.</>,
      });
    }
    topologyCallouts.push({
      level: 'info',
      message: (
        <>
          {topologyData.nodes.length} services,{' '}
          {topologyData.edges.length} observed connections.
        </>
      ),
    });
  }

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
      <Tabs
        value={activeTab}
        onValueChange={(value) => {
          setActiveTab(value);
          updateQuery({ tab: value });
        }}
      >
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
            <TabsTrigger
              value="retrieval"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              Retrieval
            </TabsTrigger>
            <TabsTrigger
              value="topology"
              className="px-4 py-3 text-sm rounded-none border-b-2 border-transparent data-[state=active]:border-[var(--color-brand-primary)] data-[state=active]:bg-transparent data-[state=active]:text-[var(--color-brand-primary)] data-[state=active]:shadow-none"
            >
              Topology
            </TabsTrigger>
          </TabsList>
        </div>

        <div className="p-6 overflow-auto flex-1">
          <TabsContent value="overview" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                Platform Overview
              </span>
              <PanelInfoPopover
                title="Platform Overview"
                description={
                  <>
                    <p>
                      Tracks all inbound HTTP requests to the platform.{' '}
                      <strong>Total requests</strong> and{' '}
                      <strong>requests per minute</strong> show activity volume.{' '}
                      <strong>Error rate</strong> is the share of failed
                      requests — anything above 1% warrants investigation.{' '}
                      <strong>P95 latency</strong> is the 95th-percentile
                      response time; above 2 seconds indicates a performance
                      issue.
                    </p>
                    <p>
                      Time-series charts below show how each metric trends over
                      the selected window.
                    </p>
                  </>
                }
                callouts={
                  overviewCallouts.length > 0 ? overviewCallouts : undefined
                }
                panelKind="overview"
                getMetrics={
                  overview
                    ? () => ({
                        requests: overview.requests,
                        errors: overview.errors,
                        latency: overview.latency,
                      })
                    : undefined
                }
              />
            </div>
            {renderOverviewTab()}
          </TabsContent>

          <TabsContent value="ai" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                AI Performance
              </span>
              <PanelInfoPopover
                title="AI Performance"
                description={
                  <>
                    <p>
                      Aggregates all LLM calls recorded by{' '}
                      <code>TelemetryChatClient</code> — agent runs, chat,
                      background summarisers, projectors, and tool calls.
                    </p>
                    <ul>
                      <li>
                        <strong>P50 / P95 latency</strong> — end-to-end
                        response time per LLM call
                      </li>
                      <li>
                        <strong>TTFT</strong> (time-to-first-token) — how long
                        before streaming begins; directly impacts perceived
                        responsiveness
                      </li>
                      <li>
                        <strong>Estimated cost</strong> — calculated from token
                        counts via the cost catalog in{' '}
                        <code>AiTokenCostService</code>
                      </li>
                    </ul>
                    <p>
                      The <strong>AI Cost Watchdog</strong> row shows how close
                      current spend is to the configured hourly threshold.
                    </p>
                  </>
                }
                callouts={aiCallouts.length > 0 ? aiCallouts : undefined}
                panelKind="ai"
                getMetrics={
                  aiData
                    ? () => ({
                        latency: aiData.latency,
                        ttft: aiData.ttft,
                        tokenUsage: aiData.tokenUsage,
                        clientServerComparison: aiData.clientServerComparison,
                        byModel: (aiData.byModel ?? []).slice(0, 10),
                        byAgent: aiData.byAgent.slice(0, 10),
                        totalEstimatedCostUsd: (aiData.byUseCase ?? []).reduce(
                          (s, uc) => s + (uc.estimatedCostUsd ?? 0),
                          0,
                        ),
                      })
                    : undefined
                }
              />
            </div>
            {renderAiTab()}
          </TabsContent>

          <TabsContent value="errors" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                Application Errors
              </span>
              <PanelInfoPopover
                title="Application Errors"
                description={
                  <>
                    <p>
                      Application exceptions captured by Application Insights,
                      deduplicated by outer exception type. Each row is one
                      distinct error class.
                    </p>
                    <ul>
                      <li>
                        Click any row to expand the stack trace and sample
                        operation ID
                      </li>
                      <li>
                        Use the type rail on the left to filter by exception
                        class
                      </li>
                      <li>
                        <strong>Muting</strong> a type hides it from the active
                        list but continues counting it in the rail so it is
                        never invisible
                      </li>
                      <li>Mutes persist in browser local storage across reloads</li>
                    </ul>
                  </>
                }
                callouts={
                  errorsCallouts.length > 0 ? errorsCallouts : undefined
                }
                panelKind="errors"
                getMetrics={
                  errorsData
                    ? () => ({
                        totalGroups: errorsData.errors.length,
                        activeGroups: errorsData.errors.filter(
                          (e) => !mutedErrorTypes.has(e.type),
                        ).length,
                        mutedTypes: mutedErrorTypes.size,
                        topErrors: errorsData.errors
                          .slice(0, 15)
                          .map((e) => ({
                            type: e.type,
                            count: e.count,
                            lastSeen: e.lastSeen,
                          })),
                      })
                    : undefined
                }
              />
            </div>
            {renderErrorsTab()}
          </TabsContent>

          <TabsContent value="dependencies" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                External Dependencies
              </span>
              <PanelInfoPopover
                title="External Dependencies"
                description={
                  <>
                    <p>
                      External calls made by the platform, tracked by
                      Application Insights dependency telemetry. Covers HTTP
                      calls to third-party APIs, SQL queries, gRPC, queues, and
                      event hubs.
                    </p>
                    <ul>
                      <li>
                        <strong>Success rate</strong> below 99.5% is amber;
                        below 95% is critical
                      </li>
                      <li>
                        <strong>Avg duration</strong> is the mean wall-clock
                        time per call over the selected window
                      </li>
                      <li>
                        <strong>Failed</strong> is the absolute count of calls
                        that returned a failure status
                      </li>
                    </ul>
                  </>
                }
                callouts={depsCallouts.length > 0 ? depsCallouts : undefined}
                panelKind="dependencies"
                getMetrics={
                  depsData
                    ? () => ({
                        dependencies: depsData.dependencies.map((d) => ({
                          name: d.name,
                          type: d.type,
                          successRatePercent: d.successRatePercent,
                          avgDurationMs: d.avgDurationMs,
                          totalCalls: d.totalCalls,
                          failedCalls: d.failedCalls,
                        })),
                      })
                    : undefined
                }
              />
            </div>
            {renderDependenciesTab()}
          </TabsContent>

          <TabsContent value="jobs" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                Background Jobs
              </span>
              <PanelInfoPopover
                title="Background Jobs"
                description={
                  <>
                    <p>
                      Quartz.NET background job execution records from the
                      worker service. Each row represents one registered job
                      class.
                    </p>
                    <ul>
                      <li>
                        <strong>Total</strong> is the number of trigger fires
                      </li>
                      <li>
                        <strong>Successes</strong> and{' '}
                        <strong>failures</strong> come from Quartz trigger
                        result status
                      </li>
                      <li>
                        Any non-zero failures value means the job threw an
                        unhandled exception — check the Errors tab for the
                        matching exception type
                      </li>
                    </ul>
                  </>
                }
                callouts={jobsCallouts.length > 0 ? jobsCallouts : undefined}
                panelKind="jobs"
                getMetrics={
                  jobsData
                    ? () => ({
                        jobs: jobsData.jobs.map((j) => ({
                          jobName: j.jobName,
                          total: j.total,
                          successes: j.successes,
                          failures: j.failures,
                          avgDurationMs: j.avgDurationMs,
                        })),
                      })
                    : undefined
                }
              />
            </div>
            {renderJobsTab()}
          </TabsContent>

          <TabsContent value="retrieval" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                Vector Retrieval
              </span>
              <PanelInfoPopover
                title="Vector Retrieval"
                description={
                  <>
                    <p>
                      Qdrant vector database operations and embedding generation
                      calls, captured via{' '}
                      <code>InstrumentedQdrantClient</code> and{' '}
                      <code>InstrumentedEmbeddingGenerator</code>.
                    </p>
                    <ul>
                      <li>
                        <strong>Searches</strong> are collection query calls;{' '}
                        <strong>upserts</strong> are index writes
                      </li>
                      <li>
                        <strong>Embedding errors</strong> are failures during
                        vector generation — these cause retrieval to silently
                        degrade with no query-time error surfaced to the caller
                      </li>
                      <li>
                        Latency percentiles and per-collection stats appear
                        below the summary cards
                      </li>
                    </ul>
                  </>
                }
                callouts={
                  retrievalCallouts.length > 0 ? retrievalCallouts : undefined
                }
                panelKind="retrieval"
                getMetrics={
                  retrievalData
                    ? () => ({
                        totalSearches: retrievalData.totalSearches,
                        totalUpserts: retrievalData.totalUpserts,
                        totalEmbeddingCalls: retrievalData.totalEmbeddingCalls,
                        embeddingErrorCount: retrievalData.embeddingErrorCount,
                        collections: retrievalData.collections.slice(0, 10),
                      })
                    : undefined
                }
              />
            </div>
            {renderRetrievalTab()}
          </TabsContent>

          <TabsContent value="topology" className="mt-0">
            <div className="mb-4 flex items-center gap-1.5">
              <span className="text-sm font-medium text-[var(--color-text-secondary)]">
                Service Topology
              </span>
              <PanelInfoPopover
                title="Service Topology"
                description={
                  <>
                    <p>
                      Directed graph of service-to-service call relationships
                      derived from Application Insights distributed traces.
                      Nodes are cloud role instances; edges represent observed
                      dependency calls.
                    </p>
                    <ul>
                      <li>
                        Node colour reflects health status: green (healthy),
                        amber (degraded), red (critical), grey (unknown)
                      </li>
                      <li>
                        Edge labels show call volume and P95 latency for the
                        selected time range
                      </li>
                      <li>Interact with the graph to zoom, pan, and use the mini-map</li>
                    </ul>
                  </>
                }
                callouts={
                  topologyCallouts.length > 0 ? topologyCallouts : undefined
                }
                panelKind="topology"
                getMetrics={
                  topologyData
                    ? () => ({
                        nodeCount: topologyData.nodes.length,
                        edgeCount: topologyData.edges.length,
                        nodes: topologyData.nodes.map((n) => ({
                          id: n.id,
                          label: n.label,
                          kind: n.kind,
                          status: n.status,
                          calls: n.calls,
                          errorRatePct: n.errorRatePct,
                          p95LatencyMs: n.p95LatencyMs,
                        })),
                        edges: topologyData.edges.slice(0, 30).map((e) => ({
                          source: e.source,
                          target: e.target,
                          kind: e.kind,
                          calls: e.calls,
                          errorRatePct: e.errorRatePct,
                          p95LatencyMs: e.p95LatencyMs,
                        })),
                      })
                    : undefined
                }
              />
            </div>
            {renderTopologyTab()}
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
}
