import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { AlertCircle, Copy } from 'lucide-react';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge, type BadgeProps } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageLoadingScreen } from '@/components/layout/PageLoadingScreen';
import { aiTraceService, type AiTraceRunDetailResponse } from '@/services/aiService';

type BadgeVariant = BadgeProps['variant'];

const outcomeVariant = (outcome: string): BadgeVariant => {
  switch (outcome.toLowerCase()) {
    case 'completed':
    case 'success':
      return 'success';
    case 'failed':
    case 'error':
      return 'destructive';
    default:
      return 'secondary';
  }
};

const traceStatusVariant = (status: string): BadgeVariant => {
  switch (status) {
    case 'DbAndTelemetry':
      return 'info';
    default:
      return 'warning';
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

  const handleCopyRunId = async () => {
    if (!trace) return;
    await navigator.clipboard.writeText(trace.run.runId);
  };

  if (loading) {
    return <PageLoadingScreen message="Loading AI trace" />;
  }

  if (error || !trace) {
    return (
      <div className="p-6 space-y-4">
        <Alert variant="destructive">
          <AlertCircle />
          <AlertTitle>Failed to load AI trace</AlertTitle>
          <AlertDescription>{error ?? 'Trace not found.'}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const { run, metrics } = trace;

  return (
    <div className="p-6 space-y-6">
      <div className="space-y-2">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-foreground">Run Trace</h1>
            <p className="text-sm text-muted-foreground font-mono break-all">{run.runId}</p>
          </div>
          <div className="flex items-center gap-2">
            <Badge variant={outcomeVariant(run.outcome)} className="text-xs">{run.outcome}</Badge>
            <Badge variant={traceStatusVariant(trace.traceStatus)} className="text-xs">
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
                    <dt className="text-muted-foreground">Started</dt>
                    <dd>{formatDateTime(run.startedAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Completed</dt>
                    <dd>{formatDateTime(metrics?.completedAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Use Case</dt>
                    <dd className="font-mono text-xs">{run.useCase}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Configured Model</dt>
                    <dd className="font-mono text-xs">{run.aiModelName ?? run.aiModelId}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Requested Model</dt>
                    <dd className="font-mono text-xs">{metrics?.requestedModel ?? '--'}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Actual Model</dt>
                    <dd className="font-mono text-xs">{metrics?.actualModel ?? run.aiModelName ?? '--'}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Prompt Spec ID</dt>
                    <dd className="font-mono text-xs break-all">{run.promptSpecId ?? '--'}</dd>
                  </div>
                  <div>
                    <dt className="text-muted-foreground">Policy ID</dt>
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
                  <div className="text-muted-foreground mb-1">Input References</div>
                  <pre className="rounded-md bg-muted p-3 text-xs font-mono whitespace-pre-wrap break-words max-h-52 overflow-auto">{run.inputRefsJson}</pre>
                </div>
                <div>
                  <div className="text-muted-foreground mb-1">Output Reference</div>
                  <pre className="rounded-md bg-muted p-3 text-xs font-mono whitespace-pre-wrap break-words max-h-40 overflow-auto">{run.outputRef ?? '--'}</pre>
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
                  <div key={`${event.timestamp}-${event.eventType}-${index}`} className="border-l border-border pl-4">
                    <div className="flex flex-col gap-1 md:flex-row md:items-center md:justify-between">
                      <div className="font-medium text-foreground">{event.title}</div>
                      <div className="text-xs text-muted-foreground">{formatDateTime(event.timestamp)}</div>
                    </div>
                    <div className="mt-1 flex items-center gap-2">
                      <span className="font-mono text-[11px] text-muted-foreground">{event.eventType}</span>
                      {event.status ? <Badge variant="outline" className="text-[10px]">{event.status}</Badge> : null}
                    </div>
                    {event.description ? <p className="mt-2 text-sm text-muted-foreground">{event.description}</p> : null}
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
                <p className="text-sm text-muted-foreground">No correlated Application Insights telemetry was found for this run.</p>
              ) : (
                <div className="space-y-4">
                  {trace.rawTelemetry.map((event, index) => (
                    <div key={`${event.timestamp}-${index}`} className="rounded-md border border-border p-4">
                      <div className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
                        <div className="font-medium text-foreground">{event.message}</div>
                        <div className="text-xs text-muted-foreground">{formatDateTime(event.timestamp)}</div>
                      </div>
                      <div className="mt-3 grid gap-2 md:grid-cols-2">
                        {Object.entries(event.dimensions).map(([key, value]) => (
                          <div key={key} className="rounded bg-muted px-3 py-2 text-xs">
                            <div className="text-muted-foreground">{key}</div>
                            <div className="font-mono break-all text-foreground">{value ?? '--'}</div>
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
