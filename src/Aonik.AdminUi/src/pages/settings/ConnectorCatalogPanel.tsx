import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Textarea } from '@/components/ui/textarea';
import { api } from '@/lib/api';
import { useModules } from '@/modules/useModules';
import { connectorCatalogService, type ConnectorInstallation, type ConnectorPackage, type ConnectorPackageDetail, type ConnectorPackageImpact } from '@/services/connectorCatalogService';
import { connectorError, type ConnectorActionRef } from '@/services/connectorService';

const releaseKey = (release: { packageKey: string; packageVersion: string }) => [release.packageKey, release.packageVersion].join('@');
const actionKey = (action: ConnectorActionRef) => [action.key, action.version].join('@');
const settingPath = (key: string) => '/tenant/settings/values/Integrations.Curated.' + key;

export function ConnectorCatalogPanel({ onAccountConnected, availableActions }: { onAccountConnected: () => Promise<void>; availableActions: ConnectorActionRef[] }) {
  const { allowsPolicy, hasPermission } = useModules();
  const canManage = hasPermission('Integration.Manage') && hasPermission('Workflow.Write');
  const canReview = canManage && allowsPolicy('PlatformAdmin');
  const canCheckImpact = hasPermission('Integration.Manage') && hasPermission('Workflow.Read');
  const [releases, setReleases] = useState<ConnectorPackage[]>([]);
  const [installations, setInstallations] = useState<ConnectorInstallation[]>([]);
  const [selected, setSelected] = useState('');
  const [detail, setDetail] = useState<ConnectorPackageDetail | null>(null);
  const [secret, setSecret] = useState('');
  const [username, setUsername] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    const [nextReleases, nextInstallations] = await Promise.all([connectorCatalogService.releases(), connectorCatalogService.installations()]);
    setReleases(nextReleases);
    setInstallations(nextInstallations);
  }, []);

  useEffect(() => { void refresh().catch(reason => setError(connectorError(reason))); }, [refresh]);
  const act = async (operation: () => Promise<void>) => {
    setBusy(true);
    setError(null);
    setNotice(null);
    try { await operation(); }
    catch (reason) { setError(connectorError(reason)); }
    finally { setBusy(false); }
  };
  const selectedRelease = releases.find(release => releaseKey(release) === selected);
  const inspect = async (release: ConnectorPackage) => {
    setSelected(releaseKey(release));
    setDetail(null);
    setSecret('');
    setUsername('');
    setDetail(await connectorCatalogService.inspect(release));
  };
  const importRelease = async (release: ConnectorPackage) => {
    try { await connectorCatalogService.importRelease(release, secret || undefined, username || undefined); }
    finally { setSecret(''); setUsername(''); }
    await refresh();
    setNotice('Release imported. Submit each action or server for platform review.');
  };
  const download = async (release: ConnectorPackage) => {
    const data = await connectorCatalogService.download(release);
    const url = URL.createObjectURL(data);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = release.packageKey + '-' + release.packageVersion + '.json';
    anchor.click();
    window.setTimeout(() => URL.revokeObjectURL(url), 500);
  };

  return <Card><CardHeader><CardTitle>Connector catalog</CardTitle></CardHeader><CardContent className="space-y-4 text-sm">
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {notice && <p role="status">{notice}</p>}
    {releases.length === 0 && <p className="text-muted-foreground">No signed releases are available for this deployment.</p>}
    {releases.length > 0 && <div className="flex flex-wrap items-end gap-2">
      <label className="min-w-52 flex-1 space-y-1">Release<NativeSelect value={selected} disabled={busy} onChange={event => {
        setSelected(event.target.value); setDetail(null); setSecret(''); setUsername('');
      }}><option value="">Select a release</option>{releases.map(release => <option key={releaseKey(release)} value={releaseKey(release)}>
          {release.displayName} {release.packageVersion} · {release.adapterKind}
        </option>)}</NativeSelect></label>
      <Button variant="outline" disabled={busy || !selectedRelease} onClick={() => selectedRelease && void act(() => inspect(selectedRelease))}>Inspect release</Button>
      <Button variant="outline" disabled={busy || !selectedRelease} onClick={() => selectedRelease && void act(() => download(selectedRelease))}>Download signed artifact</Button>
    </div>}
    {detail && <div className="space-y-3 rounded-md border p-3">
      <p>{detail.package.displayName} {detail.package.packageVersion} · {detail.package.publisherId} · {detail.manifest.authentication.kind}</p>
      <p className="break-all font-mono text-xs text-muted-foreground">{detail.package.digest}</p>
      <p className="text-xs text-muted-foreground">Up to {detail.manifest.limits.maximumInputBytes} bytes in, {detail.manifest.limits.maximumOutputBytes} bytes out and {detail.manifest.limits.timeoutSeconds} seconds per action.</p>
      {detail.manifest.actions.map(action => <details key={actionKey(action)} className="rounded-md border p-2">
        <summary className="cursor-pointer">{action.displayName} · {actionKey(action)} · {detail.package.adapterKind === 'Native' || action.minimumRiskTier === 'ReadOnly' ? action.minimumRiskTier : 'High'}</summary>
        <p className="mt-2 break-all">{action.httpMethod ?? action.remoteToolName} {action.endpoint}</p>
        {action.requiredScopes.length > 0 && <p className="break-all text-xs">{action.requiredScopes.join(', ')}</p>}
        {action.scopeAlternatives?.map(scopes => <p className="break-all text-xs" key={scopes.join(' ')}>{scopes.join(', ')}</p>)}
        <pre className="mt-2 overflow-auto whitespace-pre-wrap break-all text-xs">{JSON.stringify({ input: action.inputSchema, output: action.outputSchema }, null, 2)}</pre>
      </details>)}
      {detail.package.adapterKind === 'Native' ? <p className="text-muted-foreground">This adapter is installed with AONIK. Use the connection request below to authorize an account.</p>
        : canManage && <div className="space-y-2">
          {detail.manifest.authentication.kind !== 'None' && <CredentialFields kind={detail.manifest.authentication.kind} secret={secret} username={username} onSecret={setSecret} onUsername={setUsername} />}
          <Button disabled={busy || (detail.manifest.authentication.kind === 'Basic' && Boolean(secret) && !username.trim())}
            onClick={() => void act(() => importRelease(detail.package))}>Import selected release</Button>
        </div>}
    </div>}
    {installations.length > 0 && <div className="space-y-3"><h3 className="font-medium">Tenant installations</h3>
      {installations.map(installation => <InstallationCard key={installation.id + ':' + installation.expectedVersion + ':' + installation.credentialVersion}
        installation={installation} releases={releases} busy={busy} canManage={canManage} canReview={canReview} canCheckImpact={canCheckImpact}
        onAction={operation => void act(async () => { await operation(); await refresh(); })}
        onConnect={async (row, release, label) => { await connectorCatalogService.connect(row, release, label); await onAccountConnected(); setNotice('Service account connected. Use Accounts to grant its actions.'); }}
        onInspect={release => void act(() => inspect(release))} />)}
    </div>}
    {availableActions.length > 0 && <CuratedPolicy availableActions={availableActions} />}
  </CardContent></Card>;
}

