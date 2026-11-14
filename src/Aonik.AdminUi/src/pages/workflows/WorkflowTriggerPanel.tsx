import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { NativeSelect } from '@/components/ui/native-select';
import { Checkbox } from '@/components/ui/checkbox';
import { ServerApprovalCard, type ServerApprovalState } from '@/components/ai/chatSupport';
import { automationIdentityService, type AutomationIdentity } from '@/services/automationIdentityService';
import { connectorService, connectorError, type ConnectorConnection } from '@/services/connectorService';
import { workflowService, type WorkflowVersionDto } from '@/services/workflowService';
import { workflowTriggerService, type TriggerDefinition, type WorkflowTrigger } from '@/services/workflowTriggerService';

export function WorkflowTriggerPanel({ slug, workflowId, publishedVersionId }: { slug: string; workflowId: string; publishedVersionId?: string | null }) {
  const [items, setItems] = useState<WorkflowTrigger[]>([]); const [actors, setActors] = useState<AutomationIdentity[]>([]);
  const [versions, setVersions] = useState<WorkflowVersionDto[]>([]); const [accounts, setAccounts] = useState<ConnectorConnection[]>([]);
  const [editor, setEditor] = useState<WorkflowTrigger | 'new' | null>(null); const [reviewDelete, setReviewDelete] = useState<WorkflowTrigger | null>(null);
  const [approval, setApproval] = useState<ServerApprovalState | null>(null); const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null);
  const [diagnostics, setDiagnostics] = useState<string | null>(null);
  const refresh = async () => setItems(await workflowTriggerService.list(slug));
  useEffect(() => { let disposed = false; void Promise.all([workflowTriggerService.list(slug), automationIdentityService.list(), workflowService.listVersions(workflowId)])
    .then(([triggers, identities, snapshots]) => { if (!disposed) { setItems(triggers); setActors(identities); setVersions(snapshots.filter(item => item.publishedAt)); } })
    .catch(reason => { if (!disposed) setError(connectorError(reason)); }); return () => { disposed = true; }; }, [slug, workflowId]);
  const act = async (operation: () => Promise<unknown>) => { setBusy(true); setError(null); try { await operation(); } catch (reason) { setError(connectorError(reason)); } finally { setBusy(false); } };
  return <section className="space-y-3 border-t pt-4" aria-label="Automatic workflow triggers"><h3 className="font-semibold">Automatic triggers</h3>
    <p className="text-sm text-muted-foreground">Pin a published version, automation identity and source. Activation needs a human governance approval. Each financial action still has its own approval.</p>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {items.map(item => <div key={item.id} className="space-y-2 rounded-md border p-3 text-sm"><p className="font-medium">{item.name} · {item.status} · {item.draft.source}</p>
      <p className="text-xs text-muted-foreground">Active revision {item.activeRevision ?? 'none'} · {item.admittedRuns} admitted runs{item.lastError && ` · ${item.lastError}`}</p>
      <p className="break-all text-xs">Pinned workflow {item.active?.workflowVersionId ?? item.draft.workflowVersionId} · Actor {item.active?.automationIdentityId ?? item.draft.automationIdentityId}</p>
      <div className="flex flex-wrap gap-2"><Button variant="outline" size="sm" disabled={busy} onClick={() => { setEditor(item); setApproval(null); if (item.draft.mailbox) void connectorService.connections('gmail').then(setAccounts).catch(reason => setError(connectorError(reason))); }}>Edit draft</Button>
        <Button variant="outline" size="sm" disabled={busy} onClick={() => void act(async () => { const result = await workflowTriggerService.validate(slug, item.id); setDiagnostics(result.diagnostics.map(value => `${value.path}: ${value.message}`).join('\n') || 'The saved trigger is ready for activation review.'); })}>Validate trigger</Button>
        {[false, ...(item.status === 'Active' ? [true] : [])].map(disabling => <Button key={String(disabling)} size="sm" variant="outline" disabled={busy} onClick={() => void act(async () => {
          const result = await workflowTriggerService.request(slug, item, disabling); setEditor(null); setApproval({ id: result.approvalRequestId, approvalRequestId: result.approvalRequestId, proposalId: result.proposalId,
            kind: 'high', tier: 'High', tool: disabling ? 'aonik_request_disable_workflow_trigger' : 'aonik_request_activate_workflow_trigger', actionKind: disabling ? 'Disable exact trigger' : 'Activate exact trigger', status: 'pending', preview: result.preview });
        })}>{disabling ? 'Request disable' : 'Request activation'}</Button>)}
        {item.activeRevision === null && <Button variant="outline" size="sm" disabled={busy} onClick={() => setReviewDelete(item)}>Delete unused draft</Button>}
      </div></div>)}
    {diagnostics && <p className="whitespace-pre-wrap text-sm" role="status">{diagnostics}</p>}
    {reviewDelete && <div className="space-y-2 rounded-md border border-destructive p-3 text-sm"><p>Delete unused trigger draft “{reviewDelete.name}”?</p><Button variant="destructive" disabled={busy} onClick={() => void act(async () => { await workflowTriggerService.delete(slug, reviewDelete); setReviewDelete(null); await refresh(); })}>Confirm draft deletion</Button><Button variant="outline" onClick={() => setReviewDelete(null)}>Keep draft</Button></div>}
    {approval && <ServerApprovalCard approval={busy ? { ...approval, status: 'deciding' } : approval} onDecide={(_value, decision) => { void act(async () => {
      await workflowService.decide(approval.approvalRequestId!, decision); setApproval({ ...approval, status: decision === 'Approve' ? 'approved' : 'rejected' }); await refresh();
    }); }} />}
    <div className="flex flex-wrap gap-2"><Button variant="outline" disabled={busy || !publishedVersionId} onClick={() => { setEditor('new'); setApproval(null); void connectorService.connections('gmail').then(setAccounts).catch(reason => setError(connectorError(reason))); }}>New automatic trigger</Button><Button variant="outline" disabled={busy} onClick={() => void act(refresh)}>Refresh triggers</Button><Link className="self-center text-sm text-primary underline" to="/settings/tenant-integrations">Manage identities, connections and tenant policy</Link></div>
    {editor && <TriggerEditor key={editor === 'new' ? 'new' : `${editor.id}:${editor.expectedVersion}`} item={editor === 'new' ? null : editor} versionId={publishedVersionId ?? ''} versions={versions} actors={actors}
      accounts={accounts} workflowId={workflowId} onLoadAccounts={() => void connectorService.connections('gmail').then(setAccounts).catch(reason => setError(connectorError(reason)))}
      onClose={() => setEditor(null)} onSave={(name, definition) => act(async () => { await workflowTriggerService.save(slug, editor === 'new' ? null : editor, name, definition); setEditor(null); await refresh(); })} />}
  </section>;
}

