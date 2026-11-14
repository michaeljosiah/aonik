import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { workflowService } from '@/services/workflowService';
import { connectorError } from '@/services/connectorService';
import { invoiceDecision } from './decisionParameters';

export function NewWorkflowButton() {
  const navigate = useNavigate();
  const [name, setName] = useState('Invoice approval'); const [slug, setSlug] = useState('invoice-approval');
  const [kind, setKind] = useState('Workflow');
  const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null);
  const create = async () => {
    setBusy(true); setError(null);
    try {
      const node = (key: string, kind: string, label: string, x: number, y: number, parameters: unknown) => ({ clientId: key, key, kind, label, summary: '', notes: '', x, y, paramsJson: JSON.stringify(parameters) });
      const edge = (from: string, to: string, index = 0, routeKey?: string) => ({ fromClientId: from, toClientId: to, fromIndex: index, label: routeKey ?? '', routeKey });
      const library = kind === 'DecisionLibrary'; const table = invoiceDecision();
      const libraryContract = { schemaVersion: 1, inputs: [{ name: 'invoice.id', type: 'Id', required: true }, { name: 'invoice.total', type: 'Decimal', required: true }, { name: 'invoice.currency', type: 'Currency', required: true }, { name: 'invoice.status', type: 'String', required: true }], routes: ['standard', 'manual'], outputType: 'Route' };
      const draft = await workflowService.create({ slug, name, description: 'Invoice handling and approval routing', state: 'Draft', version: 'v0.1', autoRetry: false, ownerColor: '', ownerAgentId: null, contributors: [], versionMessage: 'Create invoice approval draft',
        definitionKind: kind, schemaVersion: library ? 2 : 1,
        nodes: [node('start', 'trigger', library ? 'Declared inputs' : 'Manual invoice', 0, 0, library ? { libraryContract } : {}), node('route', 'decision', 'Invoice route', 280, 0, library ? { ...table, schemaVersion: 2, currency: 'GBP', optionalFactBehavior: 'Unknown' } : table),
          ...library ? [] : [node('issue', 'tool', 'Issue after human approval', 560, 0, { actionKey: 'finance.invoices.issue', contractVersion: 1, inputs: { invoiceId: { fact: 'invoice.id' } } })],
          node('done', 'end', 'Completed', 840, 0, library ? {} : { outcome: 'Completed' }), node('manual', 'end', 'Manual review', 560, 180, library ? {} : { outcome: 'NeedsManualReview' })],
        edges: [edge('start', 'route'), edge('route', library ? 'done' : 'issue', 0, 'standard'), edge('route', 'manual', 1, 'manual'), ...library ? [] : [edge('issue', 'done')]] });
      navigate(`/ai/workflows/${draft.slug}`);
    } catch (reason) { setError(connectorError(reason)); } finally { setBusy(false); }
  };
  return <Dialog><DialogTrigger asChild><Button size="sm"><Plus className="h-3 w-3" />New workflow</Button></DialogTrigger><DialogContent><DialogHeader><DialogTitle>Create decision workflow or library</DialogTitle></DialogHeader>
    <label className="space-y-1 text-sm">Definition type<NativeSelect value={kind} onChange={event => setKind(event.target.value)}><option value="Workflow">Invoice workflow</option><option value="DecisionLibrary">Reusable decision library</option></NativeSelect></label>
    <p className="text-sm text-muted-foreground">{kind === 'DecisionLibrary' ? 'Creates one pure table with declared inputs and output routes. Consumers explicitly pin its published version.' : 'Starts with a decision table, safe manual fallback and an invoice action. Configure its reviewer role and test fixtures before requesting publication.'}</p>
    <label className="space-y-1 text-sm">Name<Input value={name} maxLength={200} onChange={event => setName(event.target.value)} /></label>
    <label className="space-y-1 text-sm">Stable slug<Input value={slug} maxLength={100} onChange={event => setSlug(event.target.value)} /></label>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    <Button disabled={busy || !name.trim() || !/^[a-z0-9][a-z0-9-]*$/.test(slug)} onClick={() => void create()}>{busy ? 'Creating…' : 'Create draft'}</Button>
  </DialogContent></Dialog>;
}
