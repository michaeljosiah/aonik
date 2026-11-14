import { useCallback, useEffect, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { NativeSelect } from '@/components/ui/native-select';
import { Checkbox } from '@/components/ui/checkbox';
import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';
import { connectorService, connectorError, consentUrl, type ConnectionRequest, type ConnectorActionRef, type ConnectorConnection, type ConnectorDescriptor, type ConnectorGrant } from '@/services/connectorService';

export function MyConnectionsPage() { return <ConnectionsPage />; }
export function TenantIntegrationsPage() { return <ConnectionsPage tenant />; }

function ConnectionsPage({ tenant = false }: { tenant?: boolean }) {
  const [search] = useSearchParams();
  const requestId = search.get('requestId');
  const [catalog, setCatalog] = useState<ConnectorDescriptor[]>([]);
  const [accounts, setAccounts] = useState<ConnectorConnection[]>([]);
  const [requests, setRequests] = useState<ConnectionRequest[]>([]);
  const [managedIds, setManagedIds] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [purpose, setPurpose] = useState('Read invoice correspondence');
  const [actions, setActions] = useState<ConnectorActionRef[]>([]);
  const refresh = useCallback(async () => {
    const [descriptors, managed, eligible, items] = await Promise.all([connectorService.catalog(), connectorService.managed(), connectorService.connections('gmail'), connectorService.requests()]);
    if (requestId && !items.some(item => item.id === requestId)) items.unshift(await connectorService.request(requestId));
    setCatalog(descriptors); setManagedIds(managed.map(account => account.id));
    setAccounts([...new Map([...eligible, ...managed].map(account => [account.id, account])).values()]); setRequests(items);
  }, [requestId]);
  useEffect(() => { void refresh().catch(reason => setError(connectorError(reason))); }, [refresh]);
  const run = async (operation: () => Promise<unknown>) => {
    setBusy(true); setError(null);
    try { await operation(); await refresh(); } catch (reason) { setError(connectorError(reason)); } finally { setBusy(false); }
  };
  const gmail = catalog.find(item => item.key === 'gmail');
  const visibleAccounts = accounts.filter(account => !tenant || account.ownership === 'Tenant');
  return <div className="h-full space-y-6 overflow-auto p-6">
    <div className="flex items-start justify-between gap-4"><div><h1 className="text-2xl font-semibold">{tenant ? 'Tenant integrations' : 'My connections'}</h1>
      <p className="mt-2 max-w-3xl text-sm text-muted-foreground">{tenant ? 'Review organizational access, account consent and exact use grants. Managing an account does not grant access to its email.' : 'Choose the accounts Aonik can use, review connection requests and manage your grants.'}</p></div>
      <Button variant="outline" disabled={busy} onClick={() => void run(refresh)}>Refresh</Button></div>
    {error && <p role="alert" className="rounded-md border border-destructive p-3 text-sm text-destructive">{error}</p>}
    {tenant && <IntegrationPolicyPanel disabled={busy} onSave={run} />}
    <Card><CardHeader><CardTitle>Request Gmail access</CardTitle></CardHeader><CardContent className="space-y-4">
      <p className="text-sm text-muted-foreground">{tenant ? 'An integration reviewer chooses an organizational account or completes its consent.' : 'Your account stays private unless you explicitly grant a user, role or workflow access.'} Select the actions and review Google consent before access is applied.</p>
      <ActionChoices actions={gmail?.actions ?? []} selected={actions} onChange={setActions} />
      <label className="block space-y-2 text-sm">Purpose<Textarea value={purpose} maxLength={1000} onChange={event => setPurpose(event.target.value)} /></label>
      <Button disabled={busy || !actions.length || !purpose.trim()} onClick={() => void run(async () => {
        const created = await connectorService.createRequest({ connectorKey: 'gmail', ownership: tenant ? 'Tenant' : 'User', actions, purpose, idempotencyKey: crypto.randomUUID() });
        setRequests(items => [created, ...items.filter(item => item.id !== created.id)]);
      })}>Submit access request</Button>
    </CardContent></Card>
    <section className="space-y-3" aria-label="Connection requests"><h2 className="text-lg font-semibold">Requests and reviews</h2>
      {requests.length === 0 && <p className="text-sm text-muted-foreground">No requests are available.</p>}
      {requests.map(request => <ConnectionRequestCard key={request.id} request={request} accounts={accounts} disabled={busy} onAction={run} highlighted={request.id === requestId} />)}
    </section>
    <section className="space-y-3" aria-label="Connected accounts"><h2 className="text-lg font-semibold">Accounts</h2>
      {visibleAccounts.length === 0 && <p className="text-sm text-muted-foreground">No eligible accounts have been connected.</p>}
      {visibleAccounts.map(account => <ConnectionAccountCard key={`${account.id}:${account.expectedVersion}`} account={account} canManage={managedIds.includes(account.id)}
        catalog={catalog} tenantDefault={tenant} disabled={busy} onAction={run} />)}
    </section>
  </div>;
}

type RunAction = (operation: () => Promise<unknown>) => Promise<void>;
function ConnectionRequestCard({ request, accounts, disabled, onAction, highlighted }: {
  request: ConnectionRequest; accounts: ConnectorConnection[]; disabled: boolean; onAction: RunAction; highlighted: boolean;
}) {
  const [selectedAccount, setSelectedAccount] = useState('');
  return <Card className={highlighted ? 'border-primary' : undefined}><CardHeader><CardTitle className="flex flex-wrap items-center gap-2 text-base">{request.connectorKey} · {request.ownership === 'User' ? 'Personal account' : 'Organization account'}<Badge variant="outline">{request.status}</Badge></CardTitle></CardHeader>
    <CardContent className="space-y-3 text-sm"><p>{request.purpose}</p><p className="text-muted-foreground">{request.nextAction}</p>
      {request.desiredCapability && <p>{request.desiredCapability}</p>}
      <div className="flex flex-wrap gap-2">{request.actions.map(action => <Badge key={`${action.key}:${action.version}`} variant="secondary">{action.key} · v{action.version}</Badge>)}</div>
      <details><summary className="cursor-pointer text-muted-foreground">Requested scopes and references</summary><div className="mt-2 space-y-1 break-all text-xs">
        {request.requiredScopes.map(scope => <p key={scope}>{scope}</p>)}<p>Request: {request.id}</p><p>Expires: {new Date(request.expiresAt).toLocaleString()}</p>
        {request.connectionId && <p>Account: {request.connectionId}</p>}{request.workflowId && <p>Workflow: {request.workflowId}</p>}{request.runId && <p>Run: {request.runId}</p>}
      </div></details>
      {request.canReview && !request.connectionId && request.ownership === 'Tenant' && <label className="block space-y-2">Use an existing organizational account<NativeSelect value={selectedAccount} onChange={event => setSelectedAccount(event.target.value)}>
        <option value="">Request new account consent</option>{accounts.filter(account => account.ownership === 'Tenant' && account.connectorKey === request.connectorKey && account.status === 'Active').map(account => <option key={account.id} value={account.id}>{account.displayName}</option>)}
      </NativeSelect></label>}
      {request.canConfirm && <p className="rounded-md border p-3">Google returned <strong>{request.consentedAccountLabel}</strong>. Confirm this exact account for the actions above.</p>}
      <div className="flex flex-wrap gap-2">
        {request.canReview && <><Button disabled={disabled} onClick={() => void onAction(() => connectorService.decideRequest(request, 'Approve', selectedAccount || undefined))}>Approve requested use</Button><Button variant="outline" disabled={disabled} onClick={() => void onAction(() => connectorService.decideRequest(request, 'Reject'))}>Reject</Button></>}
        {request.canConnect && !request.canConfirm && <Button disabled={disabled} onClick={() => void onAction(async () => { const challenge = await connectorService.beginConsent(request); window.location.assign(consentUrl(challenge.authorizationUrl)); })}>Continue to Google consent</Button>}
        {request.canConfirm && <Button disabled={disabled} onClick={() => void onAction(() => connectorService.confirmAccount(request))}>Confirm account and requested access</Button>}
        {request.canConnect && request.ownership === 'User' && <Button disabled={disabled} variant="outline" onClick={() => void onAction(() => connectorService.decideRequest(request, 'Reject'))}>Decline requested access</Button>}
        {request.canCancel && <Button variant="outline" disabled={disabled} onClick={() => void onAction(() => connectorService.cancelRequest(request))}>Cancel request</Button>}
        {request.runId && <Link to={`/ai/workflow-runs/${request.runId}`} className="self-center text-primary underline">Review paused run</Link>}
      </div>
    </CardContent></Card>;
}

function ConnectionAccountCard({ account, canManage, catalog, tenantDefault, disabled, onAction }: {
  account: ConnectorConnection; canManage: boolean; catalog: ConnectorDescriptor[]; tenantDefault: boolean; disabled: boolean; onAction: RunAction;
}) {
  const [name, setName] = useState(account.displayName);
  const [showGrants, setShowGrants] = useState(false);
  const [disconnect, setDisconnect] = useState(false);
  return <Card><CardHeader><CardTitle className="flex flex-wrap items-center gap-2 text-base">{account.displayName}<Badge variant="outline">{account.status}</Badge><Badge variant="secondary">{account.ownership}</Badge></CardTitle></CardHeader><CardContent className="space-y-3">
    <p className="break-all text-xs text-muted-foreground">{account.id}</p>
    <div className="flex flex-wrap gap-2">
      {account.status === 'Active' && <Button variant="outline" disabled={disabled} onClick={() => void onAction(() => connectorService.setDefault(account.id, tenantDefault))}>Set {tenantDefault ? 'tenant' : 'my'} default</Button>}
      {canManage && <><Button variant="outline" disabled={disabled} onClick={() => setShowGrants(value => !value)}>Manage use grants</Button><Button variant="outline" disabled={disabled} onClick={() => setDisconnect(true)}>Disconnect</Button></>}
    </div>
    {canManage && <div className="flex gap-2"><Input aria-label="Account label" value={name} maxLength={160} onChange={event => setName(event.target.value)} /><Button variant="outline" disabled={disabled || !name.trim() || name === account.displayName} onClick={() => void onAction(() => connectorService.rename(account, name))}>Save label</Button></div>}
    {disconnect && <div className="space-y-2 rounded-md border border-destructive p-3 text-sm"><p>Disconnect {account.displayName}? All AONIK grants and local credentials for this account will be revoked. Workflows using it will pause.</p><div className="flex gap-2"><Button variant="destructive" disabled={disabled} onClick={() => void onAction(() => connectorService.disconnect(account))}>Confirm disconnect</Button><Button variant="outline" onClick={() => setDisconnect(false)}>Keep connected</Button></div></div>}
    {showGrants && <ConnectionGrantPanel account={account} descriptor={catalog.find(item => item.key === account.connectorKey)} disabled={disabled} onAction={onAction} />}
  </CardContent></Card>;
}

function ConnectionGrantPanel({ account, descriptor, disabled, onAction }: { account: ConnectorConnection; descriptor?: ConnectorDescriptor; disabled: boolean; onAction: RunAction }) {
  const [grants, setGrants] = useState<ConnectorGrant[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [type, setType] = useState('User'); const [id, setId] = useState(''); const [actions, setActions] = useState<ConnectorActionRef[]>([]);
  const [edited, setEdited] = useState<ConnectorGrant | undefined>();
  useEffect(() => { void connectorService.grants(account.id).then(setGrants).catch(reason => setError(connectorError(reason))); }, [account.id]);
  return <div className="space-y-3 border-t pt-3"><h3 className="font-medium">Explicit use grants</h3>{error && <p role="alert" className="text-sm text-destructive">{error}</p>}
    {grants.map(grant => <div key={grant.id} className="flex flex-wrap items-center justify-between gap-2 rounded-md border p-3 text-xs"><div><p>{grant.granteeType} · {grant.granteeId}</p><p className="text-muted-foreground">{grant.actions.map(action => `${action.key}@${action.version}`).join(', ')}{grant.revokedAt && ' · Revoked'}</p></div>
      <div className="flex gap-2"><Button variant="outline" size="sm" disabled={disabled} onClick={() => { setEdited(grant); setType(grant.granteeType); setId(grant.granteeId); setActions(grant.actions); }}>{grant.revokedAt ? 'Restore' : 'Edit'}</Button>{!grant.revokedAt && <Button variant="outline" size="sm" disabled={disabled} onClick={() => void onAction(async () => { await connectorService.revokeGrant(grant); setGrants(await connectorService.grants(account.id)); })}>Revoke</Button>}</div></div>)}
    <label className="block space-y-1 text-sm">Grantee type<NativeSelect disabled={Boolean(edited)} value={type} onChange={event => setType(event.target.value)}><option>User</option><option>Role</option><option>Workflow</option></NativeSelect></label>
    <label className="block space-y-1 text-sm">Tenant {type.toLowerCase()} ID<Input disabled={Boolean(edited)} value={id} onChange={event => setId(event.target.value)} /></label>
    <ActionChoices actions={descriptor?.actions ?? []} selected={actions} onChange={setActions} />
    <div className="flex gap-2"><Button disabled={disabled || !/^[0-9a-f-]{36}$/i.test(id) || !actions.length || account.status !== 'Active'} onClick={() => void onAction(() => connectorService.issueGrant(account, type, id, actions, undefined, edited))}>{edited ? 'Apply these actions to this grant' : 'Grant these actions'}</Button>{edited && <Button variant="outline" onClick={() => { setEdited(undefined); setActions([]); setId(''); }}>Cancel edit</Button>}</div>
  </div>;
}

export function ActionChoices({ actions, selected, onChange }: { actions: ConnectorActionRef[]; selected: ConnectorActionRef[]; onChange: (value: ConnectorActionRef[]) => void }) {
  return <div className="space-y-2">{actions.map(action => { const checked = selected.some(item => item.key === action.key && item.version === action.version); return <label key={`${action.key}:${action.version}`} className="flex items-center gap-2 text-sm"><Checkbox checked={checked} onCheckedChange={value => onChange(value ? [...selected, { key: action.key, version: action.version }] : selected.filter(item => item.key !== action.key || item.version !== action.version))} />{action.key} · v{action.version}</label>; })}</div>;
}

function IntegrationPolicyPanel({ disabled, onSave }: { disabled: boolean; onSave: RunAction }) {
  const [enabled, setEnabled] = useState(false); const [personal, setPersonal] = useState(true); const [selfService, setSelfService] = useState(false); const [workflows, setWorkflows] = useState(false); const [role, setRole] = useState('');
  const [loaded, setLoaded] = useState(false);
  useEffect(() => {
    const entries = ['Enabled', 'AllowUserConnections', 'AllowSelfService', 'AllowWorkflowConnections'].map(key => `Integrations.Gmail.${key}`).concat('Integrations.RequestReviewerRoleId');
    void Promise.all(entries.map(key => api.get<{ value: string | null }>(`/tenant/settings/values/${key}`))).then(values => {
      setEnabled(values[0].value === 'true'); setPersonal(values[1].value !== 'false'); setSelfService(values[2].value === 'true'); setWorkflows(values[3].value === 'true'); setRole(values[4].value ?? ''); setLoaded(true);
    }).catch(() => setLoaded(false));
  }, []);
  return <Card><CardHeader><CardTitle>Tenant policy</CardTitle></CardHeader><CardContent className="space-y-3 text-sm"><p className="text-muted-foreground">Tenant enablement is explicit. Platform restrictions continue to apply. OAuth client credentials are configured by the platform operator.</p>
    {!loaded && <p className="text-muted-foreground">Policy values could not be loaded yet. Settings read/write permission is required.</p>}
    {[['Enable Gmail', enabled, setEnabled], ['Allow personal accounts', personal, setPersonal], ['Allow personal self-service consent', selfService, setSelfService], ['Allow explicit workflow grants', workflows, setWorkflows]].map(([label, checked, update]) => <label key={String(label)} className="flex items-center gap-2"><Checkbox disabled={disabled || !loaded} checked={Boolean(checked)} onCheckedChange={value => (update as (value: boolean) => void)(value === true)} />{String(label)}</label>)}
    <label className="block space-y-1">Integration reviewer role ID<Input disabled={!loaded} value={role} onChange={event => setRole(event.target.value)} /></label>
    <Button disabled={disabled || !loaded} onClick={() => void onSave(async () => {
      const values = { 'Integrations.Gmail.Enabled': String(enabled), 'Integrations.Gmail.AllowUserConnections': String(personal), 'Integrations.Gmail.AllowSelfService': String(selfService), 'Integrations.Gmail.AllowWorkflowConnections': String(workflows), 'Integrations.RequestReviewerRoleId': role || null };
      for (const [key, value] of Object.entries(values)) await api.put(`/tenant/settings/values/${key}`, { value });
    })}>Save tenant policy</Button>
  </CardContent></Card>;
}
