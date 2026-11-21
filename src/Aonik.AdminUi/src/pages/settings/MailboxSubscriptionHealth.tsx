import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { connectorService, connectorError, type MailboxSubscriptionHealth as Health } from '@/services/connectorService';

export function MailboxSubscriptionHealth({ connectionId }: { connectionId: string }) {
  const [value, setValue] = useState<Health | null | undefined>(); const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false);
  return <div className="space-y-2 text-sm"><Button variant="outline" size="sm" disabled={busy} onClick={() => { setBusy(true); setError(null); void connectorService.subscription(connectionId).then(result => setValue(result.subscription)).catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Check mailbox synchronization</Button>
    {error && <p role="alert" className="text-destructive">{error}</p>}{value === null && <p className="text-muted-foreground">No automatic mailbox subscription is configured.</p>}
    {value && <div className="space-y-1 rounded-md border p-3 text-xs"><p>{value.status} · {value.mode}{value.fullSync && ' · Resuming bounded full synchronization'}</p><p>Last successful page: {value.lastSuccessAtUtc ? new Date(value.lastSuccessAtUtc).toLocaleString() : 'pending'}</p><p>Watch expires: {value.watchExpiresAtUtc ? new Date(value.watchExpiresAtUtc).toLocaleString() : 'no provider watch'}</p><p>Polling schedule ends: {new Date(value.pollTaskEndAtUtc).toLocaleString()}</p>{value.lastError && <p>Attention: {value.lastError}</p>}</div>}
  </div>;
}
