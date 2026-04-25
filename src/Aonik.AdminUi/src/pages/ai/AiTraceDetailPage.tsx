import { useEffect, useMemo, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Activity, AlertCircle, Copy, Loader2 } from 'lucide-react';

import { Breadcrumb } from '@/components/ui/breadcrumb';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { aiTraceService, type AiTraceRunDetailResponse } from '@/services/aiService';

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

function formatMs(value: number | null | undefined): string {
  if (value == null) return '--';
  if (value >= 1000) return `${(value / 1000).toFixed(1)}s`;
  return `${value}ms`;
}

function formatCost(value: number | null | undefined): string {
  if (value == null) return '--';
  return `$${value.toFixed(4)}`;
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '--';
  return new Date(value).toLocaleString();
}

export function AiTraceDetailPage() {
  const { runId } = useParams<{ runId: string }>();
  const requestIdRef = useRef(0);
  const [trace, setTrace] = useState<AiTraceRunDetailResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!runId) return;

    const load = async () => {
      const requestId = ++requestIdRef.current;
      setLoading(true);
      setError(null);

      try {
        const result = await aiTraceService.get(runId);
        if (requestIdRef.current !== requestId) return;
        setTrace(result);
      } catch (err) {
        if (requestIdRef.current !== requestId) return;
        const message = err && typeof err === 'object' && 'userMessage' in err
          ? String((err as { userMessage?: string }).userMessage ?? '')
          : '';
        setError(message || 'Failed to load AI trace.');
      } finally {
        if (requestIdRef.current === requestId) setLoading(false);
      }
    };

    void load();
  }, [runId]);

  const breadcrumbItems = useMemo(() => [
    { label: 'AI', href: '/ai/agents' },
    { label: 'AI Traces', href: '/ai/traces' },
    { label: runId ?? 'Trace', icon: <Activity className="h-3.5 w-3.5" /> },
  ], [runId]);

  const handleCopyRunId = async () => {
    if (!trace) return;
    await navigator.clipboard.writeText(trace.run.runId);
  };

  if (loading) {
    return (
      <div className="p-6 flex items-center justify-center">
        <Loader2 className="mr-2 h-5 w-5 animate-spin text-[var(--color-text-tertiary)]" />
        <span className="text-[var(--color-text-secondary)]">Loading AI trace...</span>
      </div>
    );
  }

  if (error || !trace) {
    return (
      <div className="p-6 space-y-4">
        <Breadcrumb items={breadcrumbItems} />
        <Card className="p-5 border-l-4 border-l-red-500">
          <div className="flex items-center gap-3 text-red-700">
            <AlertCircle className="h-5 w-5" />
            <div>
              <div className="font-medium">Failed to load AI trace</div>
              <div className="text-sm">{error ?? 'Trace not found.'}</div>
            </div>
          </div>
        </Card>
      </div>
    );
  }

  const { run, metrics } = trace;

  return (
    <div className="p-6 space-y-6">
      <div className="space-y-2">
        <Breadcrumb items={breadcrumbItems} className="mb-1" />
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-[var(--color-text-primary)]">Run Trace</h1>
            <p className="text-sm text-[var(--color-text-secondary)] font-mono break-all">{run.runId}</p>
          </div>
          <div className="flex items-center gap-2">
            <Badge className={`text-xs ${outcomeClass(run.outcome)}`}>{run.outcome}</Badge>
            <Badge className={`text-xs ${traceStatusClass(trace.traceStatus)}`}>
              {trace.traceStatus === 'DbAndTelemetry' ? 'DB + Telemetry' : 'DB only'}
            </Badge>
            <Button variant="outline" size="sm" onClick={() => void handleCopyRunId()}>
              <Copy className="mr-2 h-4 w-4" />
              Copy run ID
            </Button>
          </div>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Latency</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{formatMs(metrics?.latencyMs ?? run.latencyMs)}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">TTFT</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{formatMs(metrics?.ttftMs)}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Total Tokens</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{(metrics?.totalTokens ?? run.tokensUsed).toLocaleString()}</CardContent>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-sm">Estimated Cost</CardTitle>
          </CardHeader>
          <CardContent className="text-2xl font-semibold">{formatCost(metrics?.estimatedCostUsd ?? run.costEstimate)}</CardContent>
        </Card>
      </div>

      <Tabs defaultValue="overview" className="space-y-4">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          <TabsTrigger value="timeline">Timeline</TabsTrigger>
          <TabsTrigger value="raw">Raw Telemetry</TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Run Metadata</CardTitle>
              </CardHeader>
              <CardContent>
                <dl className="grid grid-cols-1 gap-3 text-sm md:grid-cols-2">
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Started</dt>
                    <dd>{formatDateTime(run.startedAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Completed</dt>
                    <dd>{formatDateTime(metrics?.completedAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Use Case</dt>
                    <dd className="font-mono text-xs">{run.useCase}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Configured Model</dt>
                    <dd className="font-mono text-xs">{run.aiModelName ?? run.aiModelId}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Requested Model</dt>
                    <dd className="font-mono text-xs">{metrics?.requestedModel ?? '--'}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Actual Model</dt>
                    <dd className="font-mono text-xs">{metrics?.actualModel ?? run.aiModelName ?? '--'}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Prompt Spec ID</dt>
                    <dd className="font-mono text-xs break-all">{run.promptSpecId ?? '--'}</dd>
                  </div>
                  <div>
                    <dt className="text-[var(--color-text-tertiary)]">Policy ID</dt>
                    <dd className="font-mono text-xs break-all">{run.aiPolicyId ?? '--'}</dd>
                  </div>
                </dl>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Input / Output</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4 text-sm">
                <div>
                  <div className="text-[var(--color-text-tertiary)] mb-1">Input References</div>
                  <pre className="rounded-md bg-[var(--color-surface-inset)] p-3 text-xs font-mono whitespace-pre-wrap break-words max-h-52 overflow-auto">{run.inputRefsJson}</pre>
                </div>
                <div>
                  <div className="text-[var(--color-text-tertiary)] mb-1">Output Reference</div>
                  <pre className="rounded-md bg-[var(--color-surface-inset)] p-3 text-xs font-mono whitespace-pre-wrap break-words max-h-40 overflow-auto">{run.outputRef ?? '--'}</pre>
                </div>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="timeline">
          <Card>
            <CardHeader>
              <CardTitle>Timeline</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {trace.timeline.map((event, index) => (
                  <div key={`${event.timestamp}-${event.eventType}-${index}`} className="border-l border-[var(--color-border-light)] pl-4">
                    <div className="flex flex-col gap-1 md:flex-row md:items-center md:justify-between">
                      <div className="font-medium text-[var(--color-text-primary)]">{event.title}</div>
                      <div className="text-xs text-[var(--color-text-tertiary)]">{formatDateTime(event.timestamp)}</div>
                    </div>
                    <div className="mt-1 flex items-center gap-2">
                      <span className="font-mono text-[11px] text-[var(--color-text-tertiary)]">{event.eventType}</span>
                      {event.status ? <Badge variant="outline" className="text-[10px]">{event.status}</Badge> : null}
                    </div>
                    {event.description ? <p className="mt-2 text-sm text-[var(--color-text-secondary)]">{event.description}</p> : null}
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="raw">
          <Card>
            <CardHeader>
              <CardTitle>Raw Telemetry</CardTitle>
            </CardHeader>
            <CardContent>
              {trace.rawTelemetry.length === 0 ? (
                <p className="text-sm text-[var(--color-text-tertiary)]">No correlated Application Insights telemetry was found for this run.</p>
              ) : (
                <div className="space-y-4">
                  {trace.rawTelemetry.map((event, index) => (
                    <div key={`${event.timestamp}-${index}`} className="rounded-md border border-[var(--color-border-light)] p-4">
                      <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                        <div className="font-medium text-[var(--color-text-primary)]">{event.message}</div>
                        <div className="text-xs text-[var(--color-text-tertiary)]">{formatDateTime(event.timestamp)}</div>
                      </div>
                      <div className="mt-3 grid gap-2 md:grid-cols-2">
                        {Object.entries(event.dimensions).map(([key, value]) => (
                          <div key={key} className="rounded bg-[var(--color-surface-inset)] px-3 py-2 text-xs">
                            <div className="text-[var(--color-text-tertiary)]">{key}</div>
                            <div className="font-mono break-all text-[var(--color-text-primary)]">{value ?? '--'}</div>
                          </div>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
