import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Activity, AlertCircle, Braces, ChevronDown, ChevronRight, Copy, ExternalLink, Loader2, Search, X } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { PanelInfoPopover } from '@/components/ui/panel-info-popover';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { DataTable, type ColumnDef } from '@/components/ui/data-table/data-table';
import type { AiTraceObservationResponse } from '@/services/aiService';
import { aiTraceService } from '@/services/aiService';

const breadcrumbItems = [
  { label: 'AI', href: '/ai/agents' },
  { label: 'AI Traces', icon: <Activity className="h-3.5 w-3.5" /> },
];

const typeClass = (type: string) => {
  switch (type.toLowerCase()) {
    case 'generation':
      return 'bg-violet-500/10 text-violet-700 border-violet-200';
    case 'span':
      return 'bg-blue-500/10 text-blue-700 border-blue-200';
    case 'event':
      return 'bg-sky-500/10 text-sky-700 border-sky-200';
    default:
      return 'bg-gray-500/10 text-gray-700 border-gray-200';
  }
};

const levelClass = (level: string) => {
  switch (level.toLowerCase()) {
    case 'error':
      return 'bg-red-500/10 text-red-700 border-red-200';
    case 'warning':
      return 'bg-amber-500/10 text-amber-700 border-amber-200';
    default:
      return 'bg-emerald-500/10 text-emerald-700 border-emerald-200';
  }
};

function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString();
}

function formatSeconds(value: number | null): string {
  if (value == null) return '--';
  if (value < 1) return `${Math.round(value * 1000)}ms`;
  return `${value.toFixed(2)}s`;
}

function formatCost(value: number | null): string {
  if (value == null) return '--';
  if (value === 0) return '$0.0000';
  return `$${value.toFixed(6)}`;
}

function formatTokens(value: number | null): string {
  return value == null ? '--' : value.toLocaleString();
}

function compactPayload(value: string | null, max = 150): string {
  if (!value) return '--';

  let normalized = value;
  try {
    normalized = JSON.stringify(JSON.parse(value));
  } catch {
    normalized = value.replace(/\s+/g, ' ').trim();
  }

  return normalized.length > max ? `${normalized.slice(0, max)}...` : normalized;
}

function prettyPayload(value: string | null): string {
  if (!value) return '--';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function flattenMetadata(value: string | null): Array<{ path: string; value: string }> {
  if (!value) return [];

  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch {
    return [{ path: 'value', value }];
  }

  const rows: Array<{ path: string; value: string }> = [];
  const visit = (node: unknown, path: string) => {
    if (node === null || typeof node !== 'object') {
      rows.push({ path, value: JSON.stringify(node) });
      return;
    }

    if (Array.isArray(node)) {
      if (node.length === 0) rows.push({ path, value: '[]' });
      node.forEach((child, index) => visit(child, `${path}[${index}]`));
      return;
    }

    const entries = Object.entries(node as Record<string, unknown>);
    if (entries.length === 0) {
      rows.push({ path, value: '{}' });
      return;
    }

    entries.forEach(([key, child]) => visit(child, path ? `${path}.${key}` : key));
  };

  visit(parsed, '');
  return rows.slice(0, 80);
}

function PayloadCell({ label, value, onOpen }: { label: string; value: string | null; onOpen: () => void }) {
  if (!value) {
    return <span className="text-xs text-[var(--color-text-tertiary)]">--</span>;
  }

  return (
    <button
      type="button"
      onClick={(event) => {
        event.stopPropagation();
        onOpen();
      }}
      className="group flex max-w-[240px] items-start gap-2 rounded border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-2 py-1.5 text-left hover:border-[var(--color-brand-primary)]"
      title={`Open ${label}`}
    >
      <Braces className="mt-0.5 h-3.5 w-3.5 shrink-0 text-[var(--color-text-tertiary)] group-hover:text-[var(--color-brand-primary)]" />
      <span className="line-clamp-3 font-mono text-[11px] leading-relaxed text-[var(--color-text-secondary)]">
        {compactPayload(value)}
      </span>
    </button>
  );
}

function DetailMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md bg-[var(--color-surface-inset)] px-3 py-2">
      <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">{label}</div>
      <div className="mt-1 font-mono text-xs text-[var(--color-text-primary)]">{value}</div>
    </div>
  );
}

