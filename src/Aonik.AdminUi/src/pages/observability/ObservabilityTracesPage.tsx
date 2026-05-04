import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Calendar, Download, Filter, Loader2, Sparkles } from 'lucide-react';

import { PageHeader } from '@/components/layout/aonik/PageHeader';
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
import { observabilityService } from '@/services/observabilityService';
import { SpanDetailSlideOut } from './SpanDetailSlideOut';

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

// Score rows by how well they represent the WHOLE trace, not just one
// participating call. Used to pick the dedupe winner per traceId in the
// trace list. Without this preference, ancillary calls (e.g. the title
// generator's AiCallCompleted log line, which is fully populated with
// input/output/metadata) can win the dedupe over the actual chat run —
// leaving the trace listing displaying "title-generation" for what's
// actually a 10s voice chat. Priority order:
//
//   1. Aonik chat-level activity spans (name starts with "aonik.chat.")
//      are the canonical representative for a chat trace — they carry
//      use_case, run_id, agent name, and the full audio/cost/latency
//      tag set. These are produced by AguiStreamingEndpoint /
//      PlaygroundStreamingEndpoint and span the entire chat request.
//   2. The HTTP REQUEST span is the next-best root: it spans the full
//      request and its name is the route ("POST /ai/agui"), which is
//      meaningful even when no chat activity exists.
//   3. Longer durations beat shorter ones — they cover more of the trace.
//   4. Tie-break on field-completeness so the row with the richest
//      metadata still surfaces when multiple equally-good roots exist.
function getRootTraceRepresentativenessScore(item: AiTraceObservationResponse): number {
  let score = 0;
  if (item.type === 'SPAN' && item.name?.startsWith('aonik.chat.')) {
    score += 2_000_000;
  } else if (item.type === 'REQUEST') {
    score += 1_000_000;
  }
  const dur = getDurationMs(item);
  if (Number.isFinite(dur)) score += Math.min(dur, 999_999);
  score += getObservationCompleteness(item) * 0.001;
  return score;
}

// Merge two observations sharing a spanId — typically a SPAN (from
// dependencies, with full activity duration) and a GENERATION (from log,
// with LLM-call duration but richer metadata). Keep the widest time
// window so children render inside the parent's bar, then overlay any
// non-null metadata fields from either source.
function mergeObservations(
  a: AiTraceObservationResponse,
  b: AiTraceObservationResponse,
): AiTraceObservationResponse {
  const aStart = new Date(a.startTime).getTime();
  const bStart = new Date(b.startTime).getTime();
  const aDur = getDurationMs(a);
  const bDur = getDurationMs(b);
  const aEnd = aStart + aDur;
  const bEnd = bStart + bDur;

  const earliest = aStart <= bStart ? a : b;
  const widerEnd = aEnd >= bEnd ? aEnd : bEnd;
  const widerDuration = Math.max(aDur, bDur);

  const richer = getObservationCompleteness(a) >= getObservationCompleteness(b) ? a : b;
  const other = richer === a ? b : a;
  const pick = <K extends keyof AiTraceObservationResponse>(key: K) =>
    richer[key] ?? other[key];

  return {
    ...other,
    ...richer,
    startTime: earliest.startTime,
    endTime: new Date(widerEnd).toISOString(),
    durationMs: widerDuration,
    latencySeconds: widerDuration / 1000,
    input: pick('input'),
    output: pick('output'),
    metadata: pick('metadata'),
    aiRunId: pick('aiRunId'),
    inputTokens: pick('inputTokens'),
    outputTokens: pick('outputTokens'),
    totalTokens: pick('totalTokens'),
    costUsd: pick('costUsd'),
    providedModel: pick('providedModel'),
    parentSpanId: pick('parentSpanId'),
    parentObservationId: pick('parentObservationId'),
  };
}