function TriggerEditor({ item, versionId, versions, actors, accounts, workflowId, onLoadAccounts, onSave, onClose }: {
  item: WorkflowTrigger | null; versionId: string; versions: WorkflowVersionDto[]; actors: AutomationIdentity[]; accounts: ConnectorConnection[]; workflowId: string;
  onLoadAccounts: () => void; onSave: (name: string, definition: TriggerDefinition) => Promise<void>; onClose: () => void;
}) {
  const [name, setName] = useState(item?.name ?? 'Invoice approval routing');
  const [definition, setDefinition] = useState<TriggerDefinition>(item?.draft ?? { schemaVersion: 1, source: 'InvoiceCreated', workflowVersionId: versionId, automationIdentityId: '', subject: { type: 'Invoice', mapping: 'EventSubject', fixedIds: [] },
    limits: { maximumStartsPerHour: 10, maximumConcurrentRuns: 1, maximumCausationDepth: 4, ignoreSelfGenerated: true }, schedule: null, mailbox: null });
  const [review, setReview] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null); const [path, setPath] = useState<string | null>(null);
  const selection = useRef(0);
  const update = (patch: Partial<TriggerDefinition>) => { setDefinition(current => ({ ...current, ...patch })); setReview(false); };
  const mailbox = definition.mailbox; const schedule = definition.schedule;
  return <div className="space-y-3 rounded-md border p-3"><h4 className="font-medium">{item ? 'Edit' : 'Create'} trigger draft</h4>{error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {!review && <>
      <label className="block space-y-1 text-sm">Trigger name<Input value={name} onChange={event => setName(event.target.value)} maxLength={160} /></label>
      <label className="block space-y-1 text-sm">Published workflow version<NativeSelect value={definition.workflowVersionId} onChange={event => update({ workflowVersionId: event.target.value, automationIdentityId: '' })}>{versions.map(version => <option key={version.id} value={version.id}>{version.tag} · {version.id}</option>)}</NativeSelect></label>
      <label className="block space-y-1 text-sm">Automation identity<NativeSelect value={definition.automationIdentityId} onChange={event => update({ automationIdentityId: event.target.value })}><option value="">Choose an explicitly granted identity</option>{actors.filter(actor => actor.status === 'Active' && actor.grants.some(grant => grant.workflowVersionId === definition.workflowVersionId)).map(actor => <option key={actor.id} value={actor.id}>{actor.name}</option>)}</NativeSelect></label>
      <label className="block space-y-1 text-sm">Source<NativeSelect value={definition.source} onChange={event => {
        const source = event.target.value as TriggerDefinition['source']; update({ source, subject: { type: 'Invoice', mapping: source === 'Schedule' || source === 'GmailMessageAdded' ? 'Fixed' : 'EventSubject', fixedIds: [] },
          schedule: source === 'Schedule' ? { cron: '0 0 9 * * ?', timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, startAtUtc: new Date().toISOString(), endAtUtc: new Date(Date.now() + 30 * 86400000).toISOString(), maximumOccurrences: 30, maximumCatchUpOccurrences: 0 } : null,
          mailbox: source === 'GmailMessageAdded' ? { connectionId: '', workflowGrantId: '', labelIds: ['INBOX'], mode: 'Polling', pollIntervalMinutes: 15, allowPollingFallback: false, backfillSinceUtc: null } : null }); if (source === 'GmailMessageAdded') onLoadAccounts();
      }}><option value="InvoiceCreated">Invoice created</option><option value="InvoiceIssued">Invoice issued</option><option value="Schedule">Recurring schedule</option><option value="GmailMessageAdded">Gmail message added</option></NativeSelect></label>
      {definition.subject.mapping === 'Fixed' && <label className="block space-y-1 text-sm">{mailbox ? 'One stored invoice ID for this mailbox route' : 'Stored invoice IDs, separated by commas'}<Textarea value={definition.subject.fixedIds.join(', ')} onChange={event => update({ subject: { ...definition.subject, fixedIds: event.target.value.split(/[\s,]+/).filter(Boolean) } })} /></label>}
      {schedule && <div className="grid gap-3 md:grid-cols-2">{(['cron', 'timeZone', 'startAtUtc', 'endAtUtc'] as const).map(key => <label key={key} className="block space-y-1 text-sm">{{ cron: 'Quartz cron', timeZone: 'IANA timezone', startAtUtc: 'Start (UTC ISO date)', endAtUtc: 'End (UTC ISO date)' }[key]}<Input value={schedule[key]} onChange={event => update({ schedule: { ...schedule, [key]: event.target.value } })} /></label>)}
        {(['maximumOccurrences', 'maximumCatchUpOccurrences'] as const).map(key => <label key={key} className="block space-y-1 text-sm">{key === 'maximumOccurrences' ? 'Maximum occurrences' : 'Maximum catch-up occurrences (0 skips missed times)'}<Input type="number" min={key === 'maximumOccurrences' ? 1 : 0} max={key === 'maximumOccurrences' ? 10000 : 10} value={schedule[key]} onChange={event => update({ schedule: { ...schedule, [key]: Number(event.target.value) } })} /></label>)}</div>}
      {mailbox && <div className="space-y-3"><label className="block space-y-1 text-sm">Pinned Gmail account<NativeSelect value={mailbox.connectionId} onChange={event => {
        const connectionId = event.target.value; const request = ++selection.current; update({ mailbox: { ...mailbox, connectionId, workflowGrantId: '' } });
        if (connectionId) void connectorService.binding(connectionId, workflowId, { key: 'gmail.messages.search', version: 1 }).then(binding => { if (selection.current === request) setDefinition(current => current.mailbox?.connectionId === connectionId ? { ...current, mailbox: { ...current.mailbox, workflowGrantId: binding.workflowGrantId } } : current); }).catch(reason => setError(connectorError(reason)));
      }}><option value="">Choose an authorized account</option>{accounts.map(account => <option key={account.id} value={account.id}>{account.displayName} · {account.status}</option>)}</NativeSelect></label>
        <label className="block space-y-1 text-sm">Pinned workflow grant ID<Input value={mailbox.workflowGrantId} onChange={event => update({ mailbox: { ...mailbox, workflowGrantId: event.target.value } })} /></label>
        <label className="block space-y-1 text-sm">Required label IDs, separated by commas<Input value={mailbox.labelIds.join(', ')} onChange={event => update({ mailbox: { ...mailbox, labelIds: event.target.value.split(/[\s,]+/).filter(Boolean) } })} /></label>
        <label className="block space-y-1 text-sm">Synchronization mode<NativeSelect value={mailbox.mode} onChange={event => update({ mailbox: { ...mailbox, mode: event.target.value as 'Polling' | 'PushWithPolling' } })}><option value="Polling">Polling</option><option value="PushWithPolling">Authenticated push with polling recovery</option></NativeSelect></label>
        <label className="block space-y-1 text-sm">Polling interval (minutes)<Input type="number" min={5} max={1440} value={mailbox.pollIntervalMinutes} onChange={event => update({ mailbox: { ...mailbox, pollIntervalMinutes: Number(event.target.value) } })} /></label>
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={mailbox.allowPollingFallback} onCheckedChange={checked => update({ mailbox: { ...mailbox, allowPollingFallback: checked === true } })} />Approve polling if push cannot be provisioned or another watch owns this account</label>
        <label className="block space-y-1 text-sm">Optional historical backfill since (UTC, at most seven days)<Input value={mailbox.backfillSinceUtc ?? ''} onChange={event => update({ mailbox: { ...mailbox, backfillSinceUtc: event.target.value || null } })} /></label>
        <p className="text-xs text-muted-foreground">The actor and workflow both need explicit search access to this account. Mailbox content cannot authorize an invoice write.</p>
        <Button variant="outline" disabled={busy} onClick={() => { setBusy(true); void connectorService.createRequest({ connectorKey: 'gmail', ownership: accounts.find(account => account.id === mailbox.connectionId)?.ownership ?? 'User', connectionId: mailbox.connectionId || undefined, workflowId,
          actions: [{ key: 'gmail.messages.search', version: 1 }], purpose: 'Authorize this automatic mailbox trigger', idempotencyKey: crypto.randomUUID() }).then(request => setPath(request.reviewPath)).catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Request missing connection access</Button>{path && <Link className="block text-sm text-primary underline" to={path}>Review connection request</Link>}
      </div>}
      <div className="grid gap-3 md:grid-cols-3">{(['maximumStartsPerHour', 'maximumConcurrentRuns', 'maximumCausationDepth'] as const).map(key => <label key={key} className="block space-y-1 text-sm">{{ maximumStartsPerHour: 'Starts per hour', maximumConcurrentRuns: 'Concurrent runs', maximumCausationDepth: 'Causation depth' }[key]}<Input type="number" min={1} max={{ maximumStartsPerHour: 1000, maximumConcurrentRuns: 10, maximumCausationDepth: 8 }[key]} value={definition.limits[key]} onChange={event => update({ limits: { ...definition.limits, [key]: Number(event.target.value) } })} /></label>)}</div>
      <label className="flex items-center gap-2 text-sm"><Checkbox checked={definition.limits.ignoreSelfGenerated} onCheckedChange={checked => update({ limits: { ...definition.limits, ignoreSelfGenerated: checked === true } })} />Ignore self-generated source events</label>
      <Button disabled={busy || !name.trim() || !definition.automationIdentityId} onClick={() => setReview(true)}>Review exact draft</Button>
    </>}
    {review && <div className="space-y-3 text-sm"><p>Save this trigger draft as “{name}”? Its active revision changes only after a separate activation proposal.</p>{item && <details><summary>Current draft</summary><pre className="max-h-48 overflow-auto whitespace-pre-wrap break-all text-xs">{JSON.stringify(item.draft, null, 2)}</pre></details>}<pre className="max-h-64 overflow-auto whitespace-pre-wrap break-all rounded-md border p-2 text-xs">{JSON.stringify(definition, null, 2)}</pre>
      <Button disabled={busy} onClick={() => { setBusy(true); void onSave(name, definition).finally(() => setBusy(false)); }}>Confirm draft save</Button><Button variant="outline" disabled={busy} onClick={() => setReview(false)}>Edit configuration</Button></div>}
    <Button variant="outline" disabled={busy} onClick={onClose}>Close trigger editor</Button>
  </div>;
}