function PayloadBlock({ title, value }: { title: string; value: string | null }) {
  const formatted = prettyPayload(value);

  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">{title}</h3>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={!value}
          onClick={() => value && navigator.clipboard.writeText(formatted)}
        >
          <Copy className="mr-2 h-3.5 w-3.5" />
          Copy
        </Button>
      </div>
      <pre className="max-h-56 overflow-auto rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 text-xs leading-relaxed text-[var(--color-text-primary)] whitespace-pre-wrap break-words">
        {formatted}
      </pre>
    </section>
  );
}

function MetadataTable({ value }: { value: string | null }) {
  const rows = flattenMetadata(value);

  if (rows.length === 0) {
    return (
      <section className="space-y-2">
        <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">Metadata</h3>
        <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface-inset)] p-3 text-sm text-[var(--color-text-tertiary)]">--</div>
      </section>
    );
  }

  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">Metadata</h3>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={!value}
          onClick={() => value && navigator.clipboard.writeText(prettyPayload(value))}
        >
          <Copy className="mr-2 h-3.5 w-3.5" />
          Copy
        </Button>
      </div>
      <div className="max-h-72 overflow-auto rounded-md border border-[var(--color-border-light)]">
        <table className="w-full text-xs">
          <thead className="sticky top-0 bg-[var(--color-surface-inset)] text-left text-[var(--color-text-tertiary)]">
            <tr>
              <th className="w-2/5 px-3 py-2 font-medium">Path</th>
              <th className="px-3 py-2 font-medium">Value</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.path} className="border-t border-[var(--color-border-light)]">
                <td className="px-3 py-2 font-mono text-[var(--color-text-secondary)]">{row.path}</td>
                <td className="px-3 py-2 font-mono text-[var(--color-text-primary)] break-all">{row.value}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

type WaterfallItem = AiTraceObservationResponse & {
  id: string;
  parentId: string | null;
  children: string[];
  depth: number;
  offsetPct: number;
  widthPct: number;
  durationLabel: string;
  sqlText: string | null;
};

function parseJsonValue(value: string | null): unknown {
  if (!value) return null;
  try {
    return JSON.parse(value) as unknown;
  } catch {
    return null;
  }
}

function stringifyNodeValue(value: unknown): string {
  if (typeof value === 'string') return value;
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function findNodeValueByKeys(value: unknown, matchers: string[]): string | null {
  if (value == null || typeof value !== 'object') return null;

  if (Array.isArray(value)) {
    for (const item of value) {
      const found = findNodeValueByKeys(item, matchers);
      if (found) return found;
    }
    return null;
  }

  for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
    const normalized = key.replace(/[_\s]/g, '').toLowerCase();
    if (matchers.includes(normalized)) {
      const rendered = stringifyNodeValue(child).trim();
      if (rendered) return rendered;
    }
  }

  for (const child of Object.values(value as Record<string, unknown>)) {
    const found = findNodeValueByKeys(child, matchers);
    if (found) return found;
  }

  return null;
}