function dedupeObservations(items: AiTraceObservationResponse[]): AiTraceObservationResponse[] {
  const deduped = new Map<string, AiTraceObservationResponse>();

  for (const item of items) {
    const id = getObservationNodeId(item);
    const existing = deduped.get(id);
    deduped.set(id, existing ? mergeObservations(existing, item) : item);
  }

  return Array.from(deduped.values());
}

function dedupeRootTraces(items: AiTraceObservationResponse[]): AiTraceObservationResponse[] {
  const deduped = new Map<string, AiTraceObservationResponse>();

  for (const item of items) {
    const existing = deduped.get(item.traceId);
    if (
      !existing ||
      getRootTraceRepresentativenessScore(item) >
        getRootTraceRepresentativenessScore(existing)
    ) {
      deduped.set(item.traceId, item);
    }
  }

  return Array.from(deduped.values()).sort(
    (a, b) => new Date(b.startTime).getTime() - new Date(a.startTime).getTime(),
  );
}

/**
 * For each LLM call, the server emits up to FOUR rows that look
 * identical to a human:
 *
 *   1. Outer `chat` GENERATION  (carries input/output/tokens/model)
 *   2. Inner `chat` GENERATION  (agent-framework wrapper, child of #1,
 *                                same name, NO input/output)
 *   3. `chat <model-name>` SPAN (dependency view of #1)
 *   4. `POST /v1/chat/completions` HTTP (the underlying provider call)
 *
 * Without folding, clicking 2/3/4 shows partial data and the user has
 * to guess which sibling carries the payload. This pass:
 *
 *   • Identifies the canonical row per call (prefers GENERATION with
 *     input/output, falls back to the longest-duration chat-named row).
 *   • Backfills missing input / output / providedModel /
 *     totalTokens / costUsd / timeToFirstTokenSeconds onto every other
 *     row in the cluster so clicking ANY of them shows the same data.
 *   • Drops the inner duplicate GENERATION so the waterfall has one
 *     row per call. The HTTP and SPAN views are kept (they're useful
 *     for raw HTTP timing / dependency analysis) but now carry the
 *     payload data inherited from the canonical row.
 */
