import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { ServerApprovalCard, type ServerApprovalState } from '@/components/ai/chatSupport';
import { workflowService, type WorkflowSimulation, type WorkflowValidation, type WorkflowVersionDto } from '@/services/workflowService';
import { connectorError } from '@/services/connectorService';
import { invoiceDecision } from './decisionParameters';
import type { WorkflowGraph } from './workflowTypes';

export function WorkflowQualificationPanel({ workflow, hasChanges, onClose, onChanged, rollback, onClearRollback, publishedHash }: {
  workflow: WorkflowGraph; hasChanges: boolean; onClose: () => void; onChanged: () => void; rollback?: WorkflowVersionDto; onClearRollback: () => void; publishedHash?: string | null;
}) {
  const navigate = useNavigate();
  const [fixture, setFixture] = useState(JSON.stringify({ facts: invoiceDecision().fixtures[0].facts, actionOutputs: {} }, null, 2));
  const [validation, setValidation] = useState<WorkflowValidation | null>(null);
  const [simulation, setSimulation] = useState<WorkflowSimulation | null>(null);
  const [approval, setApproval] = useState<ServerApprovalState | null>(null);
  const [invoiceId, setInvoiceId] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const act = async (operation: () => Promise<unknown>) => { setBusy(true); setError(null); try { await operation(); } catch (reason) { setError(connectorError(reason)); } finally { setBusy(false); } };
  const publish = async () => {
    const checked = await workflowService.validate(workflow.slug); setValidation(checked);
    if (!rollback && !checked.isValid) throw new Error('Resolve the saved draft diagnostics before publication.');
    if (rollback && (!rollback.publishedAt || !rollback.definitionHash)) throw new Error('Rollback requires an intact previously published snapshot.');
    const result = await workflowService.requestPublication({ slug: workflow.slug, expectedVersion: workflow.expectedVersion ?? '',
      definitionHash: rollback?.definitionHash ?? checked.definitionHash, expectedPublishedVersionId: workflow.publishedVersionId ?? null, targetVersionId: rollback?.id });
    setApproval({ id: result.approvalRequestId, approvalRequestId: result.approvalRequestId, proposalId: result.proposalId, kind: 'high', tool: 'aonik_request_publish_workflow', tier: 'High', actionKind: rollback ? `Rollback to ${rollback.tag}` : 'Publish saved workflow', status: 'pending', preview: result.preview });
  };
  return <div className="max-h-96 flex-none space-y-3 overflow-auto border-t bg-card p-4">
    <div className="flex items-center justify-between gap-3"><h2 className="font-semibold">Validate, simulate and launch</h2><Button variant="ghost" size="sm" onClick={onClose}>Close</Button></div>
    {hasChanges && <p className="text-sm text-muted-foreground">Save your changes before validating or simulating this revision.</p>}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <div className="grid gap-4 lg:grid-cols-2"><div className="space-y-3">
      <label className="block space-y-1 text-xs">Fixture facts and action outputs<Textarea value={fixture} rows={6} onChange={event => setFixture(event.target.value)} /></label>
      <div className="flex flex-wrap gap-2"><Button variant="outline" disabled={busy || hasChanges} onClick={() => void act(async () => { setValidation(await workflowService.validate(workflow.slug)); })}>Validate saved draft</Button>
        <Button variant="outline" disabled={busy || hasChanges} onClick={() => void act(async () => { setSimulation(await workflowService.simulate(workflow.slug, JSON.parse(fixture))); })}>Simulate fixture only</Button>
        <Button disabled={busy || hasChanges} onClick={() => void act(publish)}>{rollback ? `Request rollback to ${rollback.tag}` : 'Request publication'}</Button></div>
      <p className="text-xs text-muted-foreground">Simulation performs no live actions. Publication creates a governance proposal and shows its exact change for human approval.</p>
      {rollback && <Button variant="outline" size="sm" onClick={onClearRollback}>Return to saved draft publication</Button>}
      {workflow.publishedVersionId && workflow.state !== 'Retired' && <Button disabled={busy || hasChanges || !publishedHash} variant="outline" onClick={() => void act(async () => {
        const result = await workflowService.requestRetirement({ slug: workflow.slug, expectedVersion: workflow.expectedVersion ?? '', definitionHash: publishedHash!, expectedPublishedVersionId: workflow.publishedVersionId! });
        setApproval({ id: result.approvalRequestId, approvalRequestId: result.approvalRequestId, proposalId: result.proposalId, kind: 'high', tool: 'aonik_request_retire_workflow', tier: 'High', actionKind: 'Retire this published workflow', status: 'pending', preview: result.preview });
      })}>Request retirement</Button>}
      {!workflow.publishedVersionId && <div className="space-y-2"><Button disabled={busy || hasChanges} variant="outline" onClick={() => setDeleting(true)}>Delete unused draft</Button>
        {deleting && <div className="space-y-2 rounded-md border border-destructive p-3 text-sm"><p>Delete the unused draft “{workflow.name}”? Published and referenced workflows are protected by the server.</p><div className="flex gap-2"><Button disabled={busy} variant="destructive" onClick={() => void act(async () => { await workflowService.delete(workflow.slug, workflow.expectedVersion ?? ''); navigate('/ai/workflows'); })}>Confirm draft deletion</Button><Button variant="outline" onClick={() => setDeleting(false)}>Keep draft</Button></div></div>}</div>}
      {validation && <div className="space-y-1 text-xs"><p>{validation.isValid ? 'Saved draft is valid for publication.' : 'Publication is blocked.'}</p>{validation.diagnostics.map((item, index) => <p key={index}>{item.path}: {item.message}</p>)}</div>}
    </div><div className="space-y-3">
      {simulation && <details open><summary className="cursor-pointer text-sm">Fixture result: {simulation.status}</summary><pre className="max-h-48 overflow-auto whitespace-pre-wrap break-all rounded-md border p-2 text-xs">{JSON.stringify(simulation, null, 2)}</pre></details>}
      {approval && <ServerApprovalCard approval={busy ? { ...approval, status: 'deciding' } : approval} onDecide={(_value, decision) => { void act(async () => {
        await workflowService.decide(approval.approvalRequestId!, decision); setApproval({ ...approval, status: decision === 'Approve' ? 'approved' : 'rejected' }); onChanged();
      }); }} />}
      {workflow.publishedVersionId && workflow.state === 'Active' && <div className="space-y-2 rounded-md border p-3"><label className="block space-y-1 text-xs">Stored invoice ID for a live run<Input value={invoiceId} onChange={event => setInvoiceId(event.target.value)} /></label>
        <p className="text-xs text-muted-foreground">This starts version {workflow.publishedVersionId}. Its financial actions still require their own human approval.</p>
        <Button disabled={busy || !/^[0-9a-f-]{36}$/i.test(invoiceId)} onClick={() => void act(async () => { const run = await workflowService.start(workflow.slug, workflow.publishedVersionId!, invoiceId, crypto.randomUUID()); navigate(`/ai/workflow-runs/${run.id}`); })}>Start live invoice run</Button>
      </div>}
      <Link to="/approvals" className="text-sm text-primary underline">Open approvals</Link>
    </div></div>
  </div>;
}
