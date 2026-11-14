import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { workflowService } from '@/services/workflowService';
import { connectorError } from '@/services/connectorService';
import { invoiceDecision } from './decisionParameters';

export function NewWorkflowButton() {
  const navigate = useNavigate();
  const [name, setName] = useState('Invoice approval'); const [slug, setSlug] = useState('invoice-approval');
  const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null);
  const create = async () => {
    setBusy(true); setError(null);
    try {
      const node = (key: string, kind: string, label: string, x: number, y: number, parameters: unknown) => ({ clientId: key, key, kind, label, summary: '', notes: '', x, y, paramsJson: JSON.stringify(parameters) });
      const edge = (from: string, to: string, index = 0, routeKey?: string) => ({ fromClientId: from, toClientId: to, fromIndex: index, label: routeKey ?? '', routeKey });
      const draft = await workflowService.create({ slug, name, description: 'Invoice handling and approval routing', state: 'Draft', version: 'v0.1', autoRetry: false, ownerColor: '', ownerAgentId: null, contributors: [], versionMessage: 'Create invoice approval draft',
        nodes: [node('start', 'trigger', 'Manual invoice', 0, 0, {}), node('route', 'decision', 'Invoice route', 280, 0, invoiceDecision()),
          node('issue', 'tool', 'Issue after human approval', 560, 0, { actionKey: 'finance.invoices.issue', contractVersion: 1, inputs: { invoiceId: { fact: 'invoice.id' } } }),
          node('done', 'end', 'Completed', 840, 0, { outcome: 'Completed' }), node('manual', 'end', 'Manual review', 560, 180, { outcome: 'NeedsManualReview' })],
        edges: [edge('start', 'route'), edge('route', 'issue', 0, 'standard'), edge('route', 'manual', 1, 'manual'), edge('issue', 'done')] });
      navigate(`/ai/workflows/${draft.slug}`);
    } catch (reason) { setError(connectorError(reason)); } finally { setBusy(false); }
  };
  return <Dialog><DialogTrigger asChild><Button size="sm"><Plus className="h-3 w-3" />New workflow</Button></DialogTrigger><DialogContent><DialogHeader><DialogTitle>Create invoice approval draft</DialogTitle></DialogHeader>
    <p className="text-sm text-muted-foreground">Starts with a decision table, safe manual fallback and an invoice action. Configure its reviewer role and test fixtures before requesting publication.</p>
    <label className="space-y-1 text-sm">Name<Input value={name} maxLength={200} onChange={event => setName(event.target.value)} /></label>
    <label className="space-y-1 text-sm">Stable slug<Input value={slug} maxLength={100} onChange={event => setSlug(event.target.value)} /></label>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <Button disabled={busy || !name.trim() || !/^[a-z0-9][a-z0-9-]*$/.test(slug)} onClick={() => void create()}>{busy ? 'Creating…' : 'Create draft'}</Button>
  </DialogContent></Dialog>;
}