function InstallationCard({ installation, releases, busy, canManage, canReview, canCheckImpact, onAction, onConnect, onInspect }: {
  installation: ConnectorInstallation; releases: ConnectorPackage[]; busy: boolean; canManage: boolean; canReview: boolean; canCheckImpact: boolean;
  onAction: (operation: () => Promise<void>) => void;
  onConnect: (row: ConnectorInstallation, release: ConnectorPackage, label: string) => Promise<void>;
  onInspect: (release: ConnectorPackage) => void;
}) {
  const release = releases.find(candidate => releaseKey(candidate) === releaseKey(installation) && candidate.digest === installation.digest);
  const otherVersions = releases.filter(candidate => candidate.packageKey === installation.packageKey && candidate.packageVersion !== installation.packageVersion);
  const [targetVersion, setTargetVersion] = useState('');
  const [impact, setImpact] = useState<ConnectorPackageImpact | null>(null);
  const [notes, setNotes] = useState('');
  const [rotateKind, setRotateKind] = useState<string | null>(null);
  const [secret, setSecret] = useState('');
  const [username, setUsername] = useState('');
  const [label, setLabel] = useState(release?.displayName ?? '');
  const [disable, setDisable] = useState(false);
  const command = (name: 'submit' | 'activate' | 'disable') => onAction(async () => { await connectorCatalogService.command(installation, name); setDisable(false); });
  return <section className="space-y-3 rounded-md border p-3" aria-label={releaseKey(installation)}>
    <div className="flex flex-wrap items-center gap-2">
      <h4 className="font-medium">{releaseKey(installation)}{installation.action ? ' · ' + actionKey(installation.action) : ''}</h4>
      <Badge variant="outline">{installation.approvalState}</Badge><Badge variant="secondary">{installation.isActive ? 'Active' : 'Inactive'}</Badge>
    </div>
    {!release && <p className="text-muted-foreground">This signed release is unavailable on the current deployment.</p>}
    {canManage && <div className="flex flex-wrap gap-2">
      {release && ['Draft', 'Rejected'].includes(installation.approvalState) && <Button size="sm" disabled={busy} onClick={() => command('submit')}>Submit for review</Button>}
      {release && installation.approvalState === 'Approved' && !installation.isActive && <Button size="sm" disabled={busy} onClick={() => command('activate')}>Activate installation</Button>}
      {installation.isActive && <Button size="sm" variant="outline" disabled={busy} onClick={() => setDisable(true)}>Disable installation</Button>}
      {release && !rotateKind && <Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(async () => {
        const selected = await connectorCatalogService.inspect(release);
        setRotateKind(selected.manifest.authentication.kind);
      })}>Change service credential</Button>}
    </div>}
    {canReview && release && installation.approvalState === 'PendingPlatformReview' && <div className="space-y-2">
      <label className="block space-y-1">Review notes<Textarea value={notes} maxLength={2000} onChange={event => setNotes(event.target.value)} /></label>
      <div className="flex flex-wrap gap-2">
        <Button size="sm" variant="outline" disabled={busy} onClick={() => onInspect(release)}>Inspect this release</Button>
        <Button size="sm" disabled={busy} onClick={() => onAction(async () => { await connectorCatalogService.review(installation, true, notes); })}>Approve installation</Button>
        <Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(async () => { await connectorCatalogService.review(installation, false, notes); })}>Reject installation</Button>
      </div>
    </div>}
    {rotateKind && <div className="space-y-2">
      {rotateKind === 'None' ? <p>This release requires no credential.</p> : <>
        <CredentialFields kind={rotateKind} secret={secret} username={username} onSecret={setSecret} onUsername={setUsername} />
        <p className="text-muted-foreground">A new credential pauses this installation until platform review and activation.</p>
        <Button size="sm" disabled={busy || !secret || (rotateKind === 'Basic' && !username.trim())} onClick={() => onAction(async () => {
          try { await connectorCatalogService.rotate(installation, secret, username || undefined); setRotateKind(null); }
          finally { setSecret(''); setUsername(''); }
        })}>Save credential and pause</Button>
      </>}
      <Button size="sm" variant="outline" onClick={() => { setRotateKind(null); setSecret(''); setUsername(''); }}>Cancel</Button>
    </div>}
    {canManage && release && installation.isActive && <div className="flex flex-wrap items-end gap-2">
      <label className="space-y-1">Service account label<Input value={label} maxLength={160} onChange={event => setLabel(event.target.value)} /></label>
      <Button size="sm" variant="outline" disabled={busy || !label.trim()} onClick={() => onAction(() => onConnect(installation, release, label))}>Connect service account</Button>
    </div>}
    {canCheckImpact && <div className="space-y-2">
      <div className="flex flex-wrap items-end gap-2"><label className="space-y-1">Compare release<NativeSelect value={targetVersion} onChange={event => { setTargetVersion(event.target.value); setImpact(null); }}>
        <option value="">Current installation only</option>{otherVersions.map(candidate => <option key={candidate.packageVersion} value={candidate.packageVersion}>{candidate.packageVersion}</option>)}
      </NativeSelect></label>
        <Button size="sm" variant="outline" disabled={busy} onClick={() => onAction(async () => {
          setImpact(await connectorCatalogService.impact(installation, targetVersion || undefined));
        })}>Review workflow impact</Button>
      </div>
      {impact && <div className="space-y-2 rounded-md bg-muted p-3">
        <p>{impact.workflows.length} directly bound versions and {impact.runs.length} active runs{impact.truncated ? ' in this bounded preview' : ''}.</p>
        {impact.workflows.map(workflow => <p key={workflow.workflowVersionId}>{workflow.slug} · {workflow.version}{workflow.current ? ' · Current' : ''}</p>)}
        {impact.runs.map(run => <p key={run.runId}><Link className="text-primary underline" to={'/ai/workflow-runs/' + run.runId}>{run.runId}</Link> · {run.status}</p>)}
        {impact.target && <><div className="grid gap-3 sm:grid-cols-2">
          {([['Installed actions', impact.currentActions], ['Target actions', impact.targetActions]] as const).map(([title, entries]) => <div key={title}>
            <p className="font-medium">{title}</p>
            {entries.map(entry => <p key={actionKey(entry.action)}>{actionKey(entry.action)} · {entry.tier}{entry.scopes.length > 0 ? ' · ' + entry.scopes.join(', ') : ''}</p>)}
          </div>)}
        </div><p>Import the target release, review it, connect an account and publish a new workflow version to adopt it.</p>
          <Button size="sm" variant="outline" disabled={busy} onClick={() => { if (impact.target) onInspect(impact.target); }}>Inspect target release</Button></>}
      </div>}
    </div>}
    {disable && <div className="space-y-2 rounded-md border border-destructive p-3">
      <p>Disable this installation? Workflows pinned to it will pause at their next use.</p>
      <div className="flex flex-wrap gap-2"><Button size="sm" variant="destructive" disabled={busy} onClick={() => command('disable')}>Confirm disable</Button>
        <Button size="sm" variant="outline" onClick={() => setDisable(false)}>Cancel</Button></div>
    </div>}
  </section>;
}

