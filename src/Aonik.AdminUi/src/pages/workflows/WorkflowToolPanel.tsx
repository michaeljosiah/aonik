import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { NativeSelect } from '@/components/ui/native-select';
import { Checkbox } from '@/components/ui/checkbox';
import { workflowService, type WorkflowActionCapability } from '@/services/workflowService';
import { connectorService, connectorError, type ConnectorConnection } from '@/services/connectorService';
import { JsonField } from './DecisionTablePanel';
import type { WorkflowNodeParams } from './workflowTypes';

export function WorkflowToolPanel({ workflowId, parameters, onChange }: { workflowId: string; parameters: WorkflowNodeParams; onChange: (value: WorkflowNodeParams) => void }) {
  const [actions, setActions] = useState<WorkflowActionCapability[]>([]);
  const [connections, setConnections] = useState<ConnectorConnection[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [requestPath, setRequestPath] = useState<string | null>(null);
  const latest = useRef({ parameters, onChange });
  latest.current = { parameters, onChange };
  useEffect(() => { void workflowService.capabilities().then(result => setActions(result.actions ?? [])).catch(reason => setError(connectorError(reason))); }, []);
  const selected = actions.find(action => action.key === parameters.actionKey && action.contractVersion === parameters.contractVersion);
  const connector = selected?.key.startsWith('gmail.') ? 'gmail' : null;
  useEffect(() => { if (connector) void connectorService.connections(connector).then(setConnections).catch(reason => setError(connectorError(reason))); }, [connector]);
  return <div className="space-y-3">
    {error && <p role="alert" className="text-xs text-destructive">{error}</p>}
    <label className="block space-y-1 text-xs">Registered action<NativeSelect value={selected ? `${selected.key}@${selected.contractVersion}` : ''} onChange={event => {
      const action = actions.find(item => `${item.key}@${item.contractVersion}` === event.target.value);
      if (action) onChange({ actionKey: action.key, contractVersion: action.contractVersion, inputs: action.key === 'finance.invoices.issue' ? { invoiceId: { fact: 'invoice.id' } } : {} });
    }}><option value="">Choose an available action</option>{actions.map(action => <option key={`${action.key}@${action.contractVersion}`} value={`${action.key}@${action.contractVersion}`}>{action.key} · v{action.contractVersion} · {action.riskTier}</option>)}</NativeSelect></label>
    {selected && <>
      <p className="text-xs text-muted-foreground">{selected.riskTier === 'Read' ? 'Read action. Current access is checked when this step executes.' : `${selected.riskTier} action. The server requires its own human approval before an effect.`}</p>
      <JsonField label="Input bindings: literal, invoice fact, or prior step/output" value={parameters.inputs ?? {}} onChange={value => onChange({ ...parameters, inputs: value as WorkflowNodeParams['inputs'] })} />
      <details className="text-xs"><summary className="cursor-pointer">Action input contract</summary><pre className="mt-2 overflow-auto whitespace-pre-wrap break-all rounded-md border p-2">{JSON.stringify(selected.inputSchema, null, 2)}</pre></details>
      {selected.riskTier !== 'Read' && <><label className="block space-y-1 text-xs">Tenant reviewer role ID<Input value={parameters.reviewer?.roleId ?? ''} onChange={event => onChange({ ...parameters, reviewer: { roleId: event.target.value, forbidSelfApproval: parameters.reviewer?.forbidSelfApproval ?? true } })} /></label>
        <label className="flex items-center gap-2 text-xs"><Checkbox checked={parameters.reviewer?.forbidSelfApproval ?? true} onCheckedChange={value => onChange({ ...parameters, reviewer: { roleId: parameters.reviewer?.roleId ?? '', forbidSelfApproval: value === true } })} />Require a separate reviewer from initiator and invoice creator</label></>}
      {connector && <>
        <label className="block space-y-1 text-xs">Pinned account<NativeSelect value={parameters.connection?.connectionId ?? ''} onChange={event => {
          const connectionId = event.target.value;
          if (!connectionId) { onChange({ ...parameters, connection: undefined }); return; }
          onChange({ ...parameters, connection: { connectionId, workflowGrantId: '', allowedActions: [{ key: selected.key, version: selected.contractVersion }] } });
          const actionKey = selected.key;
          const contractVersion = selected.contractVersion;
          void connectorService.binding(connectionId, workflowId, { key: actionKey, version: contractVersion }).then(binding => {
            const current = latest.current;
            if (current.parameters.connection?.connectionId === connectionId && current.parameters.actionKey === actionKey && current.parameters.contractVersion === contractVersion)
              current.onChange({ ...current.parameters, connection: binding });
          }).catch(reason => setError(connectorError(reason)));
        }}><option value="">Unresolved: save as a draft</option>{connections.map(account => <option key={account.id} value={account.id}>{account.displayName} · {account.ownership} · {account.status}</option>)}</NativeSelect></label>
        <label className="block space-y-1 text-xs">Pinned workflow grant ID<Input value={parameters.connection?.workflowGrantId ?? ''} onChange={event => onChange({ ...parameters, connection: { connectionId: parameters.connection?.connectionId ?? '', workflowGrantId: event.target.value, allowedActions: parameters.connection?.allowedActions ?? [{ key: selected.key, version: selected.contractVersion }] } })} /></label>
        <p className="text-xs text-muted-foreground">A published version keeps this account and grant. Changing a default account has no effect on it.</p>
        <Button variant="outline" size="sm" onClick={() => { void connectorService.createRequest({ connectorKey: connector, ownership: connections.find(account => account.id === parameters.connection?.connectionId)?.ownership ?? 'User',
          connectionId: parameters.connection?.connectionId || undefined, workflowId, actions: [{ key: selected.key, version: selected.contractVersion }], purpose: `Use ${selected.key} in this invoice workflow`, idempotencyKey: crypto.randomUUID() })
          .then(request => setRequestPath(request.reviewPath)).catch(reason => setError(connectorError(reason))); }}>Request this connection use</Button>
        {requestPath && <Link to={requestPath} className="block text-sm text-primary underline">Review connection request</Link>}
      </>}
    </>}
  </div>;
}
