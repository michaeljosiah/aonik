import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { ServerApprovalCard, type ServerApprovalState } from '@/components/ai/chatSupport';
import { workflowService, type WorkflowRunDetail } from '@/services/workflowService';
import { connectorService, connectorError } from '@/services/connectorService';

export function WorkflowRunPage() {
  const { runId } = useParams<{ runId: string }>();
  const [run, setRun] = useState<WorkflowRunDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [reviewPath, setReviewPath] = useState<string | null>(null);
  const refresh = useCallback(async () => { if (runId) setRun(await workflowService.run(runId)); }, [runId]);
  useEffect(() => {
    let disposed = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const poll = async () => {
      try {
        if (!runId) return;
        const result = await workflowService.run(runId);
        if (disposed) return;
        setRun(result);
        setError(null);
        if (!['Completed', 'Cancelled', 'Failed'].includes(result.status)) timer = setTimeout(() => { void poll(); }, 5000);
      } catch (reason) {
        if (!disposed) { setError(connectorError(reason)); timer = setTimeout(() => { void poll(); }, 5000); }
      }
    };
    void poll();
    return () => { disposed = true; clearTimeout(timer); };
  }, [runId]);
  const act = async (operation: () => Promise<unknown>) => { setBusy(true); setError(null); try { await operation(); await refresh(); } catch (reason) { setError(connectorError(reason)); } finally { setBusy(false); } };
  if (!run) return <div className="p-6"><h1 className="text-xl font-semibold">Workflow run</h1><p role={error ? 'alert' : undefined} className="mt-3 text-sm text-muted-foreground">{error ?? 'Loading the recorded run…'}</p></div>;
  const pending = [...run.steps].reverse().find(step => step.actionKey?.startsWith('gmail.') && step.status !== 'Completed');
  return <div className="h-full space-y-5 overflow-auto p-6">
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h1 className="text-2xl font-semibold">Workflow run <Badge variant="outline">{run.status}</Badge></h1><p className="mt-2 break-all text-xs text-muted-foreground">{run.id}</p></div>
      <Button disabled={busy} variant="outline" onClick={() => void act(refresh)}>Refresh</Button></div>
    {error && <p role="alert" className="rounded-md border border-destructive p-3 text-sm text-destructive">{error}</p>}
    <Card><CardContent className="space-y-2 pt-5 text-sm"><p>Subject: {run.subjectType} · {run.subjectId}</p><p>Actor: {run.actorType} · {run.actorId}</p><p className="break-all">Published version: {run.workflowVersionId}</p><p className="break-all text-xs text-muted-foreground">Definition hash: {run.definitionHash}</p>
      {run.outcome && <p>Outcome: {run.outcome}</p>}{run.errorCode && <p>Held reason: {run.errorCode}</p>}
      <div className="flex flex-wrap gap-2 pt-2">
        {(run.status === 'WaitingForConnection' || run.status === 'NeedsAttention') && <Button disabled={busy} onClick={() => void act(() => workflowService.resume(run))}>Revalidate and resume this run</Button>}
        {!['Completed', 'Cancelled', 'Failed'].includes(run.status) && <Button disabled={busy} variant="outline" onClick={() => void act(() => workflowService.cancelRun(run))}>Cancel remaining work</Button>}
        {run.status === 'WaitingForConnection' && pending && <Button disabled={busy} variant="outline" onClick={() => void act(async () => {
          const request = await connectorService.createRequest({ connectorKey: 'gmail', ownership: 'User', actions: [{ key: pending.actionKey!, version: pending.actionVersion! }],
            purpose: 'Restore this published workflow’s exact account and action access', idempotencyKey: crypto.randomUUID(), runId: run.id });
          setReviewPath(request.reviewPath);
        })}>Request access for the pinned account</Button>}
        {reviewPath && <Link to={reviewPath} className="self-center text-primary underline">Review connection request</Link>}
      </div>
    </CardContent></Card>
    <section className="space-y-3" aria-label="Ordered workflow trace"><h2 className="text-lg font-semibold">Recorded steps</h2>
      {run.steps.map((step, index) => <Card key={step.id}><CardHeader><CardTitle className="flex flex-wrap items-center gap-2 text-base">{index + 1}. {step.nodeKey}<Badge variant="outline">{step.status}</Badge></CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
        {step.actionKey && <p>{step.actionKey} · v{step.actionVersion}</p>}{step.selectedRoute && <p>Selected route: {step.selectedRoute}</p>}{step.errorCode && <p>{step.errorCode}</p>}
        {step.approval && <ServerApprovalCard approval={{ id: step.approval.id, kind: step.approval.riskTier === 'High' ? 'high' : 'medium', approvalRequestId: step.approval.id,
          proposalId: step.proposalId ?? undefined, tool: step.actionKey ?? '', tier: step.approval.riskTier, actionKind: step.approval.actionKind,
          preview: step.approval.preview ?? undefined, message: `Status: ${step.approval.status}. Review expires ${new Date(step.approval.expiresAt).toLocaleString()}.`,
          status: busy ? 'deciding' : step.approval.status === 'Approved' ? 'approved' : step.approval.status === 'Rejected' ? 'rejected' : step.approval.status === 'Pending' ? 'pending' : 'error' } satisfies ServerApprovalState}
          onDecide={step.approval.canDecide && !busy ? (_approval, decision) => { void act(() => workflowService.decide(step.approval!.id, decision)); } : undefined} />}
        <details><summary className="cursor-pointer text-muted-foreground">Decision and action evidence</summary><pre className="mt-2 max-h-96 overflow-auto whitespace-pre-wrap break-all rounded-md border p-3 text-xs">{JSON.stringify({ invocationPath: step.invocationPath, decision: step.decisionTrace, result: step.result, approvalRequestId: step.approvalRequestId, proposalId: step.proposalId, aiRunId: step.aiRunId }, null, 2)}</pre></details>
      </CardContent></Card>)}
    </section>
  </div>;
}
