import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Activity, Calendar, Download, Filter, Loader2 } from 'lucide-react';

import { PageHeader } from '@/components/layout/aonik/PageHeader';
import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Button } from '@/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { cn } from '@/lib/utils';
import {
  aiTraceService,
  type AiTraceObservationResponse,
} from '@/services/aiService';

const TIME_RANGE_OPTIONS = [
  { value: '1h', label: 'Last hour' },
  { value: '24h', label: 'Last 24 hours' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
];

type WaterfallItem = AiTraceObservationResponse & {
  id: string;
  parentId: string | null;
  children: string[];
  depth: number;
  offsetPct: number;
  widthPct: number;
  durationLabel: string;
};

function formatAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function formatDurationMs(value: number | null): string {
  if (value == null || !Number.isFinite(value)) return '--';
  if (value >= 1000) return `${(value / 1000).toFixed(2)}s`;
  return `${Math.round(value)}ms`;
}

function formatTokens(value: number | null): string {
  return value == null ? '--' : value.toLocaleString();
}

function formatStatus(level: string, item: AiTraceObservationResponse): 'ok' | 'held' | 'error' {
  if (level.toLowerCase() === 'error') return 'error';
  if (item.level.toLowerCase() === 'warning') return 'held';
  return 'ok';
}

function getDurationMs(item: AiTraceObservationResponse): number {
  if (item.durationMs != null && Number.isFinite(item.durationMs)) return Math.max(1, item.durationMs);
  if (item.latencySeconds != null && Number.isFinite(item.latencySeconds)) return Math.max(1, item.latencySeconds * 1000);
  if (item.endTime) {
    const delta = new Date(item.endTime).getTime() - new Date(item.startTime).getTime();
    if (Number.isFinite(delta) && delta > 0) return delta;
  }
  return 1;
}

function getObservationNodeId(item: AiTraceObservationResponse): string {
  return item.spanId ?? item.observationId;
}

function getObservationCompleteness(item: AiTraceObservationResponse): number {
  let score = 0;
  if (item.input) score += 4;
  if (item.output) score += 4;
  if (item.metadata) score += 2;
  if (item.endTime) score += 1;
  if (item.durationMs != null) score += 1;
  if (item.latencySeconds != null) score += 1;
  if (item.parentSpanId ?? item.parentObservationId) score += 1;
  if (item.agentName ?? item.agentId) score += 1;
  if (item.providedModel) score += 1;
  return score;
}

function dedupeObservations(items: AiTraceObservationResponse[]): AiTraceObservationResponse[] {
  const deduped = new Map<string, AiTraceObservationResponse>();

  for (const item of items) {
    const id = getObservationNodeId(item);
    const existing = deduped.get(id);
    if (!existing || getObservationCompleteness(item) > getObservationCompleteness(existing)) {
      deduped.set(id, item);
    }
  }

  return Array.from(deduped.values());
}

function dedupeRootTraces(items: AiTraceObservationResponse[]): AiTraceObservationResponse[] {
  const deduped = new Map<string, AiTraceObservationResponse>();

  for (const item of items) {
    const existing = deduped.get(item.traceId);
    if (!existing || getObservationCompleteness(item) > getObservationCompleteness(existing)) {
      deduped.set(item.traceId, item);
    }
  }

  return Array.from(deduped.values()).sort(
    (a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime(),
  );
}

function buildWaterfall(items: AiTraceObservationResponse[]): WaterfallItem[] {
  const dedupedItems = dedupeObservations(items);
  if (dedupedItems.length === 0) return [];

  const sorted = [...dedupedItems].sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
  const firstStart = new Date(sorted[0].startTime).getTime();
  const lastEnd = Math.max(...sorted.map((item) => new Date(item.startTime).getTime() + getDurationMs(item)));
  const totalDuration = Math.max(1, lastEnd - firstStart);
  const ids = sorted.map(getObservationNodeId);
  const sortedById = new Map(sorted.map((item) => [getObservationNodeId(item), item]));
  const childrenById = new Map<string, string[]>();

  ids.forEach((id) => childrenById.set(id, []));

  sorted.forEach((item) => {
    const parentId = item.parentSpanId ?? item.parentObservationId;
    const id = getObservationNodeId(item);
    if (parentId && childrenById.has(parentId)) {
      const children = childrenById.get(parentId)!;
      if (!children.includes(id)) children.push(id);
    }
  });

  const depthCache = new Map<string, number>();
  const depthFor = (item: AiTraceObservationResponse): number => {
    const id = getObservationNodeId(item);
    const cached = depthCache.get(id);
    if (cached !== undefined) return cached;

    const parentId = item.parentSpanId ?? item.parentObservationId;
    const parent = parentId ? sortedById.get(parentId) : undefined;
    const depth = parent ? Math.min(6, depthFor(parent) + 1) : 0;
    depthCache.set(id, depth);
    return depth;
  };

  const byId = new Map<string, WaterfallItem>();
  sorted.forEach((item) => {
    const start = new Date(item.startTime).getTime();
    const durationMs = getDurationMs(item);
    const id = getObservationNodeId(item);
    byId.set(id, {
      ...item,
      id,
      parentId: item.parentSpanId ?? item.parentObservationId,
      children: childrenById.get(id) ?? [],
      depth: depthFor(item),
      offsetPct: Math.max(0, ((start - firstStart) / totalDuration) * 100),
      widthPct: Math.max(1, (durationMs / totalDuration) * 100),
      durationLabel: formatDurationMs(durationMs),
    });
  });

  const roots = sorted
    .map((item) => item.spanId ?? item.observationId)
    .filter((id) => {
      const node = byId.get(id)!;
      return !node.parentId || !byId.has(node.parentId);
    });

  const ordered: WaterfallItem[] = [];
  const visited = new Set<string>();
  const visit = (id: string) => {
    if (visited.has(id)) return;
    visited.add(id);
    const node = byId.get(id);
    if (!node) return;
    ordered.push(node);
    node.children.forEach(visit);
  };

  roots.forEach(visit);
  return ordered;
}

function getTraceTotalMs(items: AiTraceObservationResponse[]): number {
  if (items.length === 0) return 0;

  const starts = items.map((item) => new Date(item.startTime).getTime());
  const ends = items.map((item) => new Date(item.startTime).getTime() + getDurationMs(item));
  return Math.max(1, Math.max(...ends) - Math.min(...starts));
}

function statusPill(status: 'ok' | 'held' | 'error') {
  if (status === 'error') {
    return 'bg-red-500/10 text-red-600';
  }
  if (status === 'held') {
    return 'bg-amber-500/10 text-amber-600';
  }
  return 'bg-emerald-500/10 text-emerald-600';
}

export function ObservabilityTracesPage() {
  const requestIdRef = useRef(0);

  const [timeRange, setTimeRange] = useState('1h');
  const [statusFilter, setStatusFilter] = useState('all');
  const [traceItems, setTraceItems] = useState<AiTraceObservationResponse[]>([]);
  const [selectedTraceId, setSelectedTraceId] = useState<string | null>(null);
  const [selectedTraceItems, setSelectedTraceItems] = useState<AiTraceObservationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [traceLoading, setTraceLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadTraceList = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await aiTraceService.listObservations({
        page: 1,
        pageSize: 100,
        isRootObservation: true,
        timeRange,
      });

      const rootTraces = dedupeRootTraces(result.items);

      if (requestIdRef.current !== requestId) return;

      setTraceItems(rootTraces);
      setSelectedTraceId((current) => {
        if (current && rootTraces.some((item) => item.traceId === current)) return current;
        return rootTraces[0]?.traceId ?? null;
      });
    } catch (loadError) {
      if (requestIdRef.current !== requestId) return;
      const message = loadError instanceof Error ? loadError.message : 'Failed to load traces.';
      setError(message);
      setTraceItems([]);
      setSelectedTraceId(null);
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [timeRange]);

  useEffect(() => {
    void loadTraceList();
  }, [loadTraceList]);

  useEffect(() => {
    if (!selectedTraceId) {
      setSelectedTraceItems([]);
      return;
    }

    let cancelled = false;
    setTraceLoading(true);

    aiTraceService.listObservations({
      page: 1,
      pageSize: 200,
      traceId: selectedTraceId,
      timeRange,
    })
      .then((result) => {
        if (!cancelled) setSelectedTraceItems(result.items);
      })
      .catch(() => {
        if (!cancelled) setSelectedTraceItems([]);
      })
      .finally(() => {
        if (!cancelled) setTraceLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedTraceId, timeRange]);

  const filteredTraceItems = useMemo(() => traceItems.filter((item) => {
    if (statusFilter === 'all') return true;
    return formatStatus(item.level, item) === statusFilter;
  }), [statusFilter, traceItems]);

  const selectedTrace = useMemo(
    () => filteredTraceItems.find((item) => item.traceId === selectedTraceId)
      ?? traceItems.find((item) => item.traceId === selectedTraceId)
      ?? null,
    [filteredTraceItems, selectedTraceId, traceItems],
  );

  const waterfallItems = useMemo(() => buildWaterfall(selectedTraceItems), [selectedTraceItems]);
  const traceTotalMs = useMemo(() => getTraceTotalMs(selectedTraceItems), [selectedTraceItems]);

  const allStatuses = traceItems.map((item) => formatStatus(item.level, item));
  const okCount = allStatuses.filter((status) => status === 'ok').length;
  const heldCount = allStatuses.filter((status) => status === 'held').length;
  const errorCount = allStatuses.filter((status) => status === 'error').length;
  const selectedStatus = selectedTrace ? formatStatus(selectedTrace.level, selectedTrace) : 'ok';
  const selectedDurationMs = selectedTrace ? getDurationMs(selectedTrace) : null;
  const selectedSpans = waterfallItems.length;
  const selectedTools = waterfallItems.filter((item) => item.type.toLowerCase() === 'span').length;
  const selectedAgents = Array.from(new Set(selectedTraceItems.map((item) => item.agentName ?? item.agentId).filter(Boolean)));

  return (
    <div className="flex h-full flex-col overflow-auto">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
          <Breadcrumb
            items={[
              { label: 'Admin' },
              { label: 'Observability', href: '/admin/observability', icon: <Activity className="h-4 w-4" /> },
              { label: 'Traces' },
            ]}
            className="mb-3"
          />
          <PageHeader
            eyebrow="Observability · Distributed tracing"
            title="Traces"
            subtitle="Every agent run captured as a span tree using live AI observation data."
            actions={(
              <>
                <Button variant="outline" size="sm" disabled>
                  <Filter className="mr-2 h-3.5 w-3.5" />
                  Filters
                </Button>
                <Button variant="outline" size="sm" disabled>
                  <Calendar className="mr-2 h-3.5 w-3.5" />
                  {TIME_RANGE_OPTIONS.find((option) => option.value === timeRange)?.label ?? timeRange}
                </Button>
                <Button variant="outline" size="sm" disabled>
                  <Download className="mr-2 h-3.5 w-3.5" />
                  Export
                </Button>
              </>
            )}
          />
        </div>
      </div>

      <div className="flex-1 p-6">
        <div className="mb-4 grid gap-4 rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)] p-4 lg:grid-cols-[1fr_auto] lg:items-center">
          <div>
            <div className="text-sm font-semibold text-[var(--color-text-primary)]">
              {errorCount > 0 ? 'Trace errors detected' : heldCount > 0 ? 'Trace warnings detected' : 'Trace stream healthy'}
            </div>
            <div className="mt-1 text-xs text-[var(--color-text-secondary)]">
              {traceItems.length} root traces surfaced from the live observation feed for the selected window.
            </div>
          </div>
          <div className="grid grid-cols-4 gap-4 text-left lg:text-right">
            {[
              { label: 'Tail', value: traceItems.length.toString() },
              { label: 'OK', value: okCount.toString() },
              { label: 'Held', value: heldCount.toString() },
              { label: 'Errors', value: errorCount.toString() },
            ].map((item) => (
              <div key={item.label}>
                <div className="font-mono text-sm font-semibold text-[var(--color-text-primary)]">{item.value}</div>
                <div className="mt-1 text-[11px] text-[var(--color-text-tertiary)]">{item.label}</div>
              </div>
            ))}
          </div>
        </div>

        <div className="mb-4 flex flex-wrap items-center gap-3">
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[180px] bg-[var(--color-surface)]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              <SelectItem value="ok">OK</SelectItem>
              <SelectItem value="held">Held</SelectItem>
              <SelectItem value="error">Error</SelectItem>
            </SelectContent>
          </Select>

          <Select value={timeRange} onValueChange={setTimeRange}>
            <SelectTrigger className="w-[180px] bg-[var(--color-surface)]">
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

          {loading ? (
            <div className="flex items-center gap-2 text-xs text-[var(--color-text-tertiary)]">
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
              Loading traces...
            </div>
          ) : null}
        </div>

        {error ? (
          <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            {error}
          </div>
        ) : null}

        {!error ? (
          <div className="grid min-h-[640px] grid-cols-1 gap-5 xl:grid-cols-[380px_minmax(0,1fr)]">
            <div className="overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
              <div className="flex items-center gap-2 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-3">
                <Filter className="h-3.5 w-3.5 text-[var(--color-text-tertiary)]" />
                <span className="text-xs text-[var(--color-text-tertiary)]">
                  {statusFilter === 'all' ? 'status:any' : `status:${statusFilter}`}
                </span>
                <span className="ml-auto font-mono text-[10px] text-[var(--color-text-tertiary)]">
                  {filteredTraceItems.length} traces
                </span>
              </div>

              <div className="max-h-[720px] overflow-y-auto">
                {filteredTraceItems.length === 0 ? (
                  <div className="px-4 py-10 text-center text-sm text-[var(--color-text-secondary)]">
                    No traces found for the current filters.
                  </div>
                ) : (
                  filteredTraceItems.map((trace) => {
                    const active = trace.traceId === selectedTraceId;
                    const status = formatStatus(trace.level, trace);
                    return (
                      <button
                        key={trace.observationId}
                        type="button"
                        onClick={() => setSelectedTraceId(trace.traceId)}
                        className={cn(
                          'block w-full border-b border-[var(--color-border-light)] px-4 py-3 text-left transition-colors last:border-b-0',
                          active
                            ? 'border-l-4 border-l-[var(--color-brand-primary)] bg-[var(--color-brand-primary)]/10'
                            : 'hover:bg-[var(--color-surface-inset)]',
                        )}
                      >
                        <div className="mb-1 flex items-center justify-between gap-3">
                          <span className="truncate font-mono text-xs font-semibold text-[var(--color-text-primary)]">
                            {(trace.traceName ?? trace.name) || trace.traceId}
                          </span>
                          <span className={cn('rounded px-1.5 py-0.5 text-[10px] font-mono uppercase', statusPill(status))}>
                            {status}
                          </span>
                        </div>
                        <div className="mb-2 truncate font-mono text-[10px] text-[var(--color-text-tertiary)]">
                          {trace.traceId}
                        </div>
                        <div className="flex items-center justify-between gap-3 text-[10.5px] text-[var(--color-text-secondary)]">
                          <span>{trace.agentName ?? trace.agentId ?? trace.source}</span>
                          <span className="font-mono">{formatDurationMs(getDurationMs(trace))} · {formatAgo(trace.startTime)}</span>
                        </div>
                      </button>
                    );
                  })
                )}
              </div>
            </div>

            <div className="overflow-hidden rounded-xl border border-[var(--color-border-light)] bg-[var(--color-surface)]">
              {selectedTrace ? (
                <>
                  <div className="border-b border-[var(--color-border-light)] px-5 py-4">
                    <div className="mb-2 flex flex-wrap items-center gap-3">
                      <span className="font-mono text-sm font-semibold text-[var(--color-text-primary)]">
                        {(selectedTrace.traceName ?? selectedTrace.name) || selectedTrace.traceId}
                      </span>
                      <span className="font-mono text-[10px] text-[var(--color-text-tertiary)]">{selectedTrace.traceId}</span>
                      <span className={cn('rounded px-2 py-0.5 text-[10px] font-mono uppercase', statusPill(selectedStatus))}>
                        {selectedStatus}
                      </span>
                    </div>
                    <div className="flex flex-wrap gap-4 font-mono text-[11px] text-[var(--color-text-secondary)]">
                      <span>duration <b className="text-[var(--color-text-primary)]">{formatDurationMs(selectedDurationMs)}</b></span>
                      <span>spans <b className="text-[var(--color-text-primary)]">{selectedSpans}</b></span>
                      <span>tokens <b className="text-[var(--color-text-primary)]">{formatTokens(selectedTrace.totalTokens)}</b></span>
                      <span>tools <b className="text-[var(--color-text-primary)]">{selectedTools}</b></span>
                      <span>agent <b className="text-[var(--color-text-primary)]">{selectedTrace.agentName ?? selectedTrace.agentId ?? '--'}</b></span>
                      <span>source <b className="text-[var(--color-text-primary)]">{selectedTrace.source}</b></span>
                    </div>
                    {selectedAgents.length > 1 ? (
                      <div className="mt-2 text-[11px] text-[var(--color-text-tertiary)]">
                        Agents in trace: {selectedAgents.join(', ')}
                      </div>
                    ) : null}
                  </div>

                  <div className="grid grid-cols-[minmax(240px,320px)_90px_minmax(0,1fr)] gap-3 border-b border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-5 py-3 text-[10px] uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
                    <div>Span</div>
                    <div className="text-right">Duration</div>
                    <div className="grid grid-cols-5 font-mono normal-case tracking-normal text-[var(--color-text-tertiary)]">
                      {[0, 25, 50, 75, 100].map((tick) => (
                        <span key={tick} className={cn(tick === 100 ? 'text-right' : tick === 0 ? 'text-left' : 'text-center')}>
                          {Math.round((traceTotalMs * tick) / 100)}ms
                        </span>
                      ))}
                    </div>
                  </div>

                  <div className="max-h-[720px] overflow-y-auto">
                    {traceLoading ? (
                      <div className="flex items-center gap-2 px-5 py-8 text-sm text-[var(--color-text-secondary)]">
                        <Loader2 className="h-4 w-4 animate-spin" />
                        Loading trace spans...
                      </div>
                    ) : waterfallItems.length === 0 ? (
                      <div className="px-5 py-8 text-sm text-[var(--color-text-secondary)]">
                        No correlated spans found for this trace.
                      </div>
                    ) : (
                      waterfallItems.map((item) => (
                        <div
                          key={`${item.source}-${item.id}`}
                          className="grid grid-cols-[minmax(240px,320px)_90px_minmax(0,1fr)] gap-3 border-b border-[var(--color-border-light)] px-5 py-2.5 last:border-b-0"
                        >
                          <div className="min-w-0" style={{ paddingLeft: `${item.depth * 14}px` }}>
                            <div className="truncate font-mono text-[11.5px] text-[var(--color-text-primary)]" title={item.name}>
                              {item.name || '--'}
                            </div>
                            <div className="mt-1 flex items-center gap-2 text-[10px] text-[var(--color-text-tertiary)]">
                              <span className="rounded bg-[var(--color-brand-primary)]/10 px-1.5 py-0.5 font-medium text-[var(--color-brand-primary)]">
                                {item.type.toLowerCase()}
                              </span>
                              <span>{item.agentName ?? item.agentId ?? item.source}</span>
                            </div>
                          </div>

                          <div className="text-right font-mono text-[11px] text-[var(--color-text-secondary)]">
                            {item.durationLabel}
                          </div>

                          <div className="relative h-5 rounded bg-[var(--color-surface-inset)]">
                            <div className="absolute inset-y-0 left-1/4 w-px bg-[var(--color-border-light)]" />
                            <div className="absolute inset-y-0 left-2/4 w-px bg-[var(--color-border-light)]" />
                            <div className="absolute inset-y-0 left-3/4 w-px bg-[var(--color-border-light)]" />
                            <div
                              className={cn(
                                'absolute inset-y-1 rounded-sm',
                                item.level.toLowerCase() === 'error'
                                  ? 'bg-red-500'
                                  : item.level.toLowerCase() === 'warning'
                                  ? 'bg-amber-500'
                                  : item.type.toLowerCase() === 'generation'
                                  ? 'bg-[var(--color-brand-primary)]'
                                  : 'bg-sky-500',
                              )}
                              style={{
                                left: `${item.offsetPct}%`,
                                width: `${Math.min(item.widthPct, 100 - item.offsetPct)}%`,
                              }}
                            />
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </>
              ) : (
                <div className="flex h-full min-h-[320px] items-center justify-center px-6 text-sm text-[var(--color-text-secondary)]">
                  No trace selected.
                </div>
              )}
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}
