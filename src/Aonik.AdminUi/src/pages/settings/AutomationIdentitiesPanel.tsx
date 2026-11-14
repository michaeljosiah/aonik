import { useCallback, useEffect, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { NativeSelect } from '@/components/ui/native-select';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { automationIdentityService, type AutomationIdentity, type AutomationIdentityCommand, type AutomationUsage, type AutomationGrant, type AutomationCapability } from '@/services/automationIdentityService';
import { connectorError } from '@/services/connectorService';
import { workflowService, type WorkflowSummaryDto, type WorkflowVersionDto } from '@/services/workflowService';

export function AutomationIdentitiesPanel() {
  const [identities, setIdentities] = useState<AutomationIdentity[]>([]);
  const [editor, setEditor] = useState<AutomationIdentity | 'new' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [disable, setDisable] = useState<{ identity: AutomationIdentity; usage: AutomationUsage } | null>(null);
  const [busy, setBusy] = useState(false);
  const refresh = useCallback(async () => { setIdentities(await automationIdentityService.list()); setError(null); }, []);
  useEffect(() => { void automationIdentityService.list().then(items => { setIdentities(items); setError(null); }).catch(reason => setError(connectorError(reason))); }, []);
  return <Card><CardHeader><CardTitle>Workflow automation identities</CardTitle></CardHeader><CardContent className="space-y-4">
    <p className="text-sm text-muted-foreground">Give unattended work an independent tenant identity and exact access. Invoice writes still wait for a human. Administration requires Integration.Manage and Workflow.Write.</p>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {identities.map(identity => <div key={identity.id} className="space-y-2 rounded-md border p-3 text-sm"><p className="font-medium">{identity.name} <Badge variant="outline">{identity.status}</Badge></p><p className="break-all text-xs text-muted-foreground">Identity: {identity.id} · Accountable administrator: {identity.accountableUserId}</p>
      <GrantSummary grants={identity.grants} />
      {identity.expiresAt && <p className="text-xs">Expires {new Date(identity.expiresAt).toLocaleString()}</p>}
      {identity.status === 'Active' && <div className="flex flex-wrap gap-2"><Button variant="outline" size="sm" disabled={busy} onClick={() => { setEditor(identity); setDisable(null); }}>Review changes</Button>
        <Button variant="outline" size="sm" disabled={busy} onClick={() => { setBusy(true); void automationIdentityService.usage(identity.id).then(usage => { setDisable({ identity, usage }); setEditor(null); }).catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Review disable</Button></div>}
    </div>)}
    <Button variant="outline" disabled={busy || Boolean(error)} onClick={() => { setEditor('new'); setDisable(null); }}>Create automation identity</Button>
    {disable && <div className="space-y-3 rounded-md border border-destructive p-3 text-sm"><p>Disable {disable.identity.name}? {disable.usage.activeRuns} active runs will be blocked at their next authorization check. Pending human approvals cannot bypass this restriction.</p><GrantSummary grants={disable.identity.grants} /><div className="flex gap-2"><Button variant="destructive" disabled={busy} onClick={() => { setBusy(true); void automationIdentityService.disable(disable.identity).then(async () => { setDisable(null); await refresh(); }).catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Confirm disable</Button><Button variant="outline" onClick={() => setDisable(null)}>Keep active</Button></div></div>}
    {editor && <AutomationIdentityEditor key={editor === 'new' ? 'new' : editor.id} identity={editor === 'new' ? null : editor} onClose={() => setEditor(null)} onSaved={async () => { setEditor(null); await refresh(); }} />}
  </CardContent></Card>;
}

function GrantSummary({ grants }: { grants: AutomationGrant[] }) {
  return <ul className="space-y-2 text-xs">{grants.map(grant => <li key={grant.workflowVersionId} className="break-all">Version {grant.workflowVersionId}: {grant.allSubjects ? 'all tenant invoices' : `selected invoice IDs: ${grant.subjectIds.join(', ')}`}; {grant.actions.map(action => `${action.key} v${action.version}`).join(', ') || 'routing only'}</li>)}</ul>;
}

function AutomationIdentityEditor({ identity, onClose, onSaved }: { identity: AutomationIdentity | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const [name, setName] = useState(identity?.name ?? ''); const [accountable, setAccountable] = useState(identity?.accountableUserId ?? '');
  const [expiry, setExpiry] = useState(identity?.expiresAt ?? ''); const [grants, setGrants] = useState<AutomationGrant[]>(identity?.grants ?? []);
  const [workflows, setWorkflows] = useState<WorkflowSummaryDto[]>([]); const [versions, setVersions] = useState<WorkflowVersionDto[]>([]);
  const [capability, setCapability] = useState<AutomationCapability | null>(null); const [actions, setActions] = useState<AutomationGrant['actions']>([]);
  const [allSubjects, setAllSubjects] = useState(false); const [subjects, setSubjects] = useState('');
  const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false);
  const [review, setReview] = useState<{ command: AutomationIdentityCommand; usage: AutomationUsage } | null>(null);
  useEffect(() => { void workflowService.list().then(items => setWorkflows(items.filter(item => item.state === 'Active'))).catch(reason => setError(connectorError(reason))); }, []);
  return <div className="space-y-4 rounded-md border p-4">
    <h3 className="font-medium">{identity ? 'Update' : 'Create'} automation identity</h3>
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {!review && <>
      <label className="block space-y-1 text-sm">Name<Input value={name} maxLength={160} onChange={event => setName(event.target.value)} /></label>
      <label className="block space-y-1 text-sm">Accountable tenant administrator ID<Input value={accountable} onChange={event => setAccountable(event.target.value)} /></label>
      <label className="block space-y-1 text-sm">Expiry (UTC, optional)<Input placeholder="2026-12-31T23:59:00Z" value={expiry} onChange={event => setExpiry(event.target.value)} /></label>
      <GrantSummary grants={grants} />
      {grants.map(grant => <Button key={grant.workflowVersionId} size="sm" variant="outline" onClick={() => setGrants(items => items.filter(item => item.workflowVersionId !== grant.workflowVersionId))}>Remove grant {grant.workflowVersionId}</Button>)}
      <label className="block space-y-1 text-sm">Add a published workflow<NativeSelect defaultValue="" onChange={event => { setCapability(null); setVersions([]); setActions([]); void workflowService.listVersions(event.target.value).then(items => setVersions(items.filter(item => item.publishedAt))).catch(reason => setError(connectorError(reason))); }}><option value="" disabled>Choose workflow</option>{workflows.map(workflow => <option key={workflow.id} value={workflow.id}>{workflow.name}</option>)}</NativeSelect></label>
      {versions.length > 0 && <label className="block space-y-1 text-sm">Immutable version<NativeSelect value={capability?.workflowVersionId ?? ''} onChange={event => { setCapability(null); setActions([]); void automationIdentityService.capabilities(event.target.value).then(setCapability).catch(reason => setError(connectorError(reason))); }}><option value="" disabled>Choose version</option>{versions.map(version => <option key={version.id} value={version.id}>{version.tag} · {version.id}</option>)}</NativeSelect></label>}
      {capability && <div className="space-y-3 rounded-md border p-3"><p className="text-sm">Explicit permitted actions</p>{capability.actions.map(action => <label key={`${action.key}@${action.version}`} className="flex items-center gap-2 text-sm"><Checkbox checked={actions.some(item => item.key === action.key && item.version === action.version)} onCheckedChange={checked => setActions(items => checked ? [...items, { key: action.key, version: action.version }] : items.filter(item => item.key !== action.key || item.version !== action.version))} />{action.key} · v{action.version} · {action.requiredPermission}</label>)}
        <label className="flex items-center gap-2 text-sm"><Checkbox checked={allSubjects} onCheckedChange={checked => setAllSubjects(checked === true)} />Allow all tenant invoices for this version</label>
        {!allSubjects && <label className="block space-y-1 text-sm">Selected invoice IDs, separated by commas<Textarea value={subjects} onChange={event => setSubjects(event.target.value)} /></label>}
        <Button variant="outline" disabled={grants.length >= 20 || (!allSubjects && !subjects.trim())} onClick={() => setGrants(items => [...items.filter(item => item.workflowVersionId !== capability.workflowVersionId), { workflowVersionId: capability.workflowVersionId, subjectType: 'Invoice', allSubjects, subjectIds: allSubjects ? [] : subjects.split(/[\s,]+/).filter(Boolean), actions }])}>Add or replace this version grant</Button>
      </div>}
      <Button disabled={busy || !name.trim() || !accountable.trim()} onClick={() => { setBusy(true); setError(null); void (async () => {
        const command = { name, accountableUserId: accountable, expiresAt: expiry ? new Date(expiry).toISOString() : null, grants, expectedVersion: identity?.expectedVersion };
        const usage = identity ? await automationIdentityService.usage(identity.id) : { activeRuns: 0, workflowVersionIds: [] };
        setReview({ command, usage });
      })().catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Review exact grants</Button>
    </>}
    {review && <div className="space-y-3 text-sm"><p>Save <strong>{review.command.name}</strong> with accountable administrator {review.command.accountableUserId} and expiry {review.command.expiresAt ?? 'none'}?</p>
      {identity && <><p>Current access</p><GrantSummary grants={identity.grants} /></>}<p>Access after this change</p><GrantSummary grants={review.command.grants} /><p>{review.usage.activeRuns} active runs use this identity. Removed grants block their next effect. Connection access still requires separate account and workflow grants.</p>
      <div className="flex gap-2"><Button disabled={busy} onClick={() => { setBusy(true); void automationIdentityService.save(identity?.id ?? null, review.command).then(onSaved).catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Confirm identity and grants</Button><Button variant="outline" disabled={busy} onClick={() => setReview(null)}>Edit changes</Button></div>
    </div>}
    <Button variant="outline" disabled={busy} onClick={onClose}>Close</Button>
  </div>;
}