function foldChatCallDuplicates(items: AiTraceObservationResponse[]): AiTraceObservationResponse[] {
  // Group rows that represent the SAME logical chat call. The grouping
  // key is the parentSpanId of the LLM-typed chat row (or its own
  // spanId if it is itself a parent of others).
  const isChatNamed = (x: AiTraceObservationResponse) =>
    x.name === 'chat' || x.name === 'chat.stream'
    || x.name?.startsWith('chat ') || x.name === 'POST /v1/chat/completions';

  // Map every chat-named span into a cluster keyed by either its own
  // spanId (if it has children that are also chat-named) or by its
  // parent's spanId if its parent is chat-named.
  const chatRows = items.filter(isChatNamed);
  const childrenByParent = new Map<string, AiTraceObservationResponse[]>();
  for (const row of chatRows) {
    const parentId = row.parentSpanId ?? row.parentObservationId;
    if (!parentId) continue;
    if (!childrenByParent.has(parentId)) childrenByParent.set(parentId, []);
    childrenByParent.get(parentId)!.push(row);
  }

  // The cluster anchor is a chat row that has at least one chat-named
  // child. Its spanId is the cluster key.
  const clusterKeyForRow = new Map<string, string>();
  for (const row of chatRows) {
    const ownId = row.spanId ?? row.observationId;
    const parentId = row.parentSpanId ?? row.parentObservationId;
    // If row's parent is itself a chat row (i.e. there's a chat anchor
    // above us), join that cluster.
    const parentIsChat = parentId && chatRows.some(p => (p.spanId ?? p.observationId) === parentId);
    if (parentIsChat) {
      clusterKeyForRow.set(ownId, parentId!);
    } else {
      clusterKeyForRow.set(ownId, ownId);
    }
  }

  // Also fold the underlying `POST /v1/chat/completions` HTTP span when
  // its parent is the LLM chat row.
  const clusters = new Map<string, AiTraceObservationResponse[]>();
  for (const row of chatRows) {
    const ownId = row.spanId ?? row.observationId;
    const key = clusterKeyForRow.get(ownId) ?? ownId;
    if (!clusters.has(key)) clusters.set(key, []);
    clusters.get(key)!.push(row);
  }

  // For each cluster, find the canonical row (highest "completeness"
  // as measured below — we explicitly prefer rows that carry the
  // payload), then propagate its payload-y fields onto siblings.
  const completeness = (x: AiTraceObservationResponse) => {
    let s = 0;
    if (x.input) s += 8;
    if (x.output) s += 8;
    if (x.totalTokens) s += 2;
    if (x.providedModel) s += 1;
    return s;
  };

  const enriched = new Map<string, AiTraceObservationResponse>();
  for (const item of items) {
    enriched.set(item.observationId, item);
  }

  const droppedSpanIds = new Set<string>();

  for (const cluster of clusters.values()) {
    if (cluster.length < 2) continue;

    const canonical = cluster.reduce((best, x) =>
      completeness(x) > completeness(best) ? x : best);

    for (const sibling of cluster) {
      if (sibling.observationId === canonical.observationId) continue;

      // Drop the inner GENERATION duplicate — it adds no new
      // information (same parent, same name, no payload, sub-millisecond
      // delta from canonical). Keeping it would just put another
      // identical row in the waterfall that, when clicked, shows
      // nothing.
      const isInnerGenDuplicate =
        sibling.type === 'GENERATION'
        && sibling.name === canonical.name
        && (sibling.parentSpanId ?? sibling.parentObservationId) === (canonical.spanId ?? canonical.observationId)
        && !sibling.input && !sibling.output;
      if (isInnerGenDuplicate) {
        droppedSpanIds.add(sibling.observationId);
        continue;
      }

      // Otherwise (HTTP child + SPAN view), backfill the payload-y
      // fields from the canonical row so clicking the row shows the
      // same data.
      enriched.set(sibling.observationId, {
        ...sibling,
        input: sibling.input ?? canonical.input,
        output: sibling.output ?? canonical.output,
        providedModel: sibling.providedModel ?? canonical.providedModel,
        inputTokens: sibling.inputTokens ?? canonical.inputTokens,
        outputTokens: sibling.outputTokens ?? canonical.outputTokens,
        totalTokens: sibling.totalTokens ?? canonical.totalTokens,
        costUsd: sibling.costUsd ?? canonical.costUsd,
        timeToFirstTokenSeconds: sibling.timeToFirstTokenSeconds ?? canonical.timeToFirstTokenSeconds,
      });
    }
  }

  return Array.from(enriched.values()).filter(x => !droppedSpanIds.has(x.observationId));
}

