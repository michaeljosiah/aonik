import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { connectorError, connectorService, type ConnectorMutationStatus } from '@/services/connectorService';

export function ConnectorMutationStatusPanel() {
  const [id, setId] = useState(''); const [value, setValue] = useState<ConnectorMutationStatus | null>(null);
  const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false);
  const check = async (reconcile = false) => {
    setBusy(true); setError(null);
    try { setValue(await (reconcile ? connectorService.reconcileMutation(id) : connectorService.mutationStatus(id))); }
    catch (reason) { setError(connectorError(reason)); setValue(null); } finally { setBusy(false); }
  };
  return <Card><CardHeader><CardTitle>Check a connector operation</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
    <p className="text-muted-foreground">Use the approval request ID from your chat or workflow to see its recorded dispatch status.</p>
    <label className="block space-y-1">Approval request ID<Input value={id} onChange={event => { setId(event.target.value); setValue(null); }} /></label>
    <Button variant="outline" disabled={busy || !/^[0-9a-f-]{36}$/i.test(id)} onClick={() => void check()}>Check recorded status</Button>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {value && <div className="space-y-2 rounded-md border p-3"><p>{value.action.key} · {value.status}</p>
      {value.errorCode && <p>Held reason: {value.errorCode}</p>}
      {value.status === 'NeedsAttention' && (value.action.key.startsWith('gmail.')
        ? <><p>Check Gmail for the exact reviewed content or target state. Ambiguous evidence remains held for investigation.</p><Button disabled={busy} onClick={() => void check(true)}>Reconcile with Gmail</Button></>
        : <p>Review the provider’s activity for this operation before initiating further work. AONIK holds the original write for investigation.</p>)}
      {value.status === 'InFlight' && <p>The existing operation is still in progress. Check its status again before considering another action.</p>}
      {value.result != null && <details><summary className="cursor-pointer">Recorded provider receipt</summary><pre className="mt-2 overflow-auto whitespace-pre-wrap break-all text-xs">{JSON.stringify(value.result, null, 2)}</pre></details>}
    </div>}
  </CardContent></Card>;
}
