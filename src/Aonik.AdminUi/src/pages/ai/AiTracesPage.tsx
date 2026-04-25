import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Activity, AlertCircle, Loader2, Search } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { DataTable, type ColumnDef } from '@/components/ui/data-table/data-table';
import type { AiTraceListItemResponse } from '@/services/aiService';
import { aiTraceService } from '@/services/aiService';

const breadcrumbItems = [
  { label: 'AI', href: '/ai/agents' },
  { label: 'AI Traces', icon: <Activity className="h-3.5 w-3.5" /> },
];

const outcomeClass = (outcome: string) => {
  switch (outcome.toLowerCase()) {
    case 'completed':
    case 'success':
      return 'bg-green-500/10 text-green-700 border-green-200';
    case 'failed':
    case 'error':
      return 'bg-red-500/10 text-red-700 border-red-200';
    default:
      return 'bg-gray-500/10 text-gray-700 border-gray-200';
  }
};

const traceStatusClass = (status: string) => {
  switch (status) {
    case 'DbAndTelemetry':
      return 'bg-blue-500/10 text-blue-700 border-blue-200';
    default:
      return 'bg-amber-500/10 text-amber-700 border-amber-200';
  }
};

function formatRelativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

function formatMs(value: number | null): string {
  if (value == null) return '--';
  if (value >= 1000) return `${(value / 1000).toFixed(1)}s`;
  return `${value}ms`;
}

function formatTokens(value: number | null): string {
  if (value == null) return '--';
  return value.toLocaleString();
}

function formatCost(value: number | null): string {
  if (value == null) return '--';
  return `$${value.toFixed(4)}`;
}

export function AiTracesPage() {
  const navigate = useNavigate();
  const requestIdRef = useRef(0);

  const [items, setItems] = useState<AiTraceListItemResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [useCase, setUseCase] = useState('');
  const [runId, setRunId] = useState('');
  const [outcome, setOutcome] = useState('all');
  const [timeRange, setTimeRange] = useState('24h');

  const load = useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setLoading(true);
    setError(null);

    try {
      const result = await aiTraceService.list({
        page,
        pageSize,
        useCase: useCase.trim() || undefined,
        outcome: outcome === 'all' ? undefined : outcome,
        timeRange,
        runId: runId.trim() || undefined,
      });

      if (requestIdRef.current !== requestId) return;

      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (err) {
      if (requestIdRef.current !== requestId) return;
      const message = err && typeof err === 'object' && 'userMessage' in err
        ? String((err as { userMessage?: string }).userMessage ?? '')
        : '';
      setError(message || 'Failed to load AI traces.');
      setItems([]);
    } finally {
      if (requestIdRef.current === requestId) setLoading(false);
    }
  }, [outcome, page, pageSize, runId, timeRange, useCase]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns = useMemo<ColumnDef<AiTraceListItemResponse>[]>(() => [
    {
      id: 'startedAt',
      header: 'Started',
      accessorKey: 'startedAt',
      sortable: true,
      cell: (row) => (
        <div>
          <div className="text-sm text-[var(--color-text-primary)]">{formatRelativeTime(row.startedAt)}</div>
          <div className="text-xs text-[var(--color-text-tertiary)]">{new Date(row.startedAt).toLocaleString()}</div>
        </div>
      ),
    },
    {
      id: 'useCase',
      header: 'Use Case',
      accessorKey: 'useCase',
      sortable: true,
      cell: (row) => <span className="font-mono text-xs text-[var(--color-text-primary)]">{row.useCase}</span>,
    },
    {
      id: 'outcome',
      header: 'Outcome',
      accessorKey: 'outcome',
      sortable: true,
      cell: (row) => <Badge className={`text-xs ${outcomeClass(row.outcome)}`}>{row.outcome}</Badge>,
    },
    {
      id: 'model',
      header: 'Model',
      accessorFn: (row) => row.actualModel ?? row.requestedModel ?? '',
      cell: (row) => <span className="font-mono text-xs text-[var(--color-text-secondary)]">{row.actualModel ?? row.requestedModel ?? '--'}</span>,
    },
    {
      id: 'latencyMs',
      header: 'Latency',
      accessorFn: (row) => row.latencyMs ?? -1,
      sortable: true,
      cell: (row) => <span>{formatMs(row.latencyMs)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'ttftMs',
      header: 'TTFT',
      accessorFn: (row) => row.ttftMs ?? -1,
      sortable: true,
      cell: (row) => <span>{formatMs(row.ttftMs)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'totalTokens',
      header: 'Tokens',
      accessorFn: (row) => row.totalTokens ?? -1,
      sortable: true,
      cell: (row) => <span>{formatTokens(row.totalTokens)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'estimatedCostUsd',
      header: 'Cost',
      accessorFn: (row) => row.estimatedCostUsd ?? -1,
      sortable: true,
      cell: (row) => <span>{formatCost(row.estimatedCostUsd)}</span>,
      className: 'text-right',
      headerClassName: 'text-right',
    },
    {
      id: 'traceStatus',
      header: 'Trace',
      accessorKey: 'traceStatus',
      cell: (row) => <Badge className={`text-xs ${traceStatusClass(row.traceStatus)}`}>{row.traceStatus === 'DbAndTelemetry' ? 'DB + Telemetry' : 'DB only'}</Badge>,
    },
  ], []);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <div className="p-6 space-y-6">
      <div className="space-y-2">
        <Breadcrumb items={breadcrumbItems} className="mb-1" />
        <div>
          <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">AI Traces</h1>
          <p className="text-sm text-[var(--color-text-secondary)]">Inspect AI runs using database audit records enriched with Application Insights telemetry.</p>
        </div>
      </div>

      <Card className="p-4 space-y-4">
        <div className="grid gap-3 md:grid-cols-4">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--color-text-tertiary)]" />
            <Input value={useCase} onChange={(e) => setUseCase(e.target.value)} placeholder="Filter by use case" className="pl-9" />
          </div>
          <Input value={runId} onChange={(e) => setRunId(e.target.value)} placeholder="Filter by run ID" />
          <Select value={outcome} onValueChange={setOutcome}>
            <SelectTrigger>
              <SelectValue placeholder="Outcome" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All outcomes</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Failed">Failed</SelectItem>
              <SelectItem value="Started">Started</SelectItem>
            </SelectContent>
          </Select>
          <Select value={timeRange} onValueChange={setTimeRange}>
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
          <p className="text-sm text-[var(--color-text-tertiary)]">{totalCount.toLocaleString()} traces</p>
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
              <div className="font-medium">Failed to load AI traces</div>
              <div className="text-sm">{error}</div>
            </div>
          </div>
        </Card>
      ) : null}

      <Card className="p-4">
        <DataTable
          data={items}
          columns={columns}
          getRowId={(row) => row.runId}
          showCheckboxes={false}
          loading={loading}
          loadingMessage="Loading AI traces..."
          emptyIcon={<Activity className="h-10 w-10 text-[var(--color-text-tertiary)]" />}
          emptyTitle="No AI traces found"
          emptyDescription="Adjust your filters or expand the time range."
          onRowClick={(row) => navigate(`/ai/traces/${row.runId}`)}
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
    </div>
  );
}
