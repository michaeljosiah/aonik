import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Activity, AlertCircle, Braces, Copy, ExternalLink, Loader2, Search, X } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
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

export function AiTracesPage() {
  const navigate = useNavigate();
  const requestIdRef = useRef(0);

  const [items, setItems] = useState<AiTraceObservationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(50);
  const [totalCount, setTotalCount] = useState(0);
  const [provider, setProvider] = useState('Auto');
  const [name, setName] = useState('');
  const [traceName, setTraceName] = useState('');
  const [type, setType] = useState('all');
  const [level, setLevel] = useState('all');
  const [rootFilter, setRootFilter] = useState('all');
  const [timeRange, setTimeRange] = useState('24h');
  const [selectedObservation, setSelectedObservation] = useState<AiTraceObservationResponse | null>(null);

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
                </div>

                <div className="space-y-6">
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