function buildWaterfall(items: AiTraceObservationResponse[]): WaterfallItem[] {
  const folded = foldChatCallDuplicates(items);
  const dedupedItems = dedupeObservations(folded);
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

function getSpanKind(item: AiTraceObservationResponse): string {
  const type = item.type.toLowerCase();
  if (type === 'generation') return 'llm';
  if (type === 'request') return 'request';
  if (type === 'http') return 'http';
  if (type === 'db') return 'db';
  return 'span';
}

function getSpanActor(item: AiTraceObservationResponse): string {
  return item.agentName ?? item.agentId ?? item.serviceName ?? item.source;
}

export function ObservabilityTracesPage() {
  const requestIdRef = useRef(0);

  const [timeRange, setTimeRange] = useState('24h');
  const [statusFilter, setStatusFilter] = useState('all');
  const [traceItems, setTraceItems] = useState<AiTraceObservationResponse[]>([]);
  const [selectedTraceId, setSelectedTraceId] = useState<string | null>(null);
  const [selectedTraceItems, setSelectedTraceItems] = useState<AiTraceObservationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [traceLoading, setTraceLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openSpanId, setOpenSpanId] = useState<string | null>(null);

  // AI interpretation of the currently-selected trace.
  const [traceAnalysis, setTraceAnalysis] = useState<string | null>(null);
  const [traceAnalysisLoading, setTraceAnalysisLoading] = useState(false);
  const [traceAnalysisError, setTraceAnalysisError] = useState<string | null>(null);
  // Track which traceId the analysis was generated for; if the user
  // selects a different trace, drop the stale text.
  const [traceAnalysisFor, setTraceAnalysisFor] = useState<string | null>(null);

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

  // Drop any stale analysis when the user picks a different trace so
  // they never see the previous trace's interpretation under the new
  // header.
  useEffect(() => {
    if (selectedTraceId !== traceAnalysisFor) {
      setTraceAnalysis(null);
      setTraceAnalysisError(null);
    }
  }, [selectedTraceId, traceAnalysisFor]);

  const explainSelectedTrace = useCallback(async () => {
    if (!selectedTraceId || selectedTraceItems.length === 0) return;
    setTraceAnalysisLoading(true);
    setTraceAnalysisError(null);
    setTraceAnalysis(null);
    try {
      const res = await observabilityService.explainTrace(
        selectedTraceId,
        selectedTraceItems,
      );
      setTraceAnalysis(res.analysis);
      setTraceAnalysisFor(selectedTraceId);
    } catch (e) {
      const message =
        (e as { response?: { data?: { detail?: string; title?: string } } })?.response?.data?.detail
        ?? (e as { response?: { data?: { detail?: string; title?: string } } })?.response?.data?.title
        ?? (e instanceof Error ? e.message : null)
        ?? 'Could not generate trace analysis.';
      setTraceAnalysisError(message);
    } finally {
      setTraceAnalysisLoading(false);
    }
  }, [selectedTraceId, selectedTraceItems]);

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

  // Reset the slide-out whenever we switch traces — span ids only make
  // sense within the trace they were captured in.
  useEffect(() => {
    setOpenSpanId(null);
  }, [selectedTraceId]);

  const openSpanIndex = waterfallItems.findIndex((item) => item.id === openSpanId);
  const openSpan = openSpanIndex >= 0 ? waterfallItems[openSpanIndex] : null;
  const traceStartMs = useMemo(() => {
    if (selectedTraceItems.length === 0) return 0;
    return Math.min(...selectedTraceItems.map((item) => new Date(item.startTime).getTime()));
  }, [selectedTraceItems]);

  const handlePrevSpan = () => {
    if (openSpanIndex > 0) setOpenSpanId(waterfallItems[openSpanIndex - 1].id);
  };
  const handleNextSpan = () => {
    if (openSpanIndex >= 0 && openSpanIndex < waterfallItems.length - 1) {
      setOpenSpanId(waterfallItems[openSpanIndex + 1].id);
    }
  };

  const selectedStatus = selectedTrace ? formatStatus(selectedTrace.level, selectedTrace) : 'ok';
  const selectedDurationMs = selectedTrace ? getDurationMs(selectedTrace) : null;
  const selectedSpans = waterfallItems.length;
  const selectedTools = waterfallItems.filter((item) => ['span', 'http', 'db'].includes(item.type.toLowerCase())).length;
  const selectedAgents = Array.from(new Set(selectedTraceItems.map((item) => item.agentName ?? item.agentId).filter(Boolean)));
  const selectedAgentLabel = selectedTrace?.agentName ?? selectedTrace?.agentId ?? selectedTrace?.serviceName ?? '--';

  return (
    <div className="relative flex h-full flex-col overflow-hidden">
      <div className="border-b border-[var(--color-border-light)] bg-[var(--color-surface)]">
        <div className="px-6 pt-5 pb-4">
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

      <div className="flex-1 overflow-auto p-6">
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
                          <span>{trace.agentName ?? trace.agentId ?? trace.serviceName ?? trace.source}</span>
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
                      <span>agent <b className="text-[var(--color-text-primary)]">{selectedAgentLabel}</b></span>
                      <span>tail <b className="text-[var(--color-text-primary)]">{filteredTraceItems.length}</b></span>
                    </div>
                    {selectedAgents.length > 1 ? (
                      <div className="mt-2 text-[11px] text-[var(--color-text-tertiary)]">
                        Agents in trace: {selectedAgents.join(', ')}
                      </div>
                    ) : null}

                    <div className="mt-3 flex flex-wrap items-start gap-3">
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={explainSelectedTrace}
                        disabled={traceAnalysisLoading || selectedTraceItems.length === 0}
                        className="h-8 gap-1.5 text-xs"
                      >
                        {traceAnalysisLoading ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Sparkles className="h-3.5 w-3.5" />
                        )}
                        {traceAnalysisLoading ? 'Analysing...' : 'Interpret with AI'}
                      </Button>
                      {traceAnalysisError ? (
                        <span className="text-[11px] text-red-500">
                          {traceAnalysisError}
                        </span>
                      ) : null}
                    </div>

                    {traceAnalysis && traceAnalysisFor === selectedTraceId ? (
                      <div className="mt-3 rounded-lg border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-4 text-[12.5px] leading-relaxed text-[var(--color-text-primary)]">
                        <div className="mb-2 flex items-center gap-1.5 text-[10px] uppercase tracking-[0.04em] text-[var(--color-text-tertiary)]">
                          <Sparkles className="h-3 w-3" />
                          AI trace analysis
                        </div>
                        <pre className="whitespace-pre-wrap break-words font-sans text-[12.5px]">
                          {traceAnalysis}
                        </pre>
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
                      waterfallItems.map((item) => {
                        const isOpen = item.id === openSpanId;
                        return (
                        <button
                          key={`${item.source}-${item.id}`}
                          type="button"
                          onClick={() => setOpenSpanId(item.id)}
                          className={cn(
                            'grid w-full cursor-pointer grid-cols-[minmax(240px,320px)_90px_minmax(0,1fr)] gap-3 border-b border-l-[3px] border-[var(--color-border-light)] py-2.5 pr-5 text-left transition-colors last:border-b-0',
                            isOpen
                              ? 'border-l-[var(--color-brand-primary)] bg-[var(--color-brand-primary-10)] pl-[17px]'
                              : 'border-l-transparent pl-5 hover:bg-[var(--color-surface-inset)]',
                          )}
                        >
                          <div className="min-w-0" style={{ paddingLeft: `${item.depth * 14}px` }}>
                            <div className="truncate font-mono text-[11.5px] text-[var(--color-text-primary)]" title={item.name}>
                              {item.name || '--'}
                            </div>
                            <div className="mt-1 flex items-center gap-2 text-[10px] text-[var(--color-text-tertiary)]">
                              <span className="rounded bg-[var(--color-brand-primary)]/10 px-1.5 py-0.5 font-medium text-[var(--color-brand-primary)]">
                                {getSpanKind(item)}
                              </span>
                              <span>{getSpanActor(item)}</span>
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
                                  : item.type.toLowerCase() === 'db'
                                    ? 'bg-teal-500'
                                    : item.type.toLowerCase() === 'http'
                                      ? 'bg-violet-500'
                                      : 'bg-sky-500',
                              )}
                              style={{
                                left: `${item.offsetPct}%`,
                                width: `${Math.min(item.widthPct, 100 - item.offsetPct)}%`,
                              }}
                            />
                          </div>
                        </button>
                        );
                      })
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

      {openSpan && (
        <SpanDetailSlideOut
          span={openSpan}
          totalMs={traceTotalMs}
          traceStartMs={traceStartMs}
          durationMs={getDurationMs(openSpan)}
          onClose={() => setOpenSpanId(null)}
          hasPrev={openSpanIndex > 0}
          hasNext={openSpanIndex >= 0 && openSpanIndex < waterfallItems.length - 1}
          onPrev={handlePrevSpan}
          onNext={handleNextSpan}
        />
      )}
    </div>
  );
}