function extractSqlText(item: AiTraceObservationResponse): string | null {
  const matchers = ['db.statement', 'dbstatement', 'commandtext', 'sqltext', 'generatedsql', 'sql'];
  const sources = [item.metadata, item.input, item.output];

  for (const source of sources) {
    const parsed = parseJsonValue(source);
    const found = findNodeValueByKeys(parsed, matchers);
    if (found) return found;
  }

  return null;
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
      durationLabel: durationMs >= 1000 ? `${(durationMs / 1000).toFixed(2)}s` : `${Math.round(durationMs)}ms`,
      sqlText: extractSqlText(item),
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

function buildTraceInsightMetrics(
  selectedObservation: AiTraceObservationResponse,
  items: WaterfallItem[],
) {
  const relevantItems = items.length > 0
    ? items
    : [{
      ...selectedObservation,
      id: selectedObservation.spanId ?? selectedObservation.observationId,
      parentId: selectedObservation.parentSpanId ?? selectedObservation.parentObservationId,
      children: [],
      depth: 0,
      offsetPct: 0,
      widthPct: 100,
      durationLabel: selectedObservation.durationMs != null
        ? `${Math.round(selectedObservation.durationMs)}ms`
        : formatSeconds(selectedObservation.latencySeconds),
      sqlText: extractSqlText(selectedObservation),
    } satisfies WaterfallItem];

  const sortedByDuration = [...relevantItems]
    .sort((a, b) => getDurationMs(b) - getDurationMs(a))
    .slice(0, 6)
    .map((item) => ({
      name: item.name,
      type: item.type,
      level: item.level,
      durationMs: Math.round(getDurationMs(item)),
      hasChildren: item.children.length > 0,
      childCount: item.children.length,
      agentName: item.agentName ?? item.agentId ?? null,
      sql: Boolean(item.sqlText),
    }));

  const startedAt = relevantItems.reduce(
    (min, item) => Math.min(min, new Date(item.startTime).getTime()),
    Number.POSITIVE_INFINITY,
  );
  const endedAt = relevantItems.reduce((max, item) => {
    const end = item.endTime
      ? new Date(item.endTime).getTime()
      : new Date(item.startTime).getTime() + getDurationMs(item);
    return Math.max(max, end);
  }, 0);

  return {
    traceId: selectedObservation.traceId,
    traceName: selectedObservation.traceName,
    observationId: selectedObservation.observationId,
    operationId: selectedObservation.operationId ?? relevantItems.find((item) => item.operationId)?.operationId ?? null,
    source: selectedObservation.source,
    rootObservation: {
      name: selectedObservation.name,
      type: selectedObservation.type,
      level: selectedObservation.level,
      agentName: selectedObservation.agentName ?? selectedObservation.agentId ?? null,
      model: selectedObservation.providedModel,
      durationMs: selectedObservation.durationMs ?? (selectedObservation.latencySeconds != null ? Math.round(selectedObservation.latencySeconds * 1000) : null),
      ttftMs: selectedObservation.timeToFirstTokenSeconds != null ? Math.round(selectedObservation.timeToFirstTokenSeconds * 1000) : null,
      inputTokens: selectedObservation.inputTokens,
      outputTokens: selectedObservation.outputTokens,
      totalTokens: selectedObservation.totalTokens,
      costUsd: selectedObservation.costUsd,
    },
    summary: {
      spanCount: relevantItems.length,
      parentSpanCount: relevantItems.filter((item) => item.children.length > 0).length,
      leafSpanCount: relevantItems.filter((item) => item.children.length === 0).length,
      errorSpanCount: relevantItems.filter((item) => item.level.toLowerCase() === 'error').length,
      warningSpanCount: relevantItems.filter((item) => item.level.toLowerCase() === 'warning').length,
      sqlSpanCount: relevantItems.filter((item) => Boolean(item.sqlText)).length,
      totalTraceDurationMs: Number.isFinite(startedAt) && endedAt >= startedAt ? Math.round(endedAt - startedAt) : null,
      startedAt: Number.isFinite(startedAt) ? new Date(startedAt).toISOString() : selectedObservation.startTime,
      endedAt: endedAt > 0 ? new Date(endedAt).toISOString() : selectedObservation.endTime,
      agents: Array.from(new Set(relevantItems.map((item) => item.agentName ?? item.agentId).filter(Boolean))).slice(0, 10),
      models: Array.from(new Set(relevantItems.map((item) => item.providedModel).filter(Boolean))).slice(0, 10),
    },
    slowestSpans: sortedByDuration,
  };
}

function TraceWaterfall({
  items,
  loading,
  onViewExceptions,
}: {
  items: WaterfallItem[];
  loading: boolean;
  onViewExceptions: (operationId: string) => void;
}) {
  const [collapsedIds, setCollapsedIds] = useState<Set<string>>(new Set());
  const [expandedDetailIds, setExpandedDetailIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    setCollapsedIds(new Set());
    setExpandedDetailIds(new Set());
  }, [items]);

  const collapsibleIds = useMemo(
    () => items.filter((item) => item.children.length > 0).map((item) => item.id),
    [items],
  );

  const byId = useMemo(() => new Map(items.map((item) => [item.id, item])), [items]);

  const visibleItems = useMemo(
    () => items.filter((item) => {
      let parentId = item.parentId;
      while (parentId) {
        if (collapsedIds.has(parentId)) return false;
        parentId = byId.get(parentId)?.parentId ?? null;
      }
      return true;
    }),
    [byId, collapsedIds, items],
  );

  const toggleCollapsed = useCallback((id: string) => {
    setCollapsedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const toggleDetail = useCallback((id: string) => {
    setExpandedDetailIds((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  return (
    <section className="space-y-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-[var(--color-text-primary)]">Trace Waterfall</h3>
        <div className="flex items-center gap-2">
          {!loading && items.length > 0 ? (
            <>
              <Button type="button" variant="ghost" size="sm" className="h-7 px-2 text-[10px]" onClick={() => setCollapsedIds(new Set())}>
                Expand all
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="h-7 px-2 text-[10px]"
                onClick={() => setCollapsedIds(new Set(collapsibleIds))}
              >
                Collapse all
              </Button>
            </>
          ) : null}
          <span className="text-xs text-[var(--color-text-tertiary)]">
            {loading ? 'Loading spans...' : `${items.length} spans`}
          </span>
        </div>
      </div>
      <div className="rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)]">
        {items.length === 0 ? (
          <div className="p-3 text-sm text-[var(--color-text-tertiary)]">
            {loading ? 'Loading trace spans...' : 'No correlated spans found for this trace.'}
          </div>
        ) : (
          <div className="max-h-96 overflow-auto">
            {visibleItems.map((item) => {
              const expanded = expandedDetailIds.has(item.id);
              const hasChildren = item.children.length > 0;
              const collapsed = collapsedIds.has(item.id);

              return (
                <div key={`${item.source}-${item.id}`} className="border-b border-[var(--color-border-light)] last:border-b-0">
                  <div className="grid grid-cols-[minmax(220px,34%)_1fr_156px] gap-3 px-3 py-2">
                    <div style={{ paddingLeft: `${item.depth * 14}px` }} className="min-w-0">
                      <div className="flex items-start gap-2">
                        {hasChildren ? (
                          <button
                            type="button"
                            onClick={() => toggleCollapsed(item.id)}
                            className="mt-0.5 rounded text-[var(--color-text-tertiary)] hover:text-[var(--color-text-primary)]"
                            aria-label={collapsed ? 'Expand span subtree' : 'Collapse span subtree'}
                          >
                            {collapsed ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                          </button>
                        ) : (
                          <span className="mt-0.5 block h-3.5 w-3.5 shrink-0" />
                        )}
                        <button
                          type="button"
                          onClick={() => toggleDetail(item.id)}
                          className="min-w-0 flex-1 text-left"
                        >
                          <div className="truncate text-xs font-medium text-[var(--color-text-primary)]" title={item.name}>{item.name || '--'}</div>
                          <div className="mt-0.5 flex items-center gap-2 text-[10px] text-[var(--color-text-tertiary)]">
                            <span>{item.type}</span>
                            <span className={`rounded px-1.5 py-0.5 font-medium ${hasChildren ? 'bg-[var(--color-brand-primary)]/10 text-[var(--color-brand-primary)]' : 'border border-[var(--color-border-light)] bg-[var(--color-surface)] text-[var(--color-text-tertiary)]'}`}>
                              {hasChildren ? `${item.children.length} child${item.children.length === 1 ? '' : 'ren'}` : 'Leaf'}
                            </span>
                            <span className="truncate font-mono">{item.agentName ?? item.agentId ?? item.source}</span>
                            {item.sqlText ? <span className="rounded bg-[var(--color-surface-inset)] px-1.5 py-0.5 font-medium">SQL</span> : null}
                          </div>
                        </button>
                      </div>
                    </div>
                    <div className="relative h-7 rounded bg-[var(--color-surface-inset)]">
                      <div
                        className="absolute top-1.5 h-4 rounded bg-[var(--color-brand-primary)]/70"
                        style={{ left: `${item.offsetPct}%`, width: `${Math.min(item.widthPct, 100 - item.offsetPct)}%` }}
                      />
                    </div>
                    <div className="flex items-center justify-end gap-2">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="h-7 px-2 text-[10px]"
                        onClick={() => toggleDetail(item.id)}
                      >
                        {expanded ? 'Hide' : 'Details'}
                      </Button>
                      {item.level.toLowerCase() === 'error' && item.operationId ? (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          className="h-7 px-2 text-[10px]"
                          onClick={() => onViewExceptions(item.operationId!)}
                        >
                          Errors
                        </Button>
                      ) : null}
                      <span className="font-mono text-xs text-[var(--color-text-secondary)]">{item.durationLabel}</span>
                    </div>
                  </div>

                  {expanded ? (
                    <div className="border-t border-[var(--color-border-light)] bg-[var(--color-surface-inset)] px-4 py-4">
                      <div className="mb-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                        <DetailMetric label="Started" value={formatDateTime(item.startTime)} />
                        <DetailMetric label="Span" value={item.spanId ?? item.observationId} />
                        <DetailMetric label="Parent" value={item.parentSpanId ?? item.parentObservationId ?? '--'} />
                        <DetailMetric label="Operation" value={item.operationId ?? item.traceId} />
                      </div>

                      <div className="space-y-4">
                        {item.sqlText ? (
                          <section className="space-y-2">
                            <div className="flex items-center justify-between">
                              <h4 className="text-sm font-semibold text-[var(--color-text-primary)]">SQL</h4>
                              <Button type="button" variant="ghost" size="sm" onClick={() => navigator.clipboard.writeText(item.sqlText!)}>
                                <Copy className="mr-2 h-3.5 w-3.5" />
                                Copy
                              </Button>
                            </div>
                            <pre className="max-h-56 overflow-auto rounded-md border border-[var(--color-border-light)] bg-[var(--color-surface)] p-3 font-mono text-xs leading-relaxed text-[var(--color-text-primary)] whitespace-pre-wrap break-words">
                              {item.sqlText}
                            </pre>
                          </section>
                        ) : null}
                        {item.input ? <PayloadBlock title="Input" value={item.input} /> : null}
                        {item.output ? <PayloadBlock title="Output" value={item.output} /> : null}
                        <MetadataTable value={item.metadata} />
                      </div>
                    </div>
                  ) : null}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </section>
  );
}

export function AiTracesPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const requestIdRef = useRef(0);

  const [items, setItems] = useState<AiTraceObservationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);
  const [provider, setProvider] = useState('Auto');
  const [name, setName] = useState('');
  const [traceName, setTraceName] = useState(() => searchParams.get('traceId') ?? searchParams.get('traceName') ?? '');
  const [type, setType] = useState('all');
  const [level, setLevel] = useState('all');
  const [rootFilter, setRootFilter] = useState('all');
  const [timeRange, setTimeRange] = useState('24h');
  const [selectedObservation, setSelectedObservation] = useState<AiTraceObservationResponse | null>(null);
  const [traceItems, setTraceItems] = useState<AiTraceObservationResponse[]>([]);
  const [traceLoading, setTraceLoading] = useState(false);

  const load = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await aiTraceService.listObservations({
        page,
        pageSize,
        name: name.trim() || undefined,
        traceName: traceName.trim() || undefined,
        type: type === 'all' ? undefined : type,
        level: level === 'all' ? undefined : level,
        isRootObservation: rootFilter === 'all' ? undefined : rootFilter === 'root',
        timeRange,
      });

      if (requestIdRef.current !== requestId) return;

      setItems(result.items);
      setTotalCount(result.totalCount);
      setProvider(result.provider);
    } catch (err) {
      if (requestIdRef.current !== requestId) return;
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load AI trace observations.');
      setItems([]);
      setTotalCount(0);
      setProvider('Unknown');
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [level, name, page, pageSize, rootFilter, timeRange, traceName, type]);

  useEffect(() => {
    void load();
  }, [load]);

  const openObservation = useCallback((row: AiTraceObservationResponse) => {
    setSelectedObservation(row);
  }, []);

  const openExceptions = useCallback((operationId: string) => {
    navigate(`/admin/observability?tab=errors&operationId=${encodeURIComponent(operationId)}&timeRange=${encodeURIComponent(timeRange)}`);
  }, [navigate, timeRange]);

  useEffect(() => {
    if (!selectedObservation?.traceId) {
      setTraceItems([]);
      return;
    }

    let cancelled = false;
    setTraceLoading(true);

    aiTraceService.listObservations({
      page: 1,
      pageSize: 200,
      traceId: selectedObservation.traceId,
      timeRange,
    })
      .then((result) => {
        if (!cancelled) setTraceItems(result.items);
      })
      .catch(() => {
        if (!cancelled) setTraceItems([]);
      })
      .finally(() => {
        if (!cancelled) setTraceLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedObservation?.traceId, timeRange]);

  const waterfallItems = useMemo(() => buildWaterfall(traceItems), [traceItems]);
  const errorOperationId = useMemo(
    () => selectedObservation?.operationId ?? waterfallItems.find((item) => item.operationId)?.operationId ?? null,
    [selectedObservation, waterfallItems],
  );
  const traceInsightMetrics = useMemo(
    () => (selectedObservation ? buildTraceInsightMetrics(selectedObservation, waterfallItems) : null),
    [selectedObservation, waterfallItems],
  );

  const columns = useMemo<ColumnDef<AiTraceObservationResponse>[]>(() => [
    {
      id: 'startTime',
      header: 'Start Time',
      accessorKey: 'startTime',
      sortable: true,
      cell: (row) => (
        <div className="min-w-[150px]">
          <div className="text-sm text-[var(--color-text-primary)]">{formatDateTime(row.startTime)}</div>
          <div className="mt-1 font-mono text-[10px] text-[var(--color-text-tertiary)]">{row.source}</div>
        </div>
      ),
    },
    {
      id: 'type',
      header: 'Type',
      accessorKey: 'type',
      sortable: true,
      cell: (row) => <Badge className={`text-xs ${typeClass(row.type)}`}>{row.type}</Badge>,
    },
    {
      id: 'name',
      header: 'Name',
      accessorKey: 'name',
      sortable: true,
      cell: (row) => (
        <div className="min-w-[220px] max-w-[280px]">
          <div className="truncate font-medium text-[var(--color-text-primary)]" title={row.name}>{row.name || '--'}</div>
          <div className="mt-1 truncate font-mono text-[10px] text-[var(--color-text-tertiary)]" title={row.traceName ?? row.traceId}>
            {row.traceName ?? row.traceId}
          </div>
        </div>
      ),
    },
    {
      id: 'input',
      header: 'Input',
      accessorFn: (row) => row.input ?? '',
      cell: (row) => <PayloadCell label="input" value={row.input} onOpen={() => openObservation(row)} />,
    },
    {
      id: 'output',
      header: 'Output',
      accessorFn: (row) => row.output ?? '',
      cell: (row) => <PayloadCell label="output" value={row.output} onOpen={() => openObservation(row)} />,
    },
    {
      id: 'metadata',
      header: 'Metadata',
      accessorFn: (row) => row.metadata ?? '',
      cell: (row) => <PayloadCell label="metadata" value={row.metadata} onOpen={() => openObservation(row)} />,
    },
    {
      id: 'level',
      header: 'Level',
      accessorKey: 'level',
      sortable: true,
      cell: (row) => <Badge className={`text-xs ${levelClass(row.level)}`}>{row.level}</Badge>,
    },
    {
      id: 'agentName',
      header: 'Agent',
      accessorFn: (row) => row.agentName ?? row.agentId ?? '',
      cell: (row) => (
        <span className="font-mono text-xs text-[var(--color-text-secondary)]">
          {row.agentName ?? row.agentId ?? '--'}
        </span>
      ),
    },
    {
      id: 'latencySeconds',
      header: 'Latency',
      accessorFn: (row) => row.latencySeconds ?? -1,
      sortable: true,
      cell: (row) => <span className="font-mono text-xs">{formatSeconds(row.latencySeconds)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'costUsd',
      header: 'Cost',
      accessorFn: (row) => row.costUsd ?? -1,
      sortable: true,
      cell: (row) => <span className="font-mono text-xs">{formatCost(row.costUsd)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'timeToFirstTokenSeconds',
      header: 'TTFT',
      accessorFn: (row) => row.timeToFirstTokenSeconds ?? -1,
      sortable: true,
      cell: (row) => <span className="font-mono text-xs">{formatSeconds(row.timeToFirstTokenSeconds)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'providedModel',
      header: 'Provided Model',
      accessorFn: (row) => row.providedModel ?? '',
      cell: (row) => (
        <div className="min-w-[160px]">
          <div className="truncate font-mono text-xs text-[var(--color-text-secondary)]" title={row.providedModel ?? undefined}>
            {row.providedModel ?? '--'}
          </div>
          <div className="mt-1 text-[10px] text-[var(--color-text-tertiary)]">
            {formatTokens(row.totalTokens)} tokens
          </div>
        </div>
      ),
    },
  ], [openObservation]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div className="p-6 space-y-6">
      <div className="space-y-2">
        <Breadcrumb items={breadcrumbItems} className="mb-1" />
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">AI Traces</h1>
            <p className="text-sm text-[var(--color-text-secondary)]">
              Inspect normalized AI observations from Langfuse or Application Insights.
            </p>
          </div>
          <Badge className="w-fit bg-blue-500/10 text-blue-700 border-blue-200">Provider: {provider}</Badge>
        </div>
      </div>

      <Card className="p-4 space-y-4">
        <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
          <div className="relative md:col-span-1 xl:col-span-2">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
            <Input value={name} onChange={(event) => { setPage(1); setName(event.target.value); }} placeholder="Filter by name" className="pl-9" />
          </div>
          <Input value={traceName} onChange={(event) => { setPage(1); setTraceName(event.target.value); }} placeholder="Trace name or trace ID" />
          <Select value={type} onValueChange={(value) => { setPage(1); setType(value); }}>
            <SelectTrigger>
              <SelectValue placeholder="Type" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All types</SelectItem>
              <SelectItem value="GENERATION">Generation</SelectItem>
              <SelectItem value="SPAN">Span</SelectItem>
              <SelectItem value="EVENT">Event</SelectItem>
            </SelectContent>
          </Select>
          <Select value={level} onValueChange={(value) => { setPage(1); setLevel(value); }}>
            <SelectTrigger>
              <SelectValue placeholder="Level" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All levels</SelectItem>
              <SelectItem value="DEFAULT">Default</SelectItem>
              <SelectItem value="WARNING">Warning</SelectItem>
              <SelectItem value="ERROR">Error</SelectItem>
            </SelectContent>
          </Select>
          <Select value={rootFilter} onValueChange={(value) => { setPage(1); setRootFilter(value); }}>
            <SelectTrigger>
              <SelectValue placeholder="Hierarchy" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All observations</SelectItem>
              <SelectItem value="root">Root only</SelectItem>
              <SelectItem value="child">Children only</SelectItem>
            </SelectContent>
          </Select>
          <Select value={timeRange} onValueChange={(value) => { setPage(1); setTimeRange(value); }}>
            <SelectTrigger>
              <SelectValue placeholder="Time range" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="1h">Last hour</SelectItem>
              <SelectItem value="24h">Last 24 hours</SelectItem>
              <SelectItem value="7d">Last 7 days</SelectItem>
              <SelectItem value="30d">Last 30 days</SelectItem>
            </SelectContent>
          </Select>
        </div>

        <div className="flex items-center justify-between">
          <p className="text-sm text-[var(--color-text-tertiary)]">{totalCount.toLocaleString()} observations</p>
          <Button variant="outline" onClick={() => { setPage(1); void load(); }} disabled={loading}>
            {loading ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Refresh
          </Button>
        </div>
      </Card>

      {error ? (
        <Card className="p-5 border-l-4 border-l-red-500">
          <div className="flex items-center gap-3 text-red-700">
            <AlertCircle className="h-5 w-5" />
            <div>
              <div className="font-medium">Failed to load AI observations</div>
              <div className="text-sm">{error}</div>
            </div>
          </div>
        </Card>
      ) : null}

      <Card className="p-4">
        <DataTable
          data={items}
          columns={columns}
          getRowId={(row) => row.observationId}
          showCheckboxes={false}
          loading={loading}
          loadingMessage="Loading AI observations..."
          emptyIcon={<Activity className="h-10 w-10 text-[var(--color-text-tertiary)]" />}
          emptyTitle="No AI observations found"
          emptyDescription="Adjust your filters or expand the time range."
          onRowClick={openObservation}
        />

        <div className="mt-4 flex items-center justify-between border-t border-[var(--color-border-light)] pt-4">
          <span className="text-xs text-[var(--color-text-tertiary)]">Page {page} of {totalPages}</span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1 || loading} onClick={() => setPage((current) => current - 1)}>
              Previous
            </Button>
            <Button variant="outline" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage((current) => current + 1)}>
              Next
            </Button>
          </div>
        </div>
      </Card>

      <Dialog open={selectedObservation !== null} onOpenChange={(open) => { if (!open) setSelectedObservation(null); }}>
        <DialogContent className="left-auto right-0 top-0 h-screen max-h-screen w-[min(760px,100vw)] max-w-none translate-x-0 translate-y-0 gap-0 overflow-hidden rounded-none border-y-0 border-r-0 p-0 data-[state=open]:slide-in-from-right data-[state=closed]:slide-out-to-right">
          {selectedObservation ? (
            <div className="flex h-full flex-col">
              <div className="border-b border-[var(--color-border-light)] px-5 py-4">
                <div className="flex items-start justify-between gap-4">
                  <DialogHeader className="space-y-2 text-left">
                    <div className="flex items-center gap-2">
                      <Badge className={`text-xs ${typeClass(selectedObservation.type)}`}>{selectedObservation.type}</Badge>
                      <Badge className={`text-xs ${levelClass(selectedObservation.level)}`}>{selectedObservation.level}</Badge>
                      <Badge className="bg-blue-500/10 text-blue-700 border-blue-200 text-xs">{selectedObservation.source}</Badge>
                    </div>
                    <DialogTitle className="break-words text-xl">{selectedObservation.name || 'Observation'}</DialogTitle>
                    <DialogDescription className="font-mono text-xs break-all">
                      Observation ID: {selectedObservation.observationId}
                    </DialogDescription>
                  </DialogHeader>
                  <div className="flex shrink-0 items-center gap-2">
                    {traceInsightMetrics ? (
                      <PanelInfoPopover
                        title="Trace Insights"
                        description={
                          <>
                            <p>
                              Summarizes the selected trace using the spans, errors, timings, model usage, and token counts already shown in this drawer.
                            </p>
                            <p>
                              Use it to explain slow traces, identify the dominant child operations, and call out suspicious error or SQL activity.
                            </p>
                          </>
                        }
                        panelKind="trace"
                        getMetrics={() => traceInsightMetrics}
                        triggerLabel="Trace insights"
                        voiceModeStorageKey="aonik:trace-insights:voice-mode"
                      />
                    ) : null}
                    {selectedObservation.aiRunId ? (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => navigate(`/ai/traces/${selectedObservation.aiRunId}`)}
                      >
                        <ExternalLink className="mr-2 h-3.5 w-3.5" />
                        Open run
                      </Button>
                    ) : null}
                    {(selectedObservation.level.toLowerCase() === 'error' || waterfallItems.some((item) => item.level.toLowerCase() === 'error'))
                      && errorOperationId ? (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => openExceptions(errorOperationId)}
                      >
                        <ExternalLink className="mr-2 h-3.5 w-3.5" />
                        View errors
                      </Button>
                    ) : null}
                    <DialogClose asChild>
                      <Button type="button" variant="ghost" size="sm" aria-label="Close details">
                        <X className="h-4 w-4" />
                      </Button>
                    </DialogClose>
                  </div>
                </div>
              </div>

              <div className="flex-1 overflow-y-auto px-5 py-5">
                <div className="mb-5 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                  <DetailMetric label="Started" value={formatDateTime(selectedObservation.startTime)} />
                  <DetailMetric label="Latency" value={formatSeconds(selectedObservation.latencySeconds)} />
                  <DetailMetric label="Cost" value={formatCost(selectedObservation.costUsd)} />
                  <DetailMetric label="TTFT" value={formatSeconds(selectedObservation.timeToFirstTokenSeconds)} />
                  <DetailMetric label="Agent" value={selectedObservation.agentName ?? selectedObservation.agentId ?? '--'} />
                  <DetailMetric label="Model" value={selectedObservation.providedModel ?? '--'} />
                  <DetailMetric label="Input Tokens" value={formatTokens(selectedObservation.inputTokens)} />
                  <DetailMetric label="Output Tokens" value={formatTokens(selectedObservation.outputTokens)} />
                  <DetailMetric label="Total Tokens" value={formatTokens(selectedObservation.totalTokens)} />
                </div>

                <div className="mb-5 grid gap-3 rounded-md border border-[var(--color-border-light)] p-3 text-sm md:grid-cols-2">
                  <div>
                    <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">Trace</div>
                    <div className="mt-1 font-mono text-xs break-all text-[var(--color-text-primary)]">{selectedObservation.traceName ?? selectedObservation.traceId}</div>
                  </div>
                  <div>
                    <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">Parent Observation</div>
                    <div className="mt-1 font-mono text-xs break-all text-[var(--color-text-primary)]">{selectedObservation.parentObservationId ?? '--'}</div>
                  </div>
                  <div>
                    <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">AI Run</div>
                    <div className="mt-1 font-mono text-xs break-all text-[var(--color-text-primary)]">{selectedObservation.aiRunId ?? '--'}</div>
                  </div>
                  <div>
                    <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">Root Observation</div>
                    <div className="mt-1 text-xs text-[var(--color-text-primary)]">{selectedObservation.isRootObservation ? 'Yes' : 'No'}</div>
                  </div>
                  <div>
                    <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">Span</div>
                    <div className="mt-1 font-mono text-xs break-all text-[var(--color-text-primary)]">{selectedObservation.spanId ?? selectedObservation.observationId}</div>
                  </div>
                  <div>
                    <div className="text-[10px] uppercase tracking-wide text-[var(--color-text-tertiary)]">Operation</div>
                    <div className="mt-1 font-mono text-xs break-all text-[var(--color-text-primary)]">{selectedObservation.operationId ?? selectedObservation.traceId}</div>
                  </div>
                </div>

                <div className="space-y-6">
                  <TraceWaterfall items={waterfallItems} loading={traceLoading} onViewExceptions={openExceptions} />
                  <PayloadBlock title="Input" value={selectedObservation.input} />
                  <PayloadBlock title="Output" value={selectedObservation.output} />
                  <MetadataTable value={selectedObservation.metadata} />
                </div>
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>
    </div>
  );
}
