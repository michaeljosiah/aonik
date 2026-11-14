import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { api } from '@/lib/api';
import { connectorError } from '@/services/connectorService';

export function WorkflowExecutionPolicyPanel() {
  const [execution, setExecution] = useState(false); const [automatic, setAutomatic] = useState(false);
  const [loaded, setLoaded] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState<string | null>(null); const [review, setReview] = useState(false);
  useEffect(() => { void Promise.all(['ExecutionEnabled', 'AutomaticTriggersEnabled'].map(key => api.get<{ value: string | null }>(`/tenant/settings/values/Workflows.${key}`)))
    .then(([manual, unattended]) => { setExecution(manual.value === 'true'); setAutomatic(unattended.value === 'true'); setLoaded(true); })
    .catch(reason => setError(connectorError(reason))); }, []);
  return <Card><CardHeader><CardTitle>Workflow execution policy</CardTitle></CardHeader><CardContent className="space-y-3 text-sm">
    <p className="text-muted-foreground">Both settings default to disabled. Platform restrictions, exact actor grants, approved trigger activation and individual action approvals continue to apply.</p>
    {error && <p role="alert" className="text-destructive">{error}</p>}
    <label className="flex items-center gap-2"><Checkbox disabled={!loaded || busy} checked={execution} onCheckedChange={value => { setExecution(value === true); setReview(false); }} />Enable workflow execution for this tenant</label>
    <label className="flex items-center gap-2"><Checkbox disabled={!loaded || busy} checked={automatic} onCheckedChange={value => { setAutomatic(value === true); setReview(false); }} />Enable approved automatic triggers</label>
    <Button variant="outline" disabled={!loaded || busy} onClick={() => setReview(true)}>Review workflow policy</Button>
    {review && <div className="space-y-2 rounded-md border p-3"><p>Set workflow execution to {execution ? 'enabled' : 'disabled'} and automatic admission to {automatic ? 'enabled' : 'disabled'}? Disabling blocks the next authorization check on queued work.</p>
      <Button disabled={busy} onClick={() => { setBusy(true); setError(null); void (async () => {
        // Disable admission before execution; enable execution before allowing sources.
        if (!automatic) await api.put('/tenant/settings/values/Workflows.AutomaticTriggersEnabled', { value: 'false' });
        await api.put('/tenant/settings/values/Workflows.ExecutionEnabled', { value: String(execution) });
        if (automatic) await api.put('/tenant/settings/values/Workflows.AutomaticTriggersEnabled', { value: 'true' });
        setReview(false);
      })().catch(reason => setError(connectorError(reason))).finally(() => setBusy(false)); }}>Confirm workflow policy</Button></div>}
  </CardContent></Card>;
}