function CredentialFields({ kind, secret, username, onSecret, onUsername }: {
  kind: string; secret: string; username: string; onSecret: (value: string) => void; onUsername: (value: string) => void;
}) {
  return <><label className="block space-y-1">Service credential<Input type="password" autoComplete="new-password" value={secret} maxLength={16384} onChange={event => onSecret(event.target.value)} /></label>
    {kind === 'Basic' && <label className="block space-y-1">Credential username<Input autoComplete="off" value={username} maxLength={500} onChange={event => onUsername(event.target.value)} /></label>}</>;
}

function CuratedPolicy({ availableActions }: { availableActions: ConnectorActionRef[] }) {
  const [enabled, setEnabled] = useState(false);
  const [workflow, setWorkflow] = useState(false);
  const [selected, setSelected] = useState<ConnectorActionRef[] | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  useEffect(() => {
    void Promise.all(['Enabled', 'AllowWorkflowConnections', 'AllowedActions'].map(key => api.get<{ value: string | null }>(settingPath(key))))
      .then(([tenant, runs, actions]) => {
        setEnabled(tenant.value === 'true'); setWorkflow(runs.value === 'true');
        setSelected(actions.value ? JSON.parse(actions.value) as ConnectorActionRef[] : null);
        setLoaded(true);
      }).catch(reason => setError(connectorError(reason)));
  }, []);
  const allowed = selected ?? availableActions;
  const save = async () => {
    setBusy(true); setError(null); setSaved(false);
    try {
      if (!enabled) await api.put(settingPath('Enabled'), { value: 'false' });
      await api.put(settingPath('AllowedActions'), { value: JSON.stringify(allowed.map(({ key, version }) => ({ key, version }))) });
      await api.put(settingPath('AllowWorkflowConnections'), { value: String(workflow) });
      if (enabled) await api.put(settingPath('Enabled'), { value: 'true' });
      setSaved(true);
    } catch (reason) { setError(connectorError(reason)); }
    finally { setBusy(false); }
  };
  return <details className="space-y-3 rounded-md border p-3"><summary className="cursor-pointer font-medium">Curated connector tenant policy</summary>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {saved && <p role="status">Tenant policy saved.</p>}
    <label className="flex items-center gap-2"><Checkbox disabled={!loaded || busy} checked={enabled} onCheckedChange={value => { setEnabled(value === true); setSaved(false); }} />Enable curated connectors</label>
    <label className="flex items-center gap-2"><Checkbox disabled={!loaded || busy} checked={workflow} onCheckedChange={value => { setWorkflow(value === true); setSaved(false); }} />Allow explicit curated workflow grants</label>
    {availableActions.map(action => {
      const checked = allowed.some(candidate => actionKey(candidate) === actionKey(action));
      return <label className="flex items-center gap-2" key={actionKey(action)}><Checkbox disabled={!loaded || busy} checked={checked} onCheckedChange={value => {
        setSelected(value ? [...allowed.filter(item => actionKey(item) !== actionKey(action)), action] : allowed.filter(item => actionKey(item) !== actionKey(action)));
        setSaved(false);
      }} />{actionKey(action)}</label>;
    })}
    <Button size="sm" disabled={!loaded || busy} onClick={() => void save()}>Save curated policy</Button>
  </details>;
}
